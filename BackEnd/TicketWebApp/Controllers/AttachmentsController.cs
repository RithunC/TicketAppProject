using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketWebApp.Interfaces;

namespace TicketWebApp.Controllers
{
    [ApiController]
    [Route("api/attachments")]
    [Authorize]
    public class AttachmentsController : ControllerBase
    {
        private readonly IAttachmentService _attachments;

        public AttachmentsController(IAttachmentService attachments)
        {
            _attachments = attachments;
        }

        private long? CurrentUserId()
        {
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return long.TryParse(id, out var uid) ? uid : (long?)null;
        }

        // -------------------------------------------------------------
        // POST api/attachments/{ticketId}  (upload file)
        // -------------------------------------------------------------
        [HttpPost("{ticketId:long}")]
        public async Task<IActionResult> Upload(
            long ticketId,
            IFormFile file,
            [FromHeader(Name = "X-User-Id")] long? xUserId = null)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { error = "File is required." });

                var currentUserId = xUserId ?? CurrentUserId();
                if (currentUserId is null)
                    return Unauthorized();

                var res = await _attachments.UploadAsync(ticketId, currentUserId.Value, file);
                return Ok(res);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500,
                    new { error = "Server error during file upload.", detail = ex.Message });
            }
        }

        // -------------------------------------------------------------
        // GET api/attachments/{ticketId}  (list all attachments)
        // -------------------------------------------------------------
        [HttpGet("{ticketId:long}")]
        public async Task<IActionResult> GetByTicket(long ticketId)
        {
            try
            {
                var list = await _attachments.GetByTicketAsync(ticketId);
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500,
                    new { error = "Server error", detail = ex.Message });
            }
        }

        // -------------------------------------------------------------
        // GET api/attachments/{attachmentId}/download
        // -------------------------------------------------------------
        [HttpGet("{attachmentId:long}/download")]
        public async Task<IActionResult> Download(long attachmentId)
        {
            try
            {
                var currentUserId = CurrentUserId();
                if (currentUserId is null)
                    return Unauthorized();

                var fileResult = await _attachments.GetDownloadAsync(attachmentId, currentUserId.Value);

                if (fileResult is null)
                    return NotFound();

                // Force browser download
                var cd = new System.Net.Mime.ContentDisposition
                {
                    FileName = fileResult.FileName,
                    Inline = false
                };
                Response.Headers["Content-Disposition"] = cd.ToString();

                return File(fileResult.Stream, fileResult.ContentType, fileResult.FileName);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500,
                    new { error = "Server error while downloading file.", detail = ex.Message });
            }
        }

        // -------------------------------------------------------------
        // DELETE api/attachments/{attachmentId}
        // -------------------------------------------------------------
        [HttpDelete("{attachmentId:long}")]
        public async Task<IActionResult> Delete(long attachmentId)
        {
            try
            {
                var currentUserId = CurrentUserId();
                if (currentUserId is null)
                    return Unauthorized();

                var deleted = await _attachments.DeleteAsync(attachmentId, currentUserId.Value);

                if (!deleted)
                    return NotFound();

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500,
                    new { error = "Server error while deleting attachment.", detail = ex.Message });
            }
        }
    }
}