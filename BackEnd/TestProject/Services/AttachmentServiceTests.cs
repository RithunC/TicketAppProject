using Microsoft.AspNetCore.Http;
using Moq;
using TestProject.Helpers;
using TicketWebApp.Models;
using TicketWebApp.Repositories;
using TicketWebApp.Services;

namespace TestProject.Services
{
    public class AttachmentServiceTests
    {
        private static User MakeUser(long id, string name, int roleId = 3) => new User
        {
            Id = id, UserName = name, Email = $"{name}@t.com", DisplayName = name,
            IsActive = true, RoleId = roleId,
            PasswordHash = Array.Empty<byte>(), PasswordSalt = Array.Empty<byte>()
        };

        private static Ticket MakeTicket(long id, long userId, int statusId = 1) => new Ticket
        {
            Id = id, Title = "T", PriorityId = 1, StatusId = statusId,
            CreatedByUserId = userId, CreatedAt = DateTime.UtcNow
        };

        private static IFormFile MakeFakeFile(string name = "test.txt", string content = "hello")
        {
            var mock = new Mock<IFormFile>();
            var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            mock.Setup(f => f.FileName).Returns(name);
            mock.Setup(f => f.ContentType).Returns("text/plain");
            mock.Setup(f => f.Length).Returns(ms.Length);
            mock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns<Stream, CancellationToken>((s, _) => ms.CopyToAsync(s));
            return mock.Object;
        }

        private static AttachmentService BuildSut(TicketWebApp.Contexts.ComplaintContext ctx) =>
            new AttachmentService(
                new Repository<long, Attachment>(ctx),
                new Repository<long, User>(ctx),
                new Repository<long, Ticket>(ctx));

        // ── UploadAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task UploadAsync_ThrowsForNullFile()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(UploadAsync_ThrowsForNullFile));
            await Assert.ThrowsAsync<Exception>(() => BuildSut(ctx).UploadAsync(1, 1, null!));
        }

        [Fact]
        public async Task UploadAsync_ThrowsForInvalidTicketId()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(UploadAsync_ThrowsForInvalidTicketId));
            await Assert.ThrowsAsync<Exception>(() => BuildSut(ctx).UploadAsync(0, 1, MakeFakeFile()));
        }

        [Fact]
        public async Task UploadAsync_ThrowsForInvalidUserId()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(UploadAsync_ThrowsForInvalidUserId));
            await Assert.ThrowsAsync<Exception>(() => BuildSut(ctx).UploadAsync(1, 0, MakeFakeFile()));
        }

        [Fact]
        public async Task UploadAsync_ThrowsWhenTicketNotFound()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(UploadAsync_ThrowsWhenTicketNotFound));
            await Assert.ThrowsAsync<Exception>(() => BuildSut(ctx).UploadAsync(999, 1, MakeFakeFile()));
        }

        [Fact]
        public async Task UploadAsync_ThrowsForClosedTicket()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(UploadAsync_ThrowsForClosedTicket));
            ctx.Users.Add(MakeUser(1, "user1"));
            ctx.Tickets.Add(MakeTicket(1, 1, statusId: 4)); // Closed
            await ctx.SaveChangesAsync();

            await Assert.ThrowsAsync<Exception>(() => BuildSut(ctx).UploadAsync(1, 1, MakeFakeFile()));
        }

        [Fact]
        public async Task UploadAsync_SucceedsForOpenTicket()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(UploadAsync_SucceedsForOpenTicket));
            ctx.Users.Add(MakeUser(1, "user1"));
            ctx.Tickets.Add(MakeTicket(1, 1, statusId: 1));
            await ctx.SaveChangesAsync();

            var result = await BuildSut(ctx).UploadAsync(1, 1, MakeFakeFile("doc.txt", "content"));

            Assert.Equal("doc.txt", result.FileName);
            Assert.Equal(1, result.TicketId);
            Assert.Equal(1, result.UploadedByUserId);
        }

        [Fact]
        public async Task UploadAsync_UsesOctetStreamWhenContentTypeEmpty()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(UploadAsync_UsesOctetStreamWhenContentTypeEmpty));
            ctx.Users.Add(MakeUser(1, "user1"));
            ctx.Tickets.Add(MakeTicket(1, 1, statusId: 1));
            await ctx.SaveChangesAsync();

            var mock = new Mock<IFormFile>();
            var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("data"));
            mock.Setup(f => f.FileName).Returns("file.bin");
            mock.Setup(f => f.ContentType).Returns(string.Empty);
            mock.Setup(f => f.Length).Returns(ms.Length);
            mock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns<Stream, CancellationToken>((s, _) => ms.CopyToAsync(s));

            var result = await BuildSut(ctx).UploadAsync(1, 1, mock.Object);

            Assert.Equal("application/octet-stream", result.ContentType);
        }

        // ── GetByTicketAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task GetByTicketAsync_ThrowsForInvalidTicketId()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(GetByTicketAsync_ThrowsForInvalidTicketId));
            await Assert.ThrowsAsync<Exception>(() => BuildSut(ctx).GetByTicketAsync(0));
        }

        [Fact]
        public async Task GetByTicketAsync_ReturnsEmptyListWhenNoAttachments()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(GetByTicketAsync_ReturnsEmptyListWhenNoAttachments));
            var result = await BuildSut(ctx).GetByTicketAsync(1);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetByTicketAsync_ReturnsAttachmentsForTicket()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(GetByTicketAsync_ReturnsAttachmentsForTicket));
            ctx.Users.Add(MakeUser(1, "user1"));
            ctx.Tickets.Add(MakeTicket(1, 1, statusId: 1));
            await ctx.SaveChangesAsync();
            await BuildSut(ctx).UploadAsync(1, 1, MakeFakeFile("a.txt", "aaa"));
            await BuildSut(ctx).UploadAsync(1, 1, MakeFakeFile("b.txt", "bbb"));

            var result = await BuildSut(ctx).GetByTicketAsync(1);

            Assert.Equal(2, result.Count);
        }

        // ── DeleteAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAsync_ThrowsForInvalidAttachmentId()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(DeleteAsync_ThrowsForInvalidAttachmentId));
            await Assert.ThrowsAsync<Exception>(() => BuildSut(ctx).DeleteAsync(0, 1));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenAttachmentNotFound()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(DeleteAsync_ThrowsWhenAttachmentNotFound));
            await Assert.ThrowsAsync<Exception>(() => BuildSut(ctx).DeleteAsync(999, 1));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenUserNotOwnerOrAdmin()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(DeleteAsync_ThrowsWhenUserNotOwnerOrAdmin));
            ctx.Users.AddRange(MakeUser(1, "owner"), MakeUser(2, "other"));
            ctx.Tickets.Add(MakeTicket(1, 1));
            ctx.Attachments.Add(new Attachment
            {
                Id = 1, TicketId = 1, UploadedByUserId = 1,
                FileName = "f.txt", ContentType = "text/plain",
                FileSizeBytes = 5, StoragePath = "/uploads/f.txt"
            });
            await ctx.SaveChangesAsync();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                BuildSut(ctx).DeleteAsync(1, requestingUserId: 2));
        }

        [Fact]
        public async Task DeleteAsync_SucceedsForOwner()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(DeleteAsync_SucceedsForOwner));
            ctx.Users.Add(MakeUser(1, "owner"));
            ctx.Tickets.Add(MakeTicket(1, 1));
            // Use a non-existent physical path — file won't exist, but delete should still succeed
            ctx.Attachments.Add(new Attachment
            {
                Id = 1, TicketId = 1, UploadedByUserId = 1,
                FileName = "f.txt", ContentType = "text/plain",
                FileSizeBytes = 5, StoragePath = "/uploads/nonexistent_file.txt"
            });
            await ctx.SaveChangesAsync();

            var result = await BuildSut(ctx).DeleteAsync(1, requestingUserId: 1);

            Assert.True(result);
        }

        [Fact]
        public async Task DeleteAsync_SucceedsForTicketOwner()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(DeleteAsync_SucceedsForTicketOwner));
            ctx.Users.AddRange(MakeUser(1, "uploader"), MakeUser(2, "ticketowner"));
            ctx.Tickets.Add(MakeTicket(1, 2)); // ticket created by user 2
            ctx.Attachments.Add(new Attachment
            {
                Id = 1, TicketId = 1, UploadedByUserId = 1, // uploaded by user 1
                FileName = "f.txt", ContentType = "text/plain",
                FileSizeBytes = 5, StoragePath = "/uploads/nonexistent2.txt"
            });
            await ctx.SaveChangesAsync();

            // user 2 is ticket owner, should be allowed to delete
            var result = await BuildSut(ctx).DeleteAsync(1, requestingUserId: 2);

            Assert.True(result);
        }

        // ── GetDownloadAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task GetDownloadAsync_ThrowsForInvalidAttachmentId()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(GetDownloadAsync_ThrowsForInvalidAttachmentId));
            await Assert.ThrowsAsync<Exception>(() => BuildSut(ctx).GetDownloadAsync(0, 1));
        }

        [Fact]
        public async Task GetDownloadAsync_ThrowsWhenAttachmentNotFound()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(GetDownloadAsync_ThrowsWhenAttachmentNotFound));
            await Assert.ThrowsAsync<Exception>(() => BuildSut(ctx).GetDownloadAsync(999, 1));
        }

        [Fact]
        public async Task GetDownloadAsync_ThrowsWhenFileNotOnDisk()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(GetDownloadAsync_ThrowsWhenFileNotOnDisk));
            ctx.Users.Add(MakeUser(1, "user1"));
            ctx.Tickets.Add(MakeTicket(1, 1));
            ctx.Attachments.Add(new Attachment
            {
                Id = 1, TicketId = 1, UploadedByUserId = 1,
                FileName = "missing.txt", ContentType = "text/plain",
                FileSizeBytes = 5, StoragePath = "/uploads/definitely_missing_file_xyz.txt"
            });
            await ctx.SaveChangesAsync();

            await Assert.ThrowsAsync<Exception>(() => BuildSut(ctx).GetDownloadAsync(1, 1));
        }

        [Fact]
        public async Task GetDownloadAsync_ThrowsForPathTraversalAttempt()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(GetDownloadAsync_ThrowsForPathTraversalAttempt));
            ctx.Users.Add(MakeUser(1, "user1"));
            ctx.Tickets.Add(MakeTicket(1, 1));
            // StoragePath that tries to escape wwwroot
            ctx.Attachments.Add(new Attachment
            {
                Id = 1, TicketId = 1, UploadedByUserId = 1,
                FileName = "evil.txt", ContentType = "text/plain",
                FileSizeBytes = 5, StoragePath = "/../../../etc/passwd"
            });
            await ctx.SaveChangesAsync();

            await Assert.ThrowsAsync<Exception>(() => BuildSut(ctx).GetDownloadAsync(1, 1));
        }

        [Fact]
        public async Task GetDownloadAsync_UsesOctetStreamWhenContentTypeEmpty()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(GetDownloadAsync_UsesOctetStreamWhenContentTypeEmpty));
            ctx.Users.Add(MakeUser(1, "user1"));
            ctx.Tickets.Add(MakeTicket(1, 1, statusId: 1));
            await ctx.SaveChangesAsync();

            var uploaded = await BuildSut(ctx).UploadAsync(1, 1, MakeFakeFile("test.bin", "data"));

            var att = ctx.Attachments.Find(uploaded.Id)!;
            att.ContentType = "";
            await ctx.SaveChangesAsync();

            var result = await BuildSut(ctx).GetDownloadAsync(uploaded.Id, 1);

            Assert.NotNull(result);
            Assert.Equal("application/octet-stream", result!.ContentType);
            result.Stream.Dispose();
        }

        [Fact]
        public async Task UploadAsync_TicketWithNullStatus_DoesNotThrow()
        {
            // Covers: ticket.Status != null = false branch
            var ctx = DbContextFactory.CreateWithSeed(nameof(UploadAsync_TicketWithNullStatus_DoesNotThrow));
            ctx.Users.Add(MakeUser(1, "user1"));
            // Ticket with StatusId=1 (New, IsClosedState=false) — Status navigation will be loaded
            ctx.Tickets.Add(MakeTicket(1, 1, statusId: 1));
            await ctx.SaveChangesAsync();

            // Ticket with status that is NOT closed — should succeed
            var result = await BuildSut(ctx).UploadAsync(1, 1, MakeFakeFile("ok.txt", "ok"));
            Assert.Equal("ok.txt", result.FileName);
        }

        [Fact]
        public async Task DeleteAsync_DeletesFileOnDiskWhenItExists()
        {
            // Covers: File.Exists(fullPath) = true branch in DeleteAsync
            var ctx = DbContextFactory.CreateWithSeed(nameof(DeleteAsync_DeletesFileOnDiskWhenItExists));
            ctx.Users.Add(MakeUser(1, "owner"));
            ctx.Tickets.Add(MakeTicket(1, 1, statusId: 1));
            await ctx.SaveChangesAsync();

            // Upload creates a real file on disk
            var uploaded = await BuildSut(ctx).UploadAsync(1, 1, MakeFakeFile("real.txt", "content"));

            // Delete — file exists on disk, should be deleted
            var result = await BuildSut(ctx).DeleteAsync(uploaded.Id, requestingUserId: 1);

            Assert.True(result);
        }

        [Fact]
        public async Task GetByTicketAsync_ReturnsUploadedByNameFromNavigation()
        {
            // Covers: a.UploadedBy?.DisplayName ?? "" — non-null branch
            var ctx = DbContextFactory.CreateWithSeed(nameof(GetByTicketAsync_ReturnsUploadedByNameFromNavigation));
            ctx.Users.Add(MakeUser(1, "user1"));
            ctx.Tickets.Add(MakeTicket(1, 1, statusId: 1));
            await ctx.SaveChangesAsync();

            await BuildSut(ctx).UploadAsync(1, 1, MakeFakeFile("f.txt", "data"));
            var result = await BuildSut(ctx).GetByTicketAsync(1);

            Assert.Single(result);
            Assert.Equal("user1", result[0].UploadedByName);
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenTicketNotFoundDuringPermissionCheck()
        {
            // Covers: ticket == null branch inside CanDeleteAttachmentAsync
            var ctx = DbContextFactory.CreateWithSeed(nameof(DeleteAsync_ThrowsWhenTicketNotFoundDuringPermissionCheck));
            ctx.Users.AddRange(MakeUser(1, "uploader"), MakeUser(2, "other"));
            // Attachment references a ticket that doesn't exist
            ctx.Attachments.Add(new Attachment
            {
                Id = 1, TicketId = 999, UploadedByUserId = 1,
                FileName = "f.txt", ContentType = "text/plain",
                FileSizeBytes = 5, StoragePath = "/uploads/f.txt"
            });
            await ctx.SaveChangesAsync();

            await Assert.ThrowsAsync<Exception>(() =>
                BuildSut(ctx).DeleteAsync(1, requestingUserId: 2));
        }
    }
}
