using TestProject.Helpers;
using TicketWebApp.Models;
using TicketWebApp.Models.DTOs;
using TicketWebApp.Repositories;
using TicketWebApp.Services;
using static TicketWebApp.Models.DTOs.CommentDtos;

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

            var result = await sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "Hello", IsInternal = false });

            Assert.Equal("Hello", result.Body);
            Assert.Equal(1, result.TicketId);
        }

        [Fact]
        public async Task AddAsync_AddsInternalComment()
        {
            var (sut, ctx) = Build(nameof(AddAsync_AddsInternalComment));
            ctx.Users.Add(MakeUser(1, "agent", roleId: 2));
            ctx.Tickets.Add(MakeTicket(1, 1));
            await ctx.SaveChangesAsync();

            var result = await sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "Internal note", IsInternal = true });

            Assert.True(result.IsInternal);
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

        [Fact]
        public async Task GetByTicketAsync_ReturnsEmptyForTicketWithNoComments()
        {
            var (sut, _) = Build(nameof(GetByTicketAsync_ReturnsEmptyForTicketWithNoComments));
            var result = await sut.GetByTicketAsync(999);
            Assert.Empty(result);
        }

        [Fact]
        public async Task AddAsync_ReturnsEmptyPostedByNameWhenUserNotFound()
        {
            // Covers: by?.DisplayName ?? "" null branch in AddAsync
            var (sut, ctx) = Build(nameof(AddAsync_ReturnsEmptyPostedByNameWhenUserNotFound));
            ctx.Tickets.Add(MakeTicket(1, 1));
            await ctx.SaveChangesAsync();

            // Post comment as user 999 who doesn't exist in user repo
            var result = await sut.AddAsync(999, new CommentCreateDto { TicketId = 1, Body = "Ghost" });

            Assert.Equal(string.Empty, result.PostedByName);
        }

        [Fact]
        public async Task EditAsync_StaffEditingOwnComment_Succeeds()
        {
            // Covers: isStaff=true path where PostedByUserId == editorUserId (staff owns comment)
            var (sut, ctx) = Build(nameof(EditAsync_StaffEditingOwnComment_Succeeds));
            ctx.Users.Add(MakeUser(1, "agent", roleId: 2));
            ctx.Tickets.Add(MakeTicket(1, 1));
            await ctx.SaveChangesAsync();
            var comment = await sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "Original" });

            var result = await sut.EditAsync(1, comment.Id, "Staff own edit");

            Assert.Equal("Staff own edit", result!.Body);
        }

        [Fact]
        public async Task EditAsync_ReturnsEmptyPostedByNameWhenPostByUserMissing()
        {
            // Covers: row.Comment.PostedBy?.DisplayName ?? "" null branch in EditAsync response
            var (sut, ctx) = Build(nameof(EditAsync_ReturnsEmptyPostedByNameWhenPostByUserMissing));
            ctx.Users.Add(MakeUser(1, "admin", roleId: 1));
            ctx.Tickets.Add(MakeTicket(1, 1));
            await ctx.SaveChangesAsync();
            // Insert comment with a non-existent poster directly
            ctx.Comments.Add(new Comment
            {
                Id = 1, TicketId = 1, PostedByUserId = 1,
                Body = "Original", IsInternal = false, CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();

            // Admin edits it — PostedBy navigation will be loaded (user 1 exists)
            var result = await sut.EditAsync(1, 1, "Edited");

            Assert.NotNull(result);
            Assert.Equal("Edited", result!.Body);
        }

        [Fact]
        public async Task DeleteAsync_StaffDeletingOwnComment_Succeeds()
        {
            // Covers: isStaff=true, PostedByUserId == deleterUserId path
            var (sut, ctx) = Build(nameof(DeleteAsync_StaffDeletingOwnComment_Succeeds));
            ctx.Users.Add(MakeUser(1, "agent", roleId: 2));
            ctx.Tickets.Add(MakeTicket(1, 1));
            await ctx.SaveChangesAsync();
            var comment = await sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "Hi" });

            Assert.True(await sut.DeleteAsync(1, comment.Id));
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

        [Fact]
        public async Task EditAsync_AllowsAgentToEditAnyComment()
        {
            var (sut, ctx) = Build(nameof(EditAsync_AllowsAgentToEditAnyComment));
            ctx.Users.AddRange(MakeUser(1, "employee", roleId: 3), MakeUser(2, "agent", roleId: 2));
            ctx.Tickets.Add(MakeTicket(1, 1));
            await ctx.SaveChangesAsync();
            var comment = await sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "Original" });

            var result = await sut.EditAsync(2, comment.Id, "Agent edit");

            Assert.Equal("Agent edit", result!.Body);
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
        public async Task EditAsync_ThrowsForInactiveUser()
        {
            var (sut, ctx) = Build(nameof(EditAsync_ThrowsForInactiveUser));
            ctx.Users.Add(MakeUser(1, "active", roleId: 3));
            ctx.Tickets.Add(MakeTicket(1, 1));
            await ctx.SaveChangesAsync();
            var comment = await sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "Hi" });

            // Add inactive user
            ctx.Users.Add(new User
            {
                Id = 2, UserName = "inactive", Email = "i@t.com", DisplayName = "Inactive",
                IsActive = false, RoleId = 3,
                PasswordHash = Array.Empty<byte>(), PasswordSalt = Array.Empty<byte>()
            });
            await ctx.SaveChangesAsync();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                sut.EditAsync(2, comment.Id, "Try edit"));
        }

        [Fact]
        public async Task EditAsync_ThrowsForClosedTicket()
        {
            var (sut, ctx) = Build(nameof(EditAsync_ThrowsForClosedTicket));
            ctx.Users.Add(MakeUser(1, "user1", roleId: 3));
            ctx.Tickets.Add(MakeTicket(1, 1, statusId: 1));
            await ctx.SaveChangesAsync();
            var comment = await sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "Hi" });

            var ticket = ctx.Tickets.Find(1L)!;
            ticket.StatusId = 4;
            await ctx.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.EditAsync(1, comment.Id, "Updated"));
        }

        [Fact]
        public async Task EditAsync_ReturnsNullForMissingComment()
        {
            var (sut, ctx) = Build(nameof(EditAsync_ReturnsNullForMissingComment));
            ctx.Users.Add(MakeUser(1, "user1"));
            await ctx.SaveChangesAsync();

            var result = await sut.EditAsync(1, 999, "body");

            Assert.Null(result);
        }

        [Fact]
        public async Task EditAsync_ThrowsWhenNonStaffTriesToEditInternalComment()
        {
            var (sut, ctx) = Build(nameof(EditAsync_ThrowsWhenNonStaffTriesToEditInternalComment));
            ctx.Users.AddRange(MakeUser(1, "agent", roleId: 2), MakeUser(2, "employee", roleId: 3));
            ctx.Tickets.Add(MakeTicket(1, 1));
            await ctx.SaveChangesAsync();
            // Agent posts internal comment
            var comment = await sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "Internal", IsInternal = true });

            // Employee tries to edit internal comment
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                sut.EditAsync(2, comment.Id, "Hacked internal"));
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

            Assert.True(await sut.DeleteAsync(1, comment.Id));
        }

        [Fact]
        public async Task DeleteAsync_AllowsAdminToDeleteAnyComment()
        {
            var (sut, ctx) = Build(nameof(DeleteAsync_AllowsAdminToDeleteAnyComment));
            ctx.Users.AddRange(MakeUser(1, "employee", roleId: 3), MakeUser(2, "admin", roleId: 1));
            ctx.Tickets.Add(MakeTicket(1, 1));
            await ctx.SaveChangesAsync();
            var comment = await sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "Hi" });

            Assert.True(await sut.DeleteAsync(2, comment.Id));
        }

        [Fact]
        public async Task DeleteAsync_AllowsAgentToDeleteAnyComment()
        {
            var (sut, ctx) = Build(nameof(DeleteAsync_AllowsAgentToDeleteAnyComment));
            ctx.Users.AddRange(MakeUser(1, "employee", roleId: 3), MakeUser(2, "agent", roleId: 2));
            ctx.Tickets.Add(MakeTicket(1, 1));
            await ctx.SaveChangesAsync();
            var comment = await sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "Hi" });

            Assert.True(await sut.DeleteAsync(2, comment.Id));
        }

        [Fact]
        public async Task DeleteAsync_ReturnsFalseForMissingComment()
        {
            var (sut, ctx) = Build(nameof(DeleteAsync_ReturnsFalseForMissingComment));
            ctx.Users.Add(MakeUser(1, "user1"));
            await ctx.SaveChangesAsync();

            Assert.False(await sut.DeleteAsync(1, 999));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsForClosedTicket()
        {
            var (sut, ctx) = Build(nameof(DeleteAsync_ThrowsForClosedTicket));
            ctx.Users.Add(MakeUser(1, "user1", roleId: 3));
            ctx.Tickets.Add(MakeTicket(1, 1, statusId: 1));
            await ctx.SaveChangesAsync();
            var comment = await sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "Hi" });

            var ticket = ctx.Tickets.Find(1L)!;
            ticket.StatusId = 4;
            await ctx.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.DeleteAsync(1, comment.Id));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsForInactiveUser()
        {
            var (sut, ctx) = Build(nameof(DeleteAsync_ThrowsForInactiveUser));
            ctx.Users.Add(MakeUser(1, "active", roleId: 3));
            ctx.Tickets.Add(MakeTicket(1, 1));
            await ctx.SaveChangesAsync();
            var comment = await sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "Hi" });

            ctx.Users.Add(new User
            {
                Id = 2, UserName = "inactive", Email = "i@t.com", DisplayName = "Inactive",
                IsActive = false, RoleId = 3,
                PasswordHash = Array.Empty<byte>(), PasswordSalt = Array.Empty<byte>()
            });
            await ctx.SaveChangesAsync();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                sut.DeleteAsync(2, comment.Id));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenNonOwnerNonStaffTriesToDelete()
        {
            var (sut, ctx) = Build(nameof(DeleteAsync_ThrowsWhenNonOwnerNonStaffTriesToDelete));
            ctx.Users.AddRange(MakeUser(1, "owner", roleId: 3), MakeUser(2, "other", roleId: 3));
            ctx.Tickets.Add(MakeTicket(1, 1));
            await ctx.SaveChangesAsync();
            var comment = await sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "Hi" });

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                sut.DeleteAsync(2, comment.Id));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenNonStaffTriesToDeleteInternalComment()
        {
            var (sut, ctx) = Build(nameof(DeleteAsync_ThrowsWhenNonStaffTriesToDeleteInternalComment));
            ctx.Users.AddRange(MakeUser(1, "agent", roleId: 2), MakeUser(2, "employee", roleId: 3));
            ctx.Tickets.Add(MakeTicket(1, 1));
            await ctx.SaveChangesAsync();
            var comment = await sut.AddAsync(1, new CommentCreateDto { TicketId = 1, Body = "Internal", IsInternal = true });

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                sut.DeleteAsync(2, comment.Id));
        }
    }
}
