using TestProject.Helpers;
using TicketWebApp.Models;
using TicketWebApp.Models.DTOs;
using TicketWebApp.Repositories;
using TicketWebApp.Services;

namespace TestProject.Services
{
    public class ErrorLogServiceTests
    {
        private static ErrorLogService Build(string db)
        {
            var ctx = DbContextFactory.Create(db);
            return new ErrorLogService(new Repository<int, ErrorLog>(ctx));
        }

        [Fact]
        public async Task CreateAsync_PersistsErrorLog()
        {
            var sut = Build(nameof(CreateAsync_PersistsErrorLog));

            var result = await sut.CreateAsync(new ErrorLogDtos.ErrorLogCreateDto
            {
                ErrorMessage = "NullRef",
                ErrorNumber = 500
            });

            Assert.Equal("NullRef", result.ErrorMessage);
            Assert.Equal(500, result.ErrorNumber);
        }

        [Fact]
        public async Task GetRecentAsync_ReturnsLogsOrderedByCreatedAtDesc()
        {
            var sut = Build(nameof(GetRecentAsync_ReturnsLogsOrderedByCreatedAtDesc));
            await sut.CreateAsync(new ErrorLogDtos.ErrorLogCreateDto { ErrorMessage = "First", ErrorNumber = 1 });
            await sut.CreateAsync(new ErrorLogDtos.ErrorLogCreateDto { ErrorMessage = "Second", ErrorNumber = 2 });

            var result = await sut.GetRecentAsync(10);

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetRecentAsync_RespectsDefaultTakeLimit()
        {
            var sut = Build(nameof(GetRecentAsync_RespectsDefaultTakeLimit));
            for (int i = 0; i < 5; i++)
                await sut.CreateAsync(new ErrorLogDtos.ErrorLogCreateDto { ErrorMessage = $"Err{i}", ErrorNumber = i });

            var result = await sut.GetRecentAsync(3);

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task GetRecentAsync_ClampsInvalidTakeToDefault()
        {
            var sut = Build(nameof(GetRecentAsync_ClampsInvalidTakeToDefault));
            await sut.CreateAsync(new ErrorLogDtos.ErrorLogCreateDto { ErrorMessage = "E", ErrorNumber = 1 });

            // take=0 should be clamped to 100 internally
            var result = await sut.GetRecentAsync(0);

            Assert.Single(result);
        }
    }
}
