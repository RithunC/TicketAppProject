using Microsoft.EntityFrameworkCore;
using TicketWebApp.Interfaces;
using TicketWebApp.Models;
using TicketWebApp.Models.DTOs;

namespace TicketWebApp.Services
{
    public class AttachmentService : IAttachmentService
    {
        private readonly IRepository<long, Attachment> _attachRepo;
        private readonly IRepository<long, User> _userRepo;
        private readonly IRepository<long, Ticket> _ticketRepo;

        public AttachmentService(
            IRepository<long, Attachment> attachRepo,
            IRepository<long, User> userRepo,
            IRepository<long, Ticket> ticketRepo)
        {
            _attachRepo = attachRepo;
            _userRepo = userRepo;
            _ticketRepo = ticketRepo;
        }

        public async Task<AttachmentResponseDto> UploadAsync(long ticketId, long uploadedByUserId, IFormFile file)
        {
            if (file == null)
                throw new Exception("No file provided");

            if (ticketId <= 0)
                throw new Exception("Invalid ticket id");

            if (uploadedByUserId <= 0)
                throw new Exception("Invalid user id");

            // ✅ Load ticket with status
            var ticket = await _ticketRepo.GetQueryable()
                .Include(t => t.Status)
                .FirstOrDefaultAsync(t => t.Id == ticketId);

            if (ticket == null)
                throw new Exception("Ticket not found");

            // ✅ BLOCK IF CLOSED
            if (ticket.Status != null && ticket.Status.IsClosedState)
                throw new Exception("Cannot upload attachments to a closed ticket.");

            // ✅ Save the file
            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            Directory.CreateDirectory(folder);

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}"
                .Replace("/", "_")
                .Replace("\\", "_");

            var fullPath = Path.Combine(folder, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var relPath = $"/uploads/{fileName}";

            var attachment = new Attachment
            {
                TicketId = ticketId,
                UploadedByUserId = uploadedByUserId,
                FileName = file.FileName, //saves original filename not guid name
                ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                    ? "application/octet-stream"
                    : file.ContentType,
                FileSizeBytes = file.Length, //save file size in bytes
                StoragePath = relPath, //This is stored in the database, not the physical path.
                UploadedAt = DateTime.UtcNow
            };

            await _attachRepo.Add(attachment); //save attachment to db

            var by = await _userRepo.Get(uploadedByUserId); //fetch uploaded user
            if (by == null)
                throw new Exception("Uploaded-by user not found");

            return new AttachmentResponseDto
            {
                Id = attachment.Id,
                TicketId = ticketId,
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
                FileSizeBytes = attachment.FileSizeBytes,
                StoragePath = attachment.StoragePath, //relative file url
                UploadedByUserId = uploadedByUserId,
                UploadedByName = by.DisplayName,
                UploadedAt = attachment.UploadedAt
            };
        }

        public async Task<IReadOnlyList<AttachmentResponseDto>> GetByTicketAsync(long ticketId)
        {
            if (ticketId <= 0)
                throw new Exception("Invalid ticket id");

            var list = await _attachRepo.GetQueryable()
                .Include(a => a.UploadedBy)
                .Where(a => a.TicketId == ticketId)
                .OrderByDescending(a => a.UploadedAt)
                .ToListAsync();

            return list.Select(a => new AttachmentResponseDto
            {
                Id = a.Id,
                TicketId = a.TicketId,
                FileName = a.FileName,
                ContentType = a.ContentType,
                FileSizeBytes = a.FileSizeBytes,
                StoragePath = a.StoragePath,
                UploadedByUserId = a.UploadedByUserId,
                UploadedByName = a.UploadedBy?.DisplayName ?? "",
                UploadedAt = a.UploadedAt
            }).ToList();
        }

        public async Task<AttachmentDownloadResult?> GetDownloadAsync(long attachmentId, long requestingUserId)
        {
            if (attachmentId <= 0)
                throw new Exception("Invalid attachment id");

            var attachment = await _attachRepo.GetQueryable()
                .Include(a => a.Ticket)
                .FirstOrDefaultAsync(a => a.Id == attachmentId);

            if (attachment == null)
                throw new Exception("Attachment not found");

            var fullPath = ResolvePhysicalPath(attachment.StoragePath);//convert to physical path
            if (!File.Exists(fullPath))
                throw new Exception("File not found on server");

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            return new AttachmentDownloadResult
            {
                Stream = stream,
                FileName = attachment.FileName,
                ContentType = string.IsNullOrWhiteSpace(attachment.ContentType)
                    ? "application/octet-stream"
                    : attachment.ContentType,
                FileSizeBytes = attachment.FileSizeBytes
            };
        }

        public async Task<bool> DeleteAsync(long attachmentId, long requestingUserId)
        {
            if (attachmentId <= 0)
                throw new Exception("Invalid attachment id");

            var attachment = await _attachRepo.Get(attachmentId);
            if (attachment == null)
                throw new Exception("Attachment not found");

            if (!await CanDeleteAttachmentAsync(attachment, requestingUserId))
                throw new UnauthorizedAccessException("Not allowed to delete this attachment");

            var fullPath = ResolvePhysicalPath(attachment.StoragePath);
            if (File.Exists(fullPath))
            {
                try { File.Delete(fullPath); }
                catch { /* ignore */ }
            }

            await _attachRepo.Delete(attachmentId);
            return true;
        }

        private string ResolvePhysicalPath(string storagePath) //prevent pathtraversal attacks
        {
            var wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var relative = storagePath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(wwwroot, relative));

            var rootFull = Path.GetFullPath(wwwroot);
            if (!fullPath.StartsWith(rootFull, StringComparison.Ordinal))
                throw new Exception("Invalid storage path");

            return fullPath; //Return safe absolute path
        }

        private async Task<bool> CanViewTicketAsync(long ticketId, long userId)
        {
            var ticket = await _ticketRepo.Get(ticketId);
            if (ticket == null)
                throw new Exception("Ticket not found");

            return ticket.CreatedByUserId == userId || await IsSupportOrAdmin(userId);
        }

        private async Task<bool> CanDeleteAttachmentAsync(Attachment a, long userId)
        {
            if (a.UploadedByUserId == userId) //You can delete your own attachment
                return true;

            var ticket = await _ticketRepo.Get(a.TicketId);
            if (ticket == null)
                throw new Exception("Ticket not found");

            if (ticket.CreatedByUserId == userId)
                return true;

            return await IsSupportOrAdmin(userId);
        }

        private Task<bool> IsSupportOrAdmin(long userId)
        {
            return Task.FromResult(false);
        }
    }
}