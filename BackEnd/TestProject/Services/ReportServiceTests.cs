using TestProject.Helpers;
using TicketWebApp.Models;
using TicketWebApp.Repositories;
using TicketWebApp.Services;

namespace TestProject.Services
{
    public class ReportServiceTests
    {
        private static (ReportService sut, TicketWebApp.Contexts.ComplaintContext ctx) Build(string db)
        {
            var ctx = DbContextFactory.CreateWithSeed(db);
            var sut = new ReportService(
                new Repository<long, Ticket>(ctx),
                new Repository<int, Status>(ctx),
                new Repository<int, Priority>(ctx));
            return (sut, ctx);
        }

        private static User MakeUser(long id, string name, int roleId = 3) => new User
        {
            Id = id, UserName = name, Email = $"{name}@t.com", DisplayName = name,
            IsActive = true, RoleId = roleId,
            PasswordHash = Array.Empty<byte>(), PasswordSalt = Array.Empty<byte>()
        };

        private static Ticket MakeTicket(long id, long userId, int statusId, int priorityId,
            long? assigneeId = null, DateTime? dueAt = null) => new Ticket
        {
            Id = id, Title = $"T{id}", PriorityId = priorityId, StatusId = statusId,
            CreatedByUserId = userId, CurrentAssigneeUserId = assigneeId,
            DueAt = dueAt, CreatedAt = DateTime.UtcNow
        };

        [Fact]
        public async Task GetTicketSummaryAsync_AdminSeesAllTickets()
        {
            var (sut, ctx) = Build(nameof(GetTicketSummaryAsync_AdminSeesAllTickets));
            ctx.Users.AddRange(MakeUser(1, "admin", 1), MakeUser(2, "emp", 3));
            ctx.Tickets.AddRange(
                MakeTicket(1, 1, statusId: 1, priorityId: 1), // New / Urgent
                MakeTicket(2, 2, statusId: 1, priorityId: 2)  // New / High
            );
            await ctx.SaveChangesAsync();

            var result = await sut.GetTicketSummaryAsync(1, isAdmin: true, isAgent: false, isEmployee: false);

            Assert.Equal(2, result.Total);
            Assert.Equal(2, result.Open);
        }

        [Fact]
        public async Task GetTicketSummaryAsync_EmployeeSeesOnlyOwnTickets()
        {
            var (sut, ctx) = Build(nameof(GetTicketSummaryAsync_EmployeeSeesOnlyOwnTickets));
            ctx.Users.AddRange(MakeUser(1, "emp1", 3), MakeUser(2, "emp2", 3));
            ctx.Tickets.AddRange(
                MakeTicket(1, 1, statusId: 1, priorityId: 1),
                MakeTicket(2, 2, statusId: 1, priorityId: 1)
            );
            await ctx.SaveChangesAsync();

            var result = await sut.GetTicketSummaryAsync(1, isAdmin: false, isAgent: false, isEmployee: true);

            Assert.Equal(1, result.Total);
        }

        [Fact]
        public async Task GetTicketSummaryAsync_CountsOverdueCorrectly()
        {
            var (sut, ctx) = Build(nameof(GetTicketSummaryAsync_CountsOverdueCorrectly));
            ctx.Users.Add(MakeUser(1, "admin", 1));
            ctx.Tickets.AddRange(
                MakeTicket(1, 1, statusId: 1, priorityId: 1, dueAt: DateTime.UtcNow.AddDays(-1)), // overdue
                MakeTicket(2, 1, statusId: 3, priorityId: 1, dueAt: DateTime.UtcNow.AddDays(-1))  // resolved - not overdue
            );
            await ctx.SaveChangesAsync();

            var result = await sut.GetTicketSummaryAsync(1, isAdmin: true, isAgent: false, isEmployee: false);

            Assert.Equal(1, result.Overdue);
        }

        [Fact]
        public async Task GetTicketSummaryAsync_CountsAssignedToMe()
        {
            var (sut, ctx) = Build(nameof(GetTicketSummaryAsync_CountsAssignedToMe));
            ctx.Users.Add(MakeUser(1, "agent", 2));
            ctx.Tickets.AddRange(
                MakeTicket(1, 1, statusId: 2, priorityId: 1, assigneeId: 1),
                MakeTicket(2, 1, statusId: 2, priorityId: 1, assigneeId: null)
            );
            await ctx.SaveChangesAsync();

            var result = await sut.GetTicketSummaryAsync(1, isAdmin: false, isAgent: true, isEmployee: false);

            Assert.Equal(1, result.AssignedToMe);
        }

        [Fact]
        public async Task GetTicketSummaryAsync_ThrowsWhenNoStatusesConfigured()
        {
            var ctx = DbContextFactory.Create(nameof(GetTicketSummaryAsync_ThrowsWhenNoStatusesConfigured));
            var sut = new ReportService(
                new Repository<long, Ticket>(ctx),
                new Repository<int, Status>(ctx),
                new Repository<int, Priority>(ctx));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.GetTicketSummaryAsync(1, true, false, false));
        }

        [Fact]
        public async Task GetTicketSummaryAsync_BreaksDownByPriority()
        {
            var (sut, ctx) = Build(nameof(GetTicketSummaryAsync_BreaksDownByPriority));
            ctx.Users.Add(MakeUser(1, "admin", 1));
            ctx.Tickets.AddRange(
                MakeTicket(1, 1, statusId: 1, priorityId: 1), // Urgent
                MakeTicket(2, 1, statusId: 1, priorityId: 2), // High
                MakeTicket(3, 1, statusId: 1, priorityId: 3), // Medium
                MakeTicket(4, 1, statusId: 1, priorityId: 4)  // Low
            );
            await ctx.SaveChangesAsync();

            var result = await sut.GetTicketSummaryAsync(1, isAdmin: true, isAgent: false, isEmployee: false);

            Assert.Equal(1, result.UrgentPriority);
            Assert.Equal(1, result.HighPriority);
            Assert.Equal(1, result.MediumPriority);
            Assert.Equal(1, result.LowPriority);
        }
    }
}
