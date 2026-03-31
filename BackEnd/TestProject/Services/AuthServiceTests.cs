using Microsoft.Extensions.Configuration;
using Moq;
using TestProject.Helpers;
using TicketWebApp.Interfaces;
using TicketWebApp.Models;
using TicketWebApp.Models.DTOs;
using TicketWebApp.Services;

namespace TestProject.Services
{
    public class AuthServiceTests
    {
        private static (AuthService sut, TicketWebApp.Contexts.ComplaintContext ctx) Build(string db)
        {
            var ctx = DbContextFactory.CreateWithSeed(db);

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Keys:Jwt"] = "super-secret-key-for-testing-1234567890"
                })
                .Build();

            var passwords = new PasswordService();
            var tokens = new TokenService(config);
            var sut = new AuthService(ctx, passwords, tokens);
            return (sut, ctx);
        }

        // ── Register ──────────────────────────────────────────────────────────

        [Fact]
        public async Task RegisterAsync_SucceedsWithValidData()
        {
            var (sut, _) = Build(nameof(RegisterAsync_SucceedsWithValidData));

            var result = await sut.RegisterAsync(new AuthDtos.RegisterUserRequestDto
            {
                UserName = "alice",
                Email = "alice@example.com",
                DisplayName = "Alice",
                Password = "Pass@123",
                RoleName = "Agent",
                DepartmentName = "IT"
            });

            Assert.True(result.Success);
            Assert.NotNull(result.Token);
            Assert.Equal("Agent", result.Role);
        }

        [Fact]
        public async Task RegisterAsync_FailsWhenUsernameAlreadyExists()
        {
            var (sut, _) = Build(nameof(RegisterAsync_FailsWhenUsernameAlreadyExists));
            var req = new AuthDtos.RegisterUserRequestDto
            {
                UserName = "bob",
                Email = "bob@example.com",
                DisplayName = "Bob",
                Password = "Pass@123",
                RoleName = "Employee"
            };
            await sut.RegisterAsync(req);

            var result = await sut.RegisterAsync(new AuthDtos.RegisterUserRequestDto
            {
                UserName = "bob",
                Email = "bob2@example.com",
                DisplayName = "Bob2",
                Password = "Pass@123",
                RoleName = "Employee"
            });

            Assert.False(result.Success);
            Assert.Contains("Username", result.Message);
        }

        [Fact]
        public async Task RegisterAsync_FailsForInvalidRoleName()
        {
            var (sut, _) = Build(nameof(RegisterAsync_FailsForInvalidRoleName));

            var result = await sut.RegisterAsync(new AuthDtos.RegisterUserRequestDto
            {
                UserName = "charlie",
                Email = "charlie@example.com",
                DisplayName = "Charlie",
                Password = "Pass@123",
                RoleName = "SuperAdmin"
            });

            Assert.False(result.Success);
            Assert.Contains("RoleName", result.Message);
        }

        [Fact]
        public async Task RegisterAsync_FailsWhenRequiredFieldsMissing()
        {
            var (sut, _) = Build(nameof(RegisterAsync_FailsWhenRequiredFieldsMissing));

            var result = await sut.RegisterAsync(new AuthDtos.RegisterUserRequestDto());

            Assert.False(result.Success);
        }

        // ── Login ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task LoginAsync_ReturnsTokenForValidCredentials()
        {
            var (sut, _) = Build(nameof(LoginAsync_ReturnsTokenForValidCredentials));
            await sut.RegisterAsync(new AuthDtos.RegisterUserRequestDto
            {
                UserName = "dave",
                Email = "dave@example.com",
                DisplayName = "Dave",
                Password = "Pass@123",
                RoleName = "Employee"
            });

            var result = await sut.LoginAsync(new AuthDtos.CheckUserRequestDto
            {
                UserName = "dave",
                Password = "Pass@123"
            });

            Assert.False(string.IsNullOrWhiteSpace(result.Token));
        }

        [Fact]
        public async Task LoginAsync_ReturnsEmptyTokenForWrongPassword()
        {
            var (sut, _) = Build(nameof(LoginAsync_ReturnsEmptyTokenForWrongPassword));
            await sut.RegisterAsync(new AuthDtos.RegisterUserRequestDto
            {
                UserName = "eve",
                Email = "eve@example.com",
                DisplayName = "Eve",
                Password = "Pass@123",
                RoleName = "Employee"
            });

            var result = await sut.LoginAsync(new AuthDtos.CheckUserRequestDto
            {
                UserName = "eve",
                Password = "WrongPass"
            });

            Assert.Equal(string.Empty, result.Token);
        }

        [Fact]
        public async Task LoginAsync_ReturnsEmptyTokenForUnknownUser()
        {
            var (sut, _) = Build(nameof(LoginAsync_ReturnsEmptyTokenForUnknownUser));

            var result = await sut.LoginAsync(new AuthDtos.CheckUserRequestDto
            {
                UserName = "nobody",
                Password = "Pass@123"
            });

            Assert.Equal(string.Empty, result.Token);
        }

        // ── ForgotPassword ────────────────────────────────────────────────────

        [Fact]
        public async Task ForgotPasswordAsync_ReturnsResetTokenForKnownUser()
        {
            var (sut, _) = Build(nameof(ForgotPasswordAsync_ReturnsResetTokenForKnownUser));
            await sut.RegisterAsync(new AuthDtos.RegisterUserRequestDto
            {
                UserName = "frank",
                Email = "frank@example.com",
                DisplayName = "Frank",
                Password = "Pass@123",
                RoleName = "Employee"
            });

            var result = await sut.ForgotPasswordAsync(new AuthDtos.ForgotPasswordRequestDto
            {
                UserNameOrEmail = "frank"
            });

            Assert.True(result.Success);
            Assert.False(string.IsNullOrWhiteSpace(result.ResetToken));
        }

        [Fact]
        public async Task ForgotPasswordAsync_FailsForUnknownUser()
        {
            var (sut, _) = Build(nameof(ForgotPasswordAsync_FailsForUnknownUser));

            var result = await sut.ForgotPasswordAsync(new AuthDtos.ForgotPasswordRequestDto
            {
                UserNameOrEmail = "ghost"
            });

            Assert.False(result.Success);
        }

        // ── ResetPassword ─────────────────────────────────────────────────────

        [Fact]
        public async Task ResetPasswordAsync_SucceedsWithValidToken()
        {
            var (sut, _) = Build(nameof(ResetPasswordAsync_SucceedsWithValidToken));
            await sut.RegisterAsync(new AuthDtos.RegisterUserRequestDto
            {
                UserName = "grace",
                Email = "grace@example.com",
                DisplayName = "Grace",
                Password = "OldPass@123",
                RoleName = "Employee"
            });
            var forgot = await sut.ForgotPasswordAsync(new AuthDtos.ForgotPasswordRequestDto
            {
                UserNameOrEmail = "grace"
            });

            var result = await sut.ResetPasswordAsync(new AuthDtos.ResetPasswordRequestDto
            {
                Token = forgot.ResetToken!,
                NewPassword = "NewPass@456"
            });

            Assert.True(result.Success);

            // Verify new password works
            var login = await sut.LoginAsync(new AuthDtos.CheckUserRequestDto
            {
                UserName = "grace",
                Password = "NewPass@456"
            });
            Assert.False(string.IsNullOrWhiteSpace(login.Token));
        }

        [Fact]
        public async Task ResetPasswordAsync_FailsWithInvalidToken()
        {
            var (sut, _) = Build(nameof(ResetPasswordAsync_FailsWithInvalidToken));

            var result = await sut.ResetPasswordAsync(new AuthDtos.ResetPasswordRequestDto
            {
                Token = "bad.token.here",
                NewPassword = "NewPass@456"
            });

            Assert.False(result.Success);
        }
    }
}
