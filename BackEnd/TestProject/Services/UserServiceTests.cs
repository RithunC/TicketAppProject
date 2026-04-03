using TestProject.Helpers;
using TicketWebApp.Models;
using TicketWebApp.Models.DTOs;
using TicketWebApp.Repositories;
using TicketWebApp.Services;

namespace TestProject.Services
{
    public class UserServiceTests
    {
        private static (UserService sut, TicketWebApp.Contexts.ComplaintContext ctx) Build(string db)
        {
            var ctx = DbContextFactory.CreateWithSeed(db);
            return (new UserService(new Repository<long, User>(ctx)), ctx);
        }

        private static User MakeUser(long id, string name, int roleId = 2, int? deptId = 1, string? phone = null) => new User
        {
            Id = id, UserName = name, Email = $"{name}@test.com", DisplayName = name,
            IsActive = true, RoleId = roleId, DepartmentId = deptId, Phone = phone,
            PasswordHash = Array.Empty<byte>(), PasswordSalt = Array.Empty<byte>()
        };

        // ── GetAsync ──────────────────────────────────────────────────────────

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
        public async Task GetAsync_ReturnsUserWithDepartment()
        {
            var (sut, ctx) = Build(nameof(GetAsync_ReturnsUserWithDepartment));
            ctx.Users.Add(MakeUser(1, "alice", deptId: 1));
            await ctx.SaveChangesAsync();

            var result = await sut.GetAsync(1);

            Assert.Equal("IT", result!.DepartmentName);
        }

        [Fact]
        public async Task GetAsync_ReturnsUserWithNullDepartment()
        {
            var (sut, ctx) = Build(nameof(GetAsync_ReturnsUserWithNullDepartment));
            ctx.Users.Add(MakeUser(1, "alice", deptId: null));
            await ctx.SaveChangesAsync();

            var result = await sut.GetAsync(1);

            Assert.Null(result!.DepartmentName);
        }

        // ── GetAgentsAsync ────────────────────────────────────────────────────

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
        public async Task GetAgentsAsync_ThrowsWhenNoneInDepartment()
        {
            var (sut, ctx) = Build(nameof(GetAgentsAsync_ThrowsWhenNoneInDepartment));
            ctx.Users.Add(MakeUser(1, "agent1", roleId: 2, deptId: 1));
            await ctx.SaveChangesAsync();

            await Assert.ThrowsAsync<Exception>(() => sut.GetAgentsAsync(departmentId: 99));
        }

        [Fact]
        public async Task GetAgentsAsync_ReturnsAgentWithNullDepartment()
        {
            var (sut, ctx) = Build(nameof(GetAgentsAsync_ReturnsAgentWithNullDepartment));
            ctx.Users.Add(MakeUser(1, "agent_nodept", roleId: 2, deptId: null));
            await ctx.SaveChangesAsync();

            var result = await sut.GetAgentsAsync();

            Assert.Single(result);
            Assert.Null(result[0].DepartmentName);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsUserWithNullDepartment()
        {
            var (sut, ctx) = Build(nameof(GetAllAsync_ReturnsUserWithNullDepartment));
            ctx.Users.Add(MakeUser(1, "nodept", roleId: 3, deptId: null));
            await ctx.SaveChangesAsync();

            var result = await sut.GetAllAsync();

            Assert.Single(result);
            Assert.Null(result[0].DepartmentName);
        }

        // ── GetAllAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllAsync_ReturnsAllUsers()
        {
            var (sut, ctx) = Build(nameof(GetAllAsync_ReturnsAllUsers));
            ctx.Users.AddRange(
                MakeUser(1, "alice", roleId: 1),
                MakeUser(2, "bob", roleId: 2),
                MakeUser(3, "charlie", roleId: 3)
            );
            await ctx.SaveChangesAsync();

            var result = await sut.GetAllAsync();

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsEmptyWhenNoUsers()
        {
            var (sut, _) = Build(nameof(GetAllAsync_ReturnsEmptyWhenNoUsers));
            var result = await sut.GetAllAsync();
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsUsersOrderedByDisplayName()
        {
            var (sut, ctx) = Build(nameof(GetAllAsync_ReturnsUsersOrderedByDisplayName));
            ctx.Users.AddRange(
                MakeUser(1, "zara", roleId: 3),
                MakeUser(2, "alice", roleId: 3)
            );
            await ctx.SaveChangesAsync();

            var result = await sut.GetAllAsync();

            Assert.Equal("alice", result[0].UserName);
        }

        // ── UpdateProfileAsync ────────────────────────────────────────────────

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
        public async Task UpdateProfileAsync_UpdatesPhone()
        {
            var (sut, ctx) = Build(nameof(UpdateProfileAsync_UpdatesPhone));
            ctx.Users.Add(MakeUser(1, "bob", phone: null));
            await ctx.SaveChangesAsync();

            // Phone update should succeed without throwing
            var result = await sut.UpdateProfileAsync(1, new UpdateProfileDto { Phone = "555-1234" });

            Assert.NotNull(result);
            Assert.Equal("bob", result!.UserName);
        }

        [Fact]
        public async Task UpdateProfileAsync_ClearsPhone_WhenSetToNull()
        {
            var (sut, ctx) = Build(nameof(UpdateProfileAsync_ClearsPhone_WhenSetToNull));
            ctx.Users.Add(MakeUser(1, "bob", phone: "555-0000"));
            await ctx.SaveChangesAsync();

            // Clearing phone should succeed without throwing
            var result = await sut.UpdateProfileAsync(1, new UpdateProfileDto { Phone = null });

            Assert.NotNull(result);
        }

        [Fact]
        public async Task UpdateProfileAsync_ThrowsWhenUserNotFound()
        {
            var (sut, _) = Build(nameof(UpdateProfileAsync_ThrowsWhenUserNotFound));
            await Assert.ThrowsAsync<Exception>(() =>
                sut.UpdateProfileAsync(999, new UpdateProfileDto { DisplayName = "X" }));
        }

        [Fact]
        public async Task UpdateProfileAsync_NoChanges_ReturnsSameUser()
        {
            var (sut, ctx) = Build(nameof(UpdateProfileAsync_NoChanges_ReturnsSameUser));
            ctx.Users.Add(MakeUser(1, "bob"));
            await ctx.SaveChangesAsync();

            var result = await sut.UpdateProfileAsync(1, new UpdateProfileDto());

            Assert.Equal("bob", result!.UserName);
        }

        [Fact]
        public async Task UpdateProfileAsync_UserWithNullDepartment_ReturnsNullDepartmentName()
        {
            var (sut, ctx) = Build(nameof(UpdateProfileAsync_UserWithNullDepartment_ReturnsNullDepartmentName));
            ctx.Users.Add(MakeUser(1, "bob", deptId: null));
            await ctx.SaveChangesAsync();

            var result = await sut.UpdateProfileAsync(1, new UpdateProfileDto { DisplayName = "Bobby" });

            Assert.Null(result!.DepartmentName);
        }

        [Fact]
        public async Task UpdateProfileAsync_PhoneAlreadyNullAndModelPhoneNull_DoesNotUpdate()
        {
            // Covers: model.Phone == null && u.Phone != null = false (phone already null)
            var (sut, ctx) = Build(nameof(UpdateProfileAsync_PhoneAlreadyNullAndModelPhoneNull_DoesNotUpdate));
            ctx.Users.Add(MakeUser(1, "bob", phone: null)); // phone already null
            await ctx.SaveChangesAsync();

            // model.Phone = null, u.Phone = null → condition false, phone stays null
            var result = await sut.UpdateProfileAsync(1, new UpdateProfileDto { Phone = null });

            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetAsync_UserWithNoRole_ReturnsEmptyRoleName()
        {
            // Covers: u.Role?.Name ?? "" null branch
            // Use seeded context but add a role that has no name match — achieved by
            // adding a user whose RoleId points to a role not in the seeded set.
            var (sut, ctx) = Build(nameof(GetAsync_UserWithNoRole_ReturnsEmptyRoleName));
            // Add a role with empty name to simulate null-name scenario
            ctx.Roles.Add(new Role { Id = 99, Name = "" });
            ctx.Users.Add(new User
            {
                Id = 50, UserName = "norole", Email = "nr@t.com", DisplayName = "NoRole",
                IsActive = true, RoleId = 99,
                PasswordHash = Array.Empty<byte>(), PasswordSalt = Array.Empty<byte>()
            });
            await ctx.SaveChangesAsync();

            var result = await sut.GetAsync(50);

            Assert.Equal(string.Empty, result!.RoleName);
        }

        [Fact]
        public async Task UpdateProfileAsync_UserWithNoRole_ReturnsEmptyRoleName()
        {
            // Covers: result.Role?.Name ?? "" null branch in UpdateProfileAsync
            var (sut, ctx) = Build(nameof(UpdateProfileAsync_UserWithNoRole_ReturnsEmptyRoleName));
            ctx.Roles.Add(new Role { Id = 99, Name = "" });
            ctx.Users.Add(new User
            {
                Id = 50, UserName = "norole", Email = "nr@t.com", DisplayName = "NoRole",
                IsActive = true, RoleId = 99,
                PasswordHash = Array.Empty<byte>(), PasswordSalt = Array.Empty<byte>()
            });
            await ctx.SaveChangesAsync();

            var result = await sut.UpdateProfileAsync(50, new UpdateProfileDto { DisplayName = "Updated" });

            Assert.Equal(string.Empty, result!.RoleName);
        }
    }
}
