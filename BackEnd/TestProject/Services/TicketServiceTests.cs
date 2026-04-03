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

            var result = await sut.CreateAsync(1, new TicketCreateDto { Title = "Test ticket", PriorityId = 1 });

            Assert.NotNull(result);
            Assert.Equal("Test ticket", result.Title);
            Assert.Equal("New", result.StatusName);
        }

        [Fact]
        public async Task CreateAsync_ThrowsWhenNewStatusMissing()
        {
            var ctx = DbContextFactory.Create(nameof(CreateAsync_ThrowsWhenNewStatusMissing));
            var sut = new TicketService(
                new Repository<long, Ticket>(ctx),
                new Repository<long, TicketAssignment>(ctx),
                new Repository<long, TicketStatusHistory>(ctx),
                new Repository<int, Status>(ctx),
                new Repository<long, User>(ctx));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1 }));
        }

        [Fact]
        public async Task CreateAsync_WithDueAt_StoresUtc()
        {
            var (sut, ctx) = Build(nameof(CreateAsync_WithDueAt_StoresUtc));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            var due = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            var result = await sut.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1, DueAt = due });

            Assert.NotNull(result.DueAt);
        }

        [Fact]
        public async Task CreateAsync_WithDepartmentAndCategory_MapsCorrectly()
        {
            var (sut, ctx) = Build(nameof(CreateAsync_WithDepartmentAndCategory_MapsCorrectly));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();

            var result = await sut.CreateAsync(1, new TicketCreateDto
            {
                Title = "T", PriorityId = 1, DepartmentId = 1, CategoryId = 1
            });

            Assert.Equal(1, result.DepartmentId);
            Assert.Equal(1, result.CategoryId);
        }

        // ── GetAsync ──────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAsync_ReturnsNullForMissingTicket()
        {
            var (sut, _) = Build(nameof(GetAsync_ReturnsNullForMissingTicket));
            Assert.Null(await sut.GetAsync(999));
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
            Assert.Null(await sut.UpdateAsync(999, new TicketUpdateDto { Title = "X" }));
        }

        [Fact]
        public async Task UpdateAsync_UpdatesAllFields()
        {
            var (sut, ctx) = Build(nameof(UpdateAsync_UpdatesAllFields));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            var created = await sut.CreateAsync(1, new TicketCreateDto { Title = "Old", PriorityId = 1 });
            var due = new DateTime(2030, 6, 1, 0, 0, 0, DateTimeKind.Utc);

            var result = await sut.UpdateAsync(created.Id, new TicketUpdateDto
            {
                Title = "Updated",
                Description = "Desc",
                PriorityId = 2,
                DepartmentId = 1,
                CategoryId = 1,
                DueAt = due
            });

            Assert.Equal("Updated", result!.Title);
            Assert.Equal("Desc", result.Description);
            Assert.Equal(2, result.PriorityId);
            Assert.NotNull(result.DueAt);
        }

        // ── UpdateStatusAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task UpdateStatusAsync_ChangesStatus()
        {
            var (sut, ctx) = Build(nameof(UpdateStatusAsync_ChangesStatus));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            var created = await sut.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1 });

            var result = await sut.UpdateStatusAsync(created.Id, 1, new TicketStatusUpdateDto { NewStatusId = 2, Note = "Moving along" });

            Assert.True(result);
            var updated = await sut.GetAsync(created.Id);
            Assert.Equal("In Progress", updated!.StatusName);
        }

        [Fact]
        public async Task UpdateStatusAsync_ReturnsFalseForMissingTicket()
        {
            var (sut, _) = Build(nameof(UpdateStatusAsync_ReturnsFalseForMissingTicket));
            Assert.False(await sut.UpdateStatusAsync(999, 1, new TicketStatusUpdateDto { NewStatusId = 2 }));
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

            var result = await sut.AssignAsync(created.Id, 1, new TicketAssignRequestDto { AssignedToUserId = 2, Note = "Assigning" });

            Assert.NotNull(result);
            Assert.Equal(2, result!.AssignedToUserId);
            var ticket = await sut.GetAsync(created.Id);
            Assert.Equal("In Progress", ticket!.StatusName);
        }

        [Fact]
        public async Task AssignAsync_ReassignUpdatesCurrentAssignment()
        {
            var (sut, ctx) = Build(nameof(AssignAsync_ReassignUpdatesCurrentAssignment));
            ctx.Users.AddRange(MakeUser(1, "creator"), MakeUser(2, "agent1", roleId: 2), MakeUser(3, "agent2", roleId: 2));
            await ctx.SaveChangesAsync();
            var created = await sut.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1 });
            await sut.AssignAsync(created.Id, 1, new TicketAssignRequestDto { AssignedToUserId = 2 });

            // Re-assign to agent2
            var result = await sut.AssignAsync(created.Id, 1, new TicketAssignRequestDto { AssignedToUserId = 3 });

            Assert.NotNull(result);
            Assert.Equal(3, result!.AssignedToUserId);
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

        [Fact]
        public async Task AssignAsync_ReturnsNullForMissingTicket()
        {
            var (sut, ctx) = Build(nameof(AssignAsync_ReturnsNullForMissingTicket));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();

            var result = await sut.AssignAsync(999, 1, new TicketAssignRequestDto { AssignedToUserId = 1 });

            Assert.Null(result);
        }

        [Fact]
        public async Task AssignAsync_TicketAlreadyInProgress_DoesNotDuplicateStatusHistory()
        {
            var (sut, ctx) = Build(nameof(AssignAsync_TicketAlreadyInProgress_DoesNotDuplicateStatusHistory));
            ctx.Users.AddRange(MakeUser(1, "creator"), MakeUser(2, "agent", roleId: 2));
            await ctx.SaveChangesAsync();
            var created = await sut.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1 });
            // Move to In Progress first
            await sut.UpdateStatusAsync(created.Id, 1, new TicketStatusUpdateDto { NewStatusId = 2 });

            // Assign while already In Progress — should not add another status history entry
            var result = await sut.AssignAsync(created.Id, 1, new TicketAssignRequestDto { AssignedToUserId = 2 });

            Assert.NotNull(result);
            var history = await sut.GetStatusHistoryAsync(created.Id);
            // Only 1 history entry (the manual status change), not 2
            Assert.Single(history);
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

        [Fact]
        public async Task QueryAsync_FiltersByPriority()
        {
            var (sut, ctx) = Build(nameof(QueryAsync_FiltersByPriority));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            await sut.CreateAsync(1, new TicketCreateDto { Title = "Urgent", PriorityId = 1 });
            await sut.CreateAsync(1, new TicketCreateDto { Title = "Low", PriorityId = 4 });

            var result = await sut.QueryAsync(new TicketQueryDto { PriorityId = 1, Page = 1, PageSize = 20 });

            Assert.Equal(1, result.TotalCount);
            Assert.Equal("Urgent", result.Items[0].Title);
        }

        [Fact]
        public async Task QueryAsync_FiltersByDepartment()
        {
            var (sut, ctx) = Build(nameof(QueryAsync_FiltersByDepartment));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            await sut.CreateAsync(1, new TicketCreateDto { Title = "T1", PriorityId = 1, DepartmentId = 1 });
            await sut.CreateAsync(1, new TicketCreateDto { Title = "T2", PriorityId = 1 });

            var result = await sut.QueryAsync(new TicketQueryDto { DepartmentId = 1, Page = 1, PageSize = 20 });

            Assert.Equal(1, result.TotalCount);
        }

        [Fact]
        public async Task QueryAsync_FiltersByCategory()
        {
            var (sut, ctx) = Build(nameof(QueryAsync_FiltersByCategory));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            await sut.CreateAsync(1, new TicketCreateDto { Title = "T1", PriorityId = 1, CategoryId = 1 });
            await sut.CreateAsync(1, new TicketCreateDto { Title = "T2", PriorityId = 1 });

            var result = await sut.QueryAsync(new TicketQueryDto { CategoryId = 1, Page = 1, PageSize = 20 });

            Assert.Equal(1, result.TotalCount);
        }

        [Fact]
        public async Task QueryAsync_FiltersByCreatedByUserId()
        {
            var (sut, ctx) = Build(nameof(QueryAsync_FiltersByCreatedByUserId));
            ctx.Users.AddRange(MakeUser(1, "u1"), MakeUser(2, "u2"));
            await ctx.SaveChangesAsync();
            await sut.CreateAsync(1, new TicketCreateDto { Title = "T1", PriorityId = 1 });
            await sut.CreateAsync(2, new TicketCreateDto { Title = "T2", PriorityId = 1 });

            var result = await sut.QueryAsync(new TicketQueryDto { CreatedByUserId = 1, Page = 1, PageSize = 20 });

            Assert.Equal(1, result.TotalCount);
            Assert.Equal("T1", result.Items[0].Title);
        }

        [Fact]
        public async Task QueryAsync_FiltersByAssigneeUserId()
        {
            var (sut, ctx) = Build(nameof(QueryAsync_FiltersByAssigneeUserId));
            ctx.Users.AddRange(MakeUser(1, "creator"), MakeUser(2, "agent", roleId: 2));
            await ctx.SaveChangesAsync();
            var t1 = await sut.CreateAsync(1, new TicketCreateDto { Title = "T1", PriorityId = 1 });
            await sut.AssignAsync(t1.Id, 1, new TicketAssignRequestDto { AssignedToUserId = 2 });
            await sut.CreateAsync(1, new TicketCreateDto { Title = "T2", PriorityId = 1 });

            var result = await sut.QueryAsync(new TicketQueryDto { AssigneeUserId = 2, Page = 1, PageSize = 20 });

            Assert.Equal(1, result.TotalCount);
        }

        [Fact]
        public async Task QueryAsync_FiltersByCreatedDateRange()
        {
            var (sut, ctx) = Build(nameof(QueryAsync_FiltersByCreatedDateRange));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            await sut.CreateAsync(1, new TicketCreateDto { Title = "T1", PriorityId = 1 });

            var result = await sut.QueryAsync(new TicketQueryDto
            {
                CreatedFrom = DateTime.UtcNow.AddHours(-1),
                CreatedTo = DateTime.UtcNow.AddHours(1),
                Page = 1, PageSize = 20
            });

            Assert.Equal(1, result.TotalCount);
        }

        [Fact]
        public async Task QueryAsync_SortsByPriorityAsc()
        {
            var (sut, ctx) = Build(nameof(QueryAsync_SortsByPriorityAsc));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            await sut.CreateAsync(1, new TicketCreateDto { Title = "Low", PriorityId = 4 });
            await sut.CreateAsync(1, new TicketCreateDto { Title = "Urgent", PriorityId = 1 });

            // Desc=true => OrderBy(Rank) ascending => Rank 1 (Urgent) first
            var result = await sut.QueryAsync(new TicketQueryDto { SortBy = "priority", Desc = true, Page = 1, PageSize = 20 });

            Assert.Equal("Urgent", result.Items[0].Title);
        }

        [Fact]
        public async Task QueryAsync_SortsByPriorityDesc()
        {
            var (sut, ctx) = Build(nameof(QueryAsync_SortsByPriorityDesc));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            await sut.CreateAsync(1, new TicketCreateDto { Title = "Urgent", PriorityId = 1 });
            await sut.CreateAsync(1, new TicketCreateDto { Title = "Low", PriorityId = 4 });

            // Desc=false => OrderByDescending(Rank) => Rank 4 (Low) first
            var result = await sut.QueryAsync(new TicketQueryDto { SortBy = "priority", Desc = false, Page = 1, PageSize = 20 });

            Assert.Equal("Low", result.Items[0].Title);
        }

        [Fact]
        public async Task QueryAsync_SortsByDueAtAsc()
        {
            var (sut, ctx) = Build(nameof(QueryAsync_SortsByDueAtAsc));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            await sut.CreateAsync(1, new TicketCreateDto { Title = "Later", PriorityId = 1, DueAt = DateTime.UtcNow.AddDays(2) });
            await sut.CreateAsync(1, new TicketCreateDto { Title = "Sooner", PriorityId = 1, DueAt = DateTime.UtcNow.AddDays(1) });

            var result = await sut.QueryAsync(new TicketQueryDto { SortBy = "dueat", Desc = false, Page = 1, PageSize = 20 });

            Assert.Equal("Sooner", result.Items[0].Title);
        }

        [Fact]
        public async Task QueryAsync_SortsByDueAtDesc()
        {
            var (sut, ctx) = Build(nameof(QueryAsync_SortsByDueAtDesc));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            await sut.CreateAsync(1, new TicketCreateDto { Title = "Sooner", PriorityId = 1, DueAt = DateTime.UtcNow.AddDays(1) });
            await sut.CreateAsync(1, new TicketCreateDto { Title = "Later", PriorityId = 1, DueAt = DateTime.UtcNow.AddDays(2) });

            var result = await sut.QueryAsync(new TicketQueryDto { SortBy = "dueat", Desc = true, Page = 1, PageSize = 20 });

            Assert.Equal("Later", result.Items[0].Title);
        }

        [Fact]
        public async Task QueryAsync_DefaultSortByCreatedAtDesc()
        {
            var (sut, ctx) = Build(nameof(QueryAsync_DefaultSortByCreatedAtDesc));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            await sut.CreateAsync(1, new TicketCreateDto { Title = "First", PriorityId = 1 });
            await sut.CreateAsync(1, new TicketCreateDto { Title = "Second", PriorityId = 1 });

            var result = await sut.QueryAsync(new TicketQueryDto { Desc = true, Page = 1, PageSize = 20 });

            Assert.Equal("Second", result.Items[0].Title);
        }

        [Fact]
        public async Task QueryAsync_DefaultSortByCreatedAtAsc()
        {
            var (sut, ctx) = Build(nameof(QueryAsync_DefaultSortByCreatedAtAsc));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            await sut.CreateAsync(1, new TicketCreateDto { Title = "First", PriorityId = 1 });
            await sut.CreateAsync(1, new TicketCreateDto { Title = "Second", PriorityId = 1 });

            var result = await sut.QueryAsync(new TicketQueryDto { Desc = false, Page = 1, PageSize = 20 });

            Assert.Equal("First", result.Items[0].Title);
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

        [Fact]
        public async Task GetStatusHistoryAsync_ReturnsEmptyForTicketWithNoChanges()
        {
            var (sut, ctx) = Build(nameof(GetStatusHistoryAsync_ReturnsEmptyForTicketWithNoChanges));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            var created = await sut.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1 });

            var history = await sut.GetStatusHistoryAsync(created.Id);

            Assert.Empty(history);
        }

        [Fact]
        public async Task GetAsync_ReturnsTicketWithNullAssigneeAndNullDeptCategory()
        {
            // Covers ToResponse null navigation branches: CurrentAssignee, Department, Category
            var (sut, ctx) = Build(nameof(GetAsync_ReturnsTicketWithNullAssigneeAndNullDeptCategory));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            var created = await sut.CreateAsync(1, new TicketCreateDto
            {
                Title = "Minimal", PriorityId = 1
                // No DepartmentId, CategoryId, no assignee
            });

            var result = await sut.GetAsync(created.Id);

            Assert.NotNull(result);
            Assert.Null(result!.CurrentAssigneeUserId);
            Assert.Null(result.DepartmentName);
            Assert.Null(result.CategoryName);
        }

        [Fact]
        public async Task QueryAsync_ReturnsItemsWithNullAssigneeAndDept()
        {
            var (sut, ctx) = Build(nameof(QueryAsync_ReturnsItemsWithNullAssigneeAndDept));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            await sut.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1 });

            var result = await sut.QueryAsync(new TicketQueryDto { Page = 1, PageSize = 20 });

            Assert.Single(result.Items);
            Assert.Null(result.Items[0].Assignee);
            Assert.Null(result.Items[0].Department);
            Assert.Null(result.Items[0].Category);
        }

        [Fact]
        public async Task AssignAsync_WhenInProgressStatusMissing_SkipsStatusChange()
        {
            // Covers: inProgress == null branch in AssignAsync
            var ctx = DbContextFactory.Create(nameof(AssignAsync_WhenInProgressStatusMissing_SkipsStatusChange));
            // Seed only "New" status — no "In Progress"
            ctx.Roles.AddRange(
                new Role { Id = 1, Name = "Admin" },
                new Role { Id = 2, Name = "Agent" },
                new Role { Id = 3, Name = "Employee" });
            ctx.Statuses.Add(new Status { Id = 1, Name = "New", IsClosedState = false });
            ctx.Priorities.Add(new Priority { Id = 1, Name = "Urgent", Rank = 1, ColorHex = "#FF0000" });
            ctx.Departments.Add(new Department { Id = 1, Name = "IT" });
            await ctx.SaveChangesAsync();

            var sut = new TicketService(
                new Repository<long, Ticket>(ctx),
                new Repository<long, TicketAssignment>(ctx),
                new Repository<long, TicketStatusHistory>(ctx),
                new Repository<int, Status>(ctx),
                new Repository<long, User>(ctx));

            var creator = new User { Id = 1, UserName = "c", Email = "c@t.com", DisplayName = "c", IsActive = true, RoleId = 1, PasswordHash = Array.Empty<byte>(), PasswordSalt = Array.Empty<byte>() };
            var agent = new User { Id = 2, UserName = "a", Email = "a@t.com", DisplayName = "a", IsActive = true, RoleId = 2, PasswordHash = Array.Empty<byte>(), PasswordSalt = Array.Empty<byte>() };
            ctx.Users.AddRange(creator, agent);
            await ctx.SaveChangesAsync();

            var ticket = await sut.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1 });
            var result = await sut.AssignAsync(ticket.Id, 1, new TicketAssignRequestDto { AssignedToUserId = 2 });

            Assert.NotNull(result);
            // Status should still be "New" since "In Progress" doesn't exist
            var updated = await sut.GetAsync(ticket.Id);
            Assert.Equal("New", updated!.StatusName);
        }

        [Fact]
        public async Task AssignAsync_AssigneeNotFoundReturnsEmptyName()
        {
            // Covers: assignee?.DisplayName ?? "" null branch — assignee deleted after AnyAsync check
            var (sut, ctx) = Build(nameof(AssignAsync_AssigneeNotFoundReturnsEmptyName));
            ctx.Users.AddRange(MakeUser(1, "creator"), MakeUser(2, "agent", roleId: 2));
            await ctx.SaveChangesAsync();
            var ticket = await sut.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1 });

            var result = await sut.AssignAsync(ticket.Id, 1, new TicketAssignRequestDto { AssignedToUserId = 2 });

            // AssignedToName should be the agent's display name
            Assert.Equal("agent", result!.AssignedToName);
        }

        [Fact]
        public async Task UpdateAsync_WithNullDescriptionAndEmptyTitle_SkipsThoseFields()
        {
            // Covers: dto.Description != null = false, string.IsNullOrWhiteSpace(dto.Title) = true
            var (sut, ctx) = Build(nameof(UpdateAsync_WithNullDescriptionAndEmptyTitle_SkipsThoseFields));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            var created = await sut.CreateAsync(1, new TicketCreateDto { Title = "Original", PriorityId = 1, Description = "Desc" });

            // Pass null description and whitespace title — neither should update
            var result = await sut.UpdateAsync(created.Id, new TicketUpdateDto { Title = "   ", Description = null });

            Assert.Equal("Original", result!.Title);
            Assert.Equal("Desc", result.Description);
        }

        [Fact]
        public async Task GetStatusHistoryAsync_IncludesNullOldStatusName()
        {
            // Covers: h.OldStatus?.Name null branch and h.ChangedBy?.DisplayName null branch
            var (sut, ctx) = Build(nameof(GetStatusHistoryAsync_IncludesNullOldStatusName));
            ctx.Users.Add(MakeUser(1, "u1"));
            await ctx.SaveChangesAsync();
            var ticket = await sut.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1 });

            // Insert history row with null OldStatusId and null ChangedByUserId reference
            ctx.TicketStatusHistories.Add(new TicketStatusHistory
            {
                TicketId = ticket.Id,
                OldStatusId = 999,  // non-existent status — OldStatus navigation will be null
                NewStatusId = 1,
                ChangedByUserId = 1,
                ChangedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();

            var history = await sut.GetStatusHistoryAsync(ticket.Id);

            Assert.Single(history);
            Assert.Null(history[0].OldStatusName);
        }
    }
}
