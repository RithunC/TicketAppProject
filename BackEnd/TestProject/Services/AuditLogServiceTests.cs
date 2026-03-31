using TestProject.Helpers;
using TicketWebApp.Models;
using TicketWebApp.Models.DTOs;
using TicketWebApp.Repositories;
using TicketWebApp.Services;
using static TicketWebApp.Models.DTOs.AuditLogDtos;

namespace TestProject.Services
{
    public class AuditLogServiceTests
    {
        private static AuditLogService Build(string db)
        {
            var ctx = DbContextFactory.Create(db);
            return new AuditLogService(new Repository<long, AuditLog>(ctx));
        }

        [Fact]
        public async Task LogAsync_PersistsAuditLog()
        {
            var sut = Build(nameof(LogAsync_PersistsAuditLog));

            await sut.LogAsync(new AuditLog
            {
                Action = "GET",
                EntityType = "Ticket",
                EntityId = "1",
                Success = true,
                StatusCode = 200,
                HttpMethod = "GET",
                Path = "/api/tickets/1"
            });

            var recent = await sut.GetRecentAsync(10);
            Assert.Single(recent);
            Assert.Equal("GET", recent[0].Action);
        }

        [Fact]
        public async Task GetRecentAsync_ReturnsLogsOrderedByOccurredAtDesc()
        {
            var sut = Build(nameof(GetRecentAsync_ReturnsLogsOrderedByOccurredAtDesc));

            await sut.LogAsync(new AuditLog
            {
                Action = "A1", EntityType = "T", Success = true, StatusCode = 200,
                HttpMethod = "GET", Path = "/a", OccurredAtUtc = DateTime.UtcNow.AddMinutes(-5)
            });
            await sut.LogAsync(new AuditLog
            {
                Action = "A2", EntityType = "T", Success = true, StatusCode = 200,
                HttpMethod = "GET", Path = "/b", OccurredAtUtc = DateTime.UtcNow
            });

            var result = await sut.GetRecentAsync(10);

            Assert.Equal("A2", result[0].Action);
        }

        [Fact]
        public async Task QueryAsync_FiltersByDateRange()
        {
            var sut = Build(nameof(QueryAsync_FiltersByDateRange));
            var past = DateTime.UtcNow.AddDays(-2);
            var now = DateTime.UtcNow;

            await sut.LogAsync(new AuditLog
            {
                Action = "OLD", EntityType = "T", Success = true, StatusCode = 200,
                HttpMethod = "GET", Path = "/old", OccurredAtUtc = past
            });
            await sut.LogAsync(new AuditLog
            {
                Action = "NEW", EntityType = "T", Success = true, StatusCode = 200,
                HttpMethod = "GET", Path = "/new", OccurredAtUtc = now
            });

            var result = await sut.QueryAsync(new AuditLogQueryDto
            {
                FromUtc = DateTime.UtcNow.AddHours(-1),
                Page = 1,
                PageSize = 50
            });

            Assert.Equal(1, result.TotalCount);
            Assert.Equal("NEW", result.Items[0].Action);
        }

        [Fact]
        public async Task QueryAsync_ReturnsPaginatedResults()
        {
            var sut = Build(nameof(QueryAsync_ReturnsPaginatedResults));
            for (int i = 0; i < 5; i++)
                await sut.LogAsync(new AuditLog
                {
                    Action = $"ACT{i}", EntityType = "T", Success = true, StatusCode = 200,
                    HttpMethod = "GET", Path = $"/p{i}"
                });

            var result = await sut.QueryAsync(new AuditLogQueryDto { Page = 1, PageSize = 3 });

            Assert.Equal(5, result.TotalCount);
            Assert.Equal(3, result.Items.Count);
        }
    }
}
