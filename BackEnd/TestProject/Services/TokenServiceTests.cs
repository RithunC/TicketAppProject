using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TicketWebApp.Models.DTOs;
using TicketWebApp.Services;

namespace TestProject.Services
{
    public class TokenServiceTests
    {
        private static TokenService BuildSut(string? secret = null)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Keys:Jwt"] = secret ?? "super-secret-key-for-testing-1234567890"
                })
                .Build();
            return new TokenService(config);
        }

        private static string MakeCraftedToken(
            string secret,
            Claim[] claims,
            DateTime? expires = null)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);
            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expires ?? DateTime.UtcNow.AddMinutes(15),
                SigningCredentials = creds
            };
            var handler = new JwtSecurityTokenHandler();
            return handler.WriteToken(handler.CreateToken(descriptor));
        }

        private readonly TokenService _sut = BuildSut();
        private const string Secret = "super-secret-key-for-testing-1234567890";

        [Fact]
        public void Constructor_ThrowsWhenJwtKeyMissing()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();
            Assert.Throws<InvalidOperationException>(() => new TokenService(config));
        }

        [Fact]
        public void CreateToken_ReturnsNonEmptyString()
        {
            var token = _sut.CreateToken(new AuthDtos.TokenPayloadDto
            {
                Username = "testuser", Role = "Admin", UserId = 1
            });
            Assert.False(string.IsNullOrWhiteSpace(token));
        }

        [Fact]
        public void CreatePasswordResetToken_ReturnsNonEmptyString()
        {
            Assert.False(string.IsNullOrWhiteSpace(_sut.CreatePasswordResetToken(42, "testuser")));
        }

        // ── TryValidatePasswordResetToken ─────────────────────────────────────

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
            Assert.False(_sut.TryValidatePasswordResetToken("not.a.valid.token", out var userId, out _));
            Assert.Equal(0, userId);
        }

        [Fact]
        public void TryValidatePasswordResetToken_ReturnsFalseForRegularAuthToken()
        {
            // purpose claim missing → string.Equals(null, "pwdreset") = false
            var authToken = _sut.CreateToken(new AuthDtos.TokenPayloadDto
            {
                Username = "user", Role = "Agent", UserId = 1
            });
            Assert.False(_sut.TryValidatePasswordResetToken(authToken, out _, out _));
        }

        [Fact]
        public void TryValidatePasswordResetToken_ReturnsFalseForWrongPurposeClaim()
        {
            // purpose claim present but wrong value
            var token = MakeCraftedToken(Secret, new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, "user"),
                new Claim("purpose", "other")
            });
            Assert.False(_sut.TryValidatePasswordResetToken(token, out _, out _));
        }

        [Fact]
        public void TryValidatePasswordResetToken_ReturnsFalseForTokenSignedWithDifferentKey()
        {
            var otherSut = BuildSut("completely-different-secret-key-xyz-9876543210");
            var token = otherSut.CreatePasswordResetToken(1, "user");
            Assert.False(_sut.TryValidatePasswordResetToken(token, out _, out _));
        }

        [Fact]
        public void TryValidatePasswordResetToken_ReturnsFalseWhenNameIdentifierClaimMissing()
        {
            // idClaim is null → string.IsNullOrWhiteSpace(null) = true → return false
            var token = MakeCraftedToken(Secret, new[]
            {
                new Claim(ClaimTypes.Name, "user"),
                new Claim("purpose", "pwdreset")
                // no NameIdentifier
            });
            Assert.False(_sut.TryValidatePasswordResetToken(token, out var userId, out _));
            Assert.Equal(0, userId);
        }

        [Fact]
        public void TryValidatePasswordResetToken_ReturnsFalseWhenNameIdentifierNotNumeric()
        {
            // long.TryParse fails → return false
            var token = MakeCraftedToken(Secret, new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "not-a-number"),
                new Claim(ClaimTypes.Name, "user"),
                new Claim("purpose", "pwdreset")
            });
            Assert.False(_sut.TryValidatePasswordResetToken(token, out var userId, out _));
            Assert.Equal(0, userId);
        }

        [Fact]
        public void TryValidatePasswordResetToken_ReturnsEmptyUsernameWhenNameClaimMissing()
        {
            // nameClaim is null → username = null ?? string.Empty = ""
            var token = MakeCraftedToken(Secret, new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "99"),
                new Claim("purpose", "pwdreset")
                // no ClaimTypes.Name
            });
            var result = _sut.TryValidatePasswordResetToken(token, out var userId, out var username);
            Assert.True(result);
            Assert.Equal(99, userId);
            Assert.Equal(string.Empty, username);
        }
    }
}
