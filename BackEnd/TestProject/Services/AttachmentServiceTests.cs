using Microsoft.AspNetCore.Http;
using Moq;
using TestProject.Helpers;
using TicketWebApp.Interfaces;
using TicketWebApp.Models;
using TicketWebApp.Repositories;
using TicketWebApp.Services;

namespace TestProject.Services
{
    public class AttachmentServiceTests
    {
        private static User MakeUser(long id, string name) => new User
        {
            Id = id, UserName = name, Email = $"{name}@t.com", DisplayName = name,
            IsActive = true, RoleId = 3,
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

        [Fact]
        public async Task UploadAsync_ThrowsForNullFile()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(UploadAsync_ThrowsForNullFile));
            var sut = new AttachmentService(
                new Repository<long, Attachment>(ctx),
                new Repository<long, User>(ctx),
                new Repository<long, Ticket>(ctx));

            await Assert.ThrowsAsync<Exception>(() =>
                sut.UploadAsync(1, 1, null!));
        }

        [Fact]
        public async Task UploadAsync_ThrowsForInvalidTicketId()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(UploadAsync_ThrowsForInvalidTicketId));
            var sut = new AttachmentService(
                new Repository<long, Attachment>(ctx),
                new Repository<long, User>(ctx),
                new Repository<long, Ticket>(ctx));

            await Assert.ThrowsAsync<Exception>(() =>
                sut.UploadAsync(0, 1, MakeFakeFile()));
        }

        [Fact]
        public async Task UploadAsync_ThrowsWhenTicketNotFound()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(UploadAsync_ThrowsWhenTicketNotFound));
            var sut = new AttachmentService(
                new Repository<long, Attachment>(ctx),
                new Repository<long, User>(ctx),
                new Repository<long, Ticket>(ctx));

            await Assert.ThrowsAsync<Exception>(() =>
                sut.UploadAsync(999, 1, MakeFakeFile()));
        }

        [Fact]
        public async Task UploadAsync_ThrowsForClosedTicket()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(UploadAsync_ThrowsForClosedTicket));
            ctx.Users.Add(MakeUser(1, "user1"));
            ctx.Tickets.Add(MakeTicket(1, 1, statusId: 4)); // Closed
            await ctx.SaveChangesAsync();

            var sut = new AttachmentService(
                new Repository<long, Attachment>(ctx),
                new Repository<long, User>(ctx),
                new Repository<long, Ticket>(ctx));

            await Assert.ThrowsAsync<Exception>(() =>
                sut.UploadAsync(1, 1, MakeFakeFile()));
        }

        [Fact]
        public async Task GetByTicketAsync_ThrowsForInvalidTicketId()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(GetByTicketAsync_ThrowsForInvalidTicketId));
            var sut = new AttachmentService(
                new Repository<long, Attachment>(ctx),
                new Repository<long, User>(ctx),
                new Repository<long, Ticket>(ctx));

            await Assert.ThrowsAsync<Exception>(() => sut.GetByTicketAsync(0));
        }

        [Fact]
        public async Task GetByTicketAsync_ReturnsEmptyListWhenNoAttachments()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(GetByTicketAsync_ReturnsEmptyListWhenNoAttachments));
            var sut = new AttachmentService(
                new Repository<long, Attachment>(ctx),
                new Repository<long, User>(ctx),
                new Repository<long, Ticket>(ctx));

            var result = await sut.GetByTicketAsync(1);

            Assert.Empty(result);
        }

        [Fact]
        public async Task DeleteAsync_ThrowsForInvalidAttachmentId()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(DeleteAsync_ThrowsForInvalidAttachmentId));
            var sut = new AttachmentService(
                new Repository<long, Attachment>(ctx),
                new Repository<long, User>(ctx),
                new Repository<long, Ticket>(ctx));

            await Assert.ThrowsAsync<Exception>(() => sut.DeleteAsync(0, 1));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenAttachmentNotFound()
        {
            var ctx = DbContextFactory.CreateWithSeed(nameof(DeleteAsync_ThrowsWhenAttachmentNotFound));
            var sut = new AttachmentService(
                new Repository<long, Attachment>(ctx),
                new Repository<long, User>(ctx),
                new Repository<long, Ticket>(ctx));

            await Assert.ThrowsAsync<Exception>(() => sut.DeleteAsync(999, 1));
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

            var sut = new AttachmentService(
                new Repository<long, Attachment>(ctx),
                new Repository<long, User>(ctx),
                new Repository<long, Ticket>(ctx));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                sut.DeleteAsync(1, requestingUserId: 2));
        }
    }
}
