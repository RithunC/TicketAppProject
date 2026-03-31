using TestProject.Helpers;
using TicketWebApp.Models;
using TicketWebApp.Models.DTOs;
using TicketWebApp.Repositories;
using TicketWebApp.Services;
using static TicketWebApp.Models.DTOs.CommentDtos;
using static TicketWebApp.Models.DTOs.TicketDtos;

namespace TestProject.Services
{
    public class CommentServiceTests
    {
        private static (CommentService sut, TicketWebApp.Contexts.ComplaintContext ctx) Build(string db)
        {
            var ctx = DbContextFactory.CreateWithSeed(db);
            var sut = new CommentService(
                new Repository<long, Comment>(ctx),
                new Repository<long, Ticket>(ctx),
                new Repository<long, User>(ctx));
            return (sut, ctx);
        }

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

        // ── AddAsync ──────────────────────────────────────────────────────────

        [Fact]
        public async Task AddAsync_AddsCommentToOpenTicket()
        {
            var (sut, ctx) = Build(nameof(AddAsync_AddsCommentToOpenTicket));
            ctx.Users.Add(MakeUser(1, "user1"));
            ctx.Tickets.Add(MakeTicket(1, 1));
            await ctx.SaveChangesAsync();

            var result = await sut.AddAsync(1, new CommentCreateDto
            {
                TicketId = 1,
                Body = "Hello",
                IsInternal = false
            });

            Assert.Equal("Hello", result.Body);
            Assert.Equal(1, result.TicketId);
        }

        [Fact]
        public async Task AddAsync_ThrowsForClosedTicket()
        {
            var (sut, ctx) = Build(nameof(AddAsync_ThrowsForClosedTicket));
            ctx.Users.Add(MakeUser(1, "user1"));
            ctx.Tickets.Add(MakeTicket(1, 1, statusId: 4)); // Closed
            await ctx.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "Hi" }));
        }

        [Fact]
        public async Task AddAsync_ThrowsForMissingTicket()
        {
            var (sut, ctx) = Build(nameof(AddAsync_ThrowsForMissingTicket));
            ctx.Users.Add(MakeUser(1, "user1"));
            await ctx.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.AddAsync(1, new CommentCreateDto { TicketId = 999, Body = "Hi" }));
        }

        // ── GetByTicketAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task GetByTicketAsync_ReturnsCommentsForTicket()
        {
            var (sut, ctx) = Build(nameof(GetByTicketAsync_ReturnsCommentsForTicket));
            ctx.Users.Add(MakeUser(1, "user1"));
            ctx.Tickets.Add(MakeTicket(1, 1));
            await ctx.SaveChangesAsync();
            await sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "C1" });
            await sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "C2" });

            var result = await sut.GetByTicketAsync(1);

            Assert.Equal(2, result.Count);
        }

        // ── EditAsync ─────────────────────────────────────────────────────────

        [Fact]
        public async Task EditAsync_AllowsOwnerToEditOwnComment()
        {
            var (sut, ctx) = Build(nameof(EditAsync_AllowsOwnerToEditOwnComment));
            ctx.Users.Add(MakeUser(1, "user1", roleId: 3));
            ctx.Tickets.Add(MakeTicket(1, 1));
            await ctx.SaveChangesAsync();
            var comment = await sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "Original" });

            var result = await sut.EditAsync(1, comment.Id, "Updated");

            Assert.Equal("Updated", result!.Body);
        }

        [Fact]
        public async Task EditAsync_ThrowsWhenNonOwnerNonStaffTriesToEdit()
        {
            var (sut, ctx) = Build(nameof(EditAsync_ThrowsWhenNonOwnerNonStaffTriesToEdit));
            ctx.Users.AddRange(MakeUser(1, "owner", roleId: 3), MakeUser(2, "other", roleId: 3));
            ctx.Tickets.Add(MakeTicket(1, 1));
            await ctx.SaveChangesAsync();
            var comment = await sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "Original" });

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                sut.EditAsync(2, comment.Id, "Hacked"));
        }

        [Fact]
        public async Task EditAsync_AllowsAdminToEditAnyComment()
        {
            var (sut, ctx) = Build(nameof(EditAsync_AllowsAdminToEditAnyComment));
            ctx.Users.AddRange(MakeUser(1, "employee", roleId: 3), MakeUser(2, "admin", roleId: 1));
            ctx.Tickets.Add(MakeTicket(1, 1));
            await ctx.SaveChangesAsync();
            var comment = await sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "Original" });

            var result = await sut.EditAsync(2, comment.Id, "Admin edit");

            Assert.Equal("Admin edit", result!.Body);
        }

        // ── DeleteAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAsync_AllowsOwnerToDeleteOwnComment()
        {
            var (sut, ctx) = Build(nameof(DeleteAsync_AllowsOwnerToDeleteOwnComment));
            ctx.Users.Add(MakeUser(1, "user1", roleId: 3));
            ctx.Tickets.Add(MakeTicket(1, 1));
            await ctx.SaveChangesAsync();
            var comment = await sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "Bye" });

            var result = await sut.DeleteAsync(1, comment.Id);

            Assert.True(result);
        }

        [Fact]
        public async Task DeleteAsync_ReturnsFalseForMissingComment()
        {
            var (sut, ctx) = Build(nameof(DeleteAsync_ReturnsFalseForMissingComment));
            ctx.Users.Add(MakeUser(1, "user1"));
            await ctx.SaveChangesAsync();

            var result = await sut.DeleteAsync(1, 999);

            Assert.False(result);
        }

        [Fact]
        public async Task DeleteAsync_ThrowsForClosedTicket()
        {
            var (sut, ctx) = Build(nameof(DeleteAsync_ThrowsForClosedTicket));
            ctx.Users.Add(MakeUser(1, "user1", roleId: 3));
            // Add comment on open ticket first, then close it
            ctx.Tickets.Add(MakeTicket(1, 1, statusId: 1));
            await ctx.SaveChangesAsync();
            var comment = await sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "Hi" });

            // Now close the ticket
            var ticket = ctx.Tickets.Find(1L)!;
            ticket.StatusId = 4;
            await ctx.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.DeleteAsync(1, comment.Id));
        }
    }
}
