using TestProject.Helpers;
using TicketWebApp.Models;
using TicketWebApp.Models.DTOs;
using TicketWebApp.Repositories;
using TicketWebApp.Services;
using static TicketWebApp.Models.DTOs.TicketAssignmentDtos;
using static TicketWebApp.Models.DTOs.TicketDtos;

namespace TestProject.Services
{
    public class TicketServiceTests
    {
        private static (TicketService sut, TicketWebApp.Contexts.ComplaintContext ctx) Build(string db)
        {
            var ctx = DbContextFactory.CreateWithSeed(db);
            var sut = new TicketService(
                new Repository<long, Ticket>(ctx),
                new Repository<long, TicketAssignment>(ctx),
                new Repository<long, TicketStatusHistory>(ctx),
                new Repository<int, Status>(ctx),
                new Repository<long, User>(ctx));
            return (sut, ctx);
        }

        private static User MakeUser(long id, string name, int roleId = 3) => new User
        {
            Id = id, UserName = name, Email = $"{name}@t.com", DisplayName = name,
            IsActive = true, RoleId = roleId,
            PasswordHash = Array.Empty<byte>(), PasswordSalt = Array.Empty<byte>()
        };

        // ── CreateAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task CreateAsync_CreatesTicketWithNewStatus()
        {
            var (sut, ctx) = Build(nameof(CreateAsync_CreatesTicketWithNewStatus));
            ctx.Users.Add(MakeUser(1, "creator"));
            await ctx.SaveChangesAsync();

            var result = await sut.CreateAsync(1, new TicketCreateDto
            {
                Title = "Test ticket",
                PriorityId = 1
            });

            Assert.NotNull(result);
            Assert.Equal("Test ticket", result.Title);
            Assert.Equal("New", result.StatusName);
        }

        [Fact]
        public async Task CreateAsync_ThrowsWhenNewStatusMissing()
        {
            var ctx = DbContextFactory.Create(nameof(CreateAsync_ThrowsWhenNewStatusMissing));
            // No statuses seeded
            var sut = new TicketService(
                new Repository<long, Ticket>(ctx),
                new Repository<long, TicketAssignment>(ctx),
                new Repository<long, TicketStatusHistory>(ctx),
                new Repository<int, Status>(ctx),
                new Repository<long, User>(ctx));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1 }));
        }

        // ── GetAsync ──────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAsync_ReturnsNullForMissingTicket()
        {
            var (sut, _) = Build(nameof(GetAsync_ReturnsNullForMissingTicket));
            var result = await sut.GetAsync(999);
            Assert.Null(result);
        }

        [Fact]
        public async Task GetAsync_ReturnsTicketById()
        {
            var (sut, ctx) = Build(nameof(GetAsync_ReturnsTicketById));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            var created = await sut.CreateAsync(1, new TicketCreateDto { Title = "My Ticket", PriorityId = 2 });

            var result = await sut.GetAsync(created.Id);

            Assert.NotNull(result);
            Assert.Equal("My Ticket", result!.Title);
        }

        // ── UpdateAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateAsync_UpdatesTitle()
        {
            var (sut, ctx) = Build(nameof(UpdateAsync_UpdatesTitle));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            var created = await sut.CreateAsync(1, new TicketCreateDto { Title = "Old", PriorityId = 1 });

            var result = await sut.UpdateAsync(created.Id, new TicketUpdateDto { Title = "New Title" });

            Assert.Equal("New Title", result!.Title);
        }

        [Fact]
        public async Task UpdateAsync_ReturnsNullForMissingTicket()
        {
            var (sut, _) = Build(nameof(UpdateAsync_ReturnsNullForMissingTicket));
            var result = await sut.UpdateAsync(999, new TicketUpdateDto { Title = "X" });
            Assert.Null(result);
        }

        // ── UpdateStatusAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task UpdateStatusAsync_ChangesStatus()
        {
            var (sut, ctx) = Build(nameof(UpdateStatusAsync_ChangesStatus));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            var created = await sut.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1 });

            var result = await sut.UpdateStatusAsync(created.Id, 1, new TicketStatusUpdateDto
            {
                NewStatusId = 2, // In Progress
                Note = "Moving along"
            });

            Assert.True(result);
            var updated = await sut.GetAsync(created.Id);
            Assert.Equal("In Progress", updated!.StatusName);
        }

        [Fact]
        public async Task UpdateStatusAsync_ReturnsFalseForMissingTicket()
        {
            var (sut, _) = Build(nameof(UpdateStatusAsync_ReturnsFalseForMissingTicket));
            var result = await sut.UpdateStatusAsync(999, 1, new TicketStatusUpdateDto { NewStatusId = 2 });
            Assert.False(result);
        }

        [Fact]
        public async Task UpdateStatusAsync_ThrowsForInvalidStatus()
        {
            var (sut, ctx) = Build(nameof(UpdateStatusAsync_ThrowsForInvalidStatus));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            var created = await sut.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1 });

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                sut.UpdateStatusAsync(created.Id, 1, new TicketStatusUpdateDto { NewStatusId = 999 }));
        }

        // ── AssignAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task AssignAsync_AssignsAgentAndMovesToInProgress()
        {
            var (sut, ctx) = Build(nameof(AssignAsync_AssignsAgentAndMovesToInProgress));
            ctx.Users.AddRange(MakeUser(1, "creator"), MakeUser(2, "agent", roleId: 2));
            await ctx.SaveChangesAsync();
            var created = await sut.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1 });

            var result = await sut.AssignAsync(created.Id, 1, new TicketAssignRequestDto
            {
                AssignedToUserId = 2,
                Note = "Assigning"
            });

            Assert.NotNull(result);
            Assert.Equal(2, result!.AssignedToUserId);

            var ticket = await sut.GetAsync(created.Id);
            Assert.Equal("In Progress", ticket!.StatusName);
        }

        [Fact]
        public async Task AssignAsync_ThrowsForUnknownAssignee()
        {
            var (sut, ctx) = Build(nameof(AssignAsync_ThrowsForUnknownAssignee));
            ctx.Users.Add(MakeUser(1, "creator"));
            await ctx.SaveChangesAsync();
            var created = await sut.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1 });

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                sut.AssignAsync(created.Id, 1, new TicketAssignRequestDto { AssignedToUserId = 999 }));
        }

        // ── QueryAsync ────────────────────────────────────────────────────────

        [Fact]
        public async Task QueryAsync_ReturnsPagedResults()
        {
            var (sut, ctx) = Build(nameof(QueryAsync_ReturnsPagedResults));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            await sut.CreateAsync(1, new TicketCreateDto { Title = "T1", PriorityId = 1 });
            await sut.CreateAsync(1, new TicketCreateDto { Title = "T2", PriorityId = 2 });
            await sut.CreateAsync(1, new TicketCreateDto { Title = "T3", PriorityId = 3 });

            var result = await sut.QueryAsync(new TicketQueryDto { Page = 1, PageSize = 2 });

            Assert.Equal(3, result.TotalCount);
            Assert.Equal(2, result.Items.Count);
        }

        [Fact]
        public async Task QueryAsync_FiltersByStatus()
        {
            var (sut, ctx) = Build(nameof(QueryAsync_FiltersByStatus));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            var t1 = await sut.CreateAsync(1, new TicketCreateDto { Title = "T1", PriorityId = 1 });
            await sut.UpdateStatusAsync(t1.Id, 1, new TicketStatusUpdateDto { NewStatusId = 2 });
            await sut.CreateAsync(1, new TicketCreateDto { Title = "T2", PriorityId = 1 });

            var result = await sut.QueryAsync(new TicketQueryDto { StatusId = 1, Page = 1, PageSize = 20 });

            Assert.Equal(1, result.TotalCount);
            Assert.Equal("T2", result.Items[0].Title);
        }

        // ── GetStatusHistoryAsync ─────────────────────────────────────────────

        [Fact]
        public async Task GetStatusHistoryAsync_ReturnsHistoryEntries()
        {
            var (sut, ctx) = Build(nameof(GetStatusHistoryAsync_ReturnsHistoryEntries));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            var created = await sut.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1 });
            await sut.UpdateStatusAsync(created.Id, 1, new TicketStatusUpdateDto { NewStatusId = 2 });

            var history = await sut.GetStatusHistoryAsync(created.Id);

            Assert.Single(history);
            Assert.Equal(2, history[0].NewStatusId);
        }
    }
}
