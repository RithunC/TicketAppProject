using Microsoft.Extensions.Configuration;
using TicketWebApp.Models.DTOs;
using TicketWebApp.Services;

namespace TestProject.Services
{
    public class TokenServiceTests
    {
        private readonly TokenService _sut;

        public TokenServiceTests()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Keys:Jwt"] = "super-secret-key-for-testing-1234567890"
                })
                .Build();
            _sut = new TokenService(config);
        }

        [Fact]
        public void CreateToken_ReturnsNonEmptyString()
        {
            var payload = new AuthDtos.TokenPayloadDto
            {
                Username = "testuser",
                Role = "Admin",
                UserId = 1
            };

            var token = _sut.CreateToken(payload);

            Assert.False(string.IsNullOrWhiteSpace(token));
        }

        [Fact]
        public void CreatePasswordResetToken_ReturnsNonEmptyString()
        {
            var token = _sut.CreatePasswordResetToken(42, "testuser");

            Assert.False(string.IsNullOrWhiteSpace(token));
        }

        [Fact]
        public void TryValidatePasswordResetToken_ReturnsTrueForValidToken()
        {
            var token = _sut.CreatePasswordResetToken(42, "testuser");

            var result = _sut.TryValidatePasswordResetToken(token, out var userId, out var username);

            Assert.True(result);
            Assert.Equal(42, userId);
            Assert.Equal("testuser", username);
        }

        [Fact]
        public void TryValidatePasswordResetToken_ReturnsFalseForGarbageToken()
        {
            var result = _sut.TryValidatePasswordResetToken("not.a.valid.token", out var userId, out var username);

            Assert.False(result);
            Assert.Equal(0, userId);
        }

        [Fact]
        public void TryValidatePasswordResetToken_ReturnsFalseForRegularAuthToken()
        {
            // A regular auth token should fail because it lacks the "purpose=pwdreset" claim
            var authToken = _sut.CreateToken(new AuthDtos.TokenPayloadDto
            {
                Username = "user",
                Role = "Agent",
                UserId = 1
            });

            var result = _sut.TryValidatePasswordResetToken(authToken, out _, out _);

            Assert.False(result);
        }
    }
}
