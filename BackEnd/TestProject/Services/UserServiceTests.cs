using Moq;
using TicketWebApp.Interfaces;
using TicketWebApp.Models;
using TicketWebApp.Models.DTOs;
using TicketWebApp.Services;
using TestProject.Helpers;
using TicketWebApp.Repositories;

namespace TestProject.Services
{
    public class UserServiceTests
    {
        private static (UserService sut, TicketWebApp.Contexts.ComplaintContext ctx) Build(string db)
        {
            var ctx = DbContextFactory.CreateWithSeed(db);
            var repo = new Repository<long, User>(ctx);
            return (new UserService(repo), ctx);
        }

        private static User MakeUser(long id, string name, int roleId = 2, int? deptId = 1) => new User
        {
            Id = id,
            UserName = name,
            Email = $"{name}@test.com",
            DisplayName = name,
            IsActive = true,
            RoleId = roleId,
            DepartmentId = deptId,
            PasswordHash = Array.Empty<byte>(),
            PasswordSalt = Array.Empty<byte>()
        };

        [Fact]
        public async Task GetAsync_ReturnsUserDto()
        {
            var (sut, ctx) = Build(nameof(GetAsync_ReturnsUserDto));
            ctx.Users.Add(MakeUser(1, "alice"));
            await ctx.SaveChangesAsync();

            var result = await sut.GetAsync(1);

            Assert.NotNull(result);
            Assert.Equal("alice", result!.UserName);
        }

        [Fact]
        public async Task GetAsync_ThrowsWhenUserNotFound()
        {
            var (sut, _) = Build(nameof(GetAsync_ThrowsWhenUserNotFound));

            await Assert.ThrowsAsync<Exception>(() => sut.GetAsync(999));
        }

        [Fact]
        public async Task GetAgentsAsync_ReturnsOnlyActiveAgents()
        {
            var (sut, ctx) = Build(nameof(GetAgentsAsync_ReturnsOnlyActiveAgents));
            ctx.Users.AddRange(
                MakeUser(1, "agent1", roleId: 2),
                MakeUser(2, "agent2", roleId: 2),
                MakeUser(3, "employee1", roleId: 3)
            );
            await ctx.SaveChangesAsync();

            var result = await sut.GetAgentsAsync();

            Assert.Equal(2, result.Count);
            Assert.All(result, u => Assert.Equal("Agent", u.RoleName));
        }

        [Fact]
        public async Task GetAgentsAsync_FiltersByDepartment()
        {
            var (sut, ctx) = Build(nameof(GetAgentsAsync_FiltersByDepartment));
            ctx.Departments.Add(new Department { Id = 2, Name = "HR" });
            ctx.Users.AddRange(
                MakeUser(1, "agent_it", roleId: 2, deptId: 1),
                MakeUser(2, "agent_hr", roleId: 2, deptId: 2)
            );
            await ctx.SaveChangesAsync();

            var result = await sut.GetAgentsAsync(departmentId: 1);

            Assert.Single(result);
            Assert.Equal("agent_it", result[0].UserName);
        }

        [Fact]
        public async Task GetAgentsAsync_ThrowsWhenNoAgentsFound()
        {
            var (sut, _) = Build(nameof(GetAgentsAsync_ThrowsWhenNoAgentsFound));

            await Assert.ThrowsAsync<Exception>(() => sut.GetAgentsAsync());
        }

        [Fact]
        public async Task UpdateProfileAsync_UpdatesDisplayName()
        {
            var (sut, ctx) = Build(nameof(UpdateProfileAsync_UpdatesDisplayName));
            ctx.Users.Add(MakeUser(1, "bob"));
            await ctx.SaveChangesAsync();

            var result = await sut.UpdateProfileAsync(1, new UpdateProfileDto { DisplayName = "Bobby" });

            Assert.Equal("Bobby", result!.DisplayName);
        }

        [Fact]
        public async Task UpdateProfileAsync_ThrowsWhenUserNotFound()
        {
            var (sut, _) = Build(nameof(UpdateProfileAsync_ThrowsWhenUserNotFound));

            await Assert.ThrowsAsync<Exception>(() =>
                sut.UpdateProfileAsync(999, new UpdateProfileDto { DisplayName = "X" }));
        }
    }
}
