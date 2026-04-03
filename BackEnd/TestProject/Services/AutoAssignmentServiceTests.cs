using TestProject.Helpers;
using TicketWebApp.Models;
using TicketWebApp.Repositories;
using TicketWebApp.Services;
using static TicketWebApp.Models.DTOs.TicketAssignmentDtos;
using static TicketWebApp.Models.DTOs.TicketDtos;

namespace TestProject.Services
{
    public class AutoAssignmentServiceTests
    {
        private static User MakeUser(long id, string name, int roleId = 2, int? deptId = 1) => new User
        {
            Id = id, UserName = name, Email = $"{name}@t.com", DisplayName = name,
            IsActive = true, RoleId = roleId, DepartmentId = deptId,
            PasswordHash = Array.Empty<byte>(), PasswordSalt = Array.Empty<byte>()
        };

        private static (AutoAssignmentService sut, TicketWebApp.Contexts.ComplaintContext ctx, TicketService ticketSvc)
            Build(string db)
        {
            var ctx = DbContextFactory.CreateWithSeed(db);
            var ticketSvc = new TicketService(
                new Repository<long, Ticket>(ctx),
                new Repository<long, TicketAssignment>(ctx),
                new Repository<long, TicketStatusHistory>(ctx),
                new Repository<int, Status>(ctx),
                new Repository<long, User>(ctx));

            var sut = new AutoAssignmentService(
                ticketSvc,
                new Repository<long, Ticket>(ctx),
                new Repository<long, User>(ctx));

            return (sut, ctx, ticketSvc);
        }

        [Fact]
        public async Task AutoAssignAsync_AssignsLeastLoadedAgent()
        {
            var (sut, ctx, ticketSvc) = Build(nameof(AutoAssignAsync_AssignsLeastLoadedAgent));
            ctx.Users.AddRange(
                MakeUser(1, "admin", roleId: 1),
                MakeUser(2, "agent1", roleId: 2),
                MakeUser(3, "agent2", roleId: 2)
            );
            await ctx.SaveChangesAsync();

            var existing = await ticketSvc.CreateAsync(1, new TicketCreateDto { Title = "Existing", PriorityId = 1 });
            await ticketSvc.AssignAsync(existing.Id, 1, new TicketAssignRequestDto { AssignedToUserId = 2 });

            var newTicket = await ticketSvc.CreateAsync(1, new TicketCreateDto { Title = "New", PriorityId = 1 });
            var result = await sut.AutoAssignAsync(newTicket.Id, 1);

            Assert.NotNull(result);
            Assert.Equal(3, result!.AssignedToUserId);
        }

        [Fact]
        public async Task AutoAssignAsync_ReturnsNullWhenNoAgentsAvailable()
        {
            var (sut, ctx, ticketSvc) = Build(nameof(AutoAssignAsync_ReturnsNullWhenNoAgentsAvailable));
            ctx.Users.Add(MakeUser(1, "admin", roleId: 1, deptId: null));
            await ctx.SaveChangesAsync();

            var ticket = await ticketSvc.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1 });
            var result = await sut.AutoAssignAsync(ticket.Id, 1);

            Assert.Null(result);
        }

        [Fact]
        public async Task AutoAssignAsync_ThrowsWhenAssignerNotFound()
        {
            var (sut, ctx, ticketSvc) = Build(nameof(AutoAssignAsync_ThrowsWhenAssignerNotFound));
            ctx.Users.Add(MakeUser(1, "admin", roleId: 1));
            await ctx.SaveChangesAsync();
            var ticket = await ticketSvc.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1 });

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                sut.AutoAssignAsync(ticket.Id, assignedByUserId: 999));
        }

        [Fact]
        public async Task AutoAssignAsync_ReturnsNullForMissingTicket()
        {
            var (sut, ctx, _) = Build(nameof(AutoAssignAsync_ReturnsNullForMissingTicket));
            ctx.Users.Add(MakeUser(1, "admin", roleId: 1));
            await ctx.SaveChangesAsync();

            var result = await sut.AutoAssignAsync(ticketId: 999, assignedByUserId: 1);

            Assert.Null(result);
        }

        [Fact]
        public async Task AutoAssignAsync_FiltersByDepartmentFromRequest()
        {
            var (sut, ctx, ticketSvc) = Build(nameof(AutoAssignAsync_FiltersByDepartmentFromRequest));
            ctx.Departments.Add(new Department { Id = 2, Name = "HR" });
            ctx.Users.AddRange(
                MakeUser(1, "admin", roleId: 1, deptId: 1),
                MakeUser(2, "agent_it", roleId: 2, deptId: 1),
                MakeUser(3, "agent_hr", roleId: 2, deptId: 2)
            );
            await ctx.SaveChangesAsync();

            var ticket = await ticketSvc.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1 });
            var result = await sut.AutoAssignAsync(ticket.Id, 1,
                new TicketAutoAssignRequestDto { DepartmentId = 2 });

            Assert.NotNull(result);
            Assert.Equal(3, result!.AssignedToUserId);
        }

        [Fact]
        public async Task AutoAssignAsync_UsesTicketDepartmentWhenNoRequestDepartment()
        {
            var (sut, ctx, ticketSvc) = Build(nameof(AutoAssignAsync_UsesTicketDepartmentWhenNoRequestDepartment));
            ctx.Users.AddRange(
                MakeUser(1, "admin", roleId: 1, deptId: 1),
                MakeUser(2, "agent_it", roleId: 2, deptId: 1)
            );
            await ctx.SaveChangesAsync();

            var ticket = await ticketSvc.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1, DepartmentId = 1 });
            var result = await sut.AutoAssignAsync(ticket.Id, 1);

            Assert.NotNull(result);
            Assert.Equal(2, result!.AssignedToUserId);
        }

        [Fact]
        public async Task AutoAssignAsync_WithCustomNote_UsesNote()
        {
            var (sut, ctx, ticketSvc) = Build(nameof(AutoAssignAsync_WithCustomNote_UsesNote));
            ctx.Users.AddRange(
                MakeUser(1, "admin", roleId: 1),
                MakeUser(2, "agent", roleId: 2)
            );
            await ctx.SaveChangesAsync();

            var ticket = await ticketSvc.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1 });
            var result = await sut.AutoAssignAsync(ticket.Id, 1,
                new TicketAutoAssignRequestDto { Note = "Custom note" });

            Assert.NotNull(result);
        }
    }
}
