using TicketWebApp.Services;

namespace TestProject.Services
{
    public class PasswordServiceTests
    {
        private readonly PasswordService _sut = new();

        [Fact]
        public void HashPassword_ReturnsDifferentHashForSamePasswordWithDifferentSalt()
        {
            var hash1 = _sut.HashPassword("secret", null, out var salt1);
            var hash2 = _sut.HashPassword("secret", null, out var salt2);

            // Different salts should produce different hashes
            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void HashPassword_ReturnsSameHashWhenSameSaltProvided()
        {
            var hash1 = _sut.HashPassword("secret", null, out var salt);
            var hash2 = _sut.HashPassword("secret", salt, out _);

            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void VerifyPassword_ReturnsTrueForCorrectPassword()
        {
            var hash = _sut.HashPassword("mypassword", null, out var salt);
            var result = _sut.VerifyPassword("mypassword", hash, salt);

            Assert.True(result);
        }

        [Fact]
        public void VerifyPassword_ReturnsFalseForWrongPassword()
        {
            var hash = _sut.HashPassword("mypassword", null, out var salt);
            var result = _sut.VerifyPassword("wrongpassword", hash, salt);

            Assert.False(result);
        }

        [Fact]
        public void HashPassword_OutputsSaltBytes()
        {
            _sut.HashPassword("test", null, out var salt);
            Assert.NotNull(salt);
            Assert.NotEmpty(salt);
        }
    }
}
