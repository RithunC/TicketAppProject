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

        // ── LogAsync ──────────────────────────────────────────────────────────

        [Fact]
        public async Task LogAsync_PersistsAuditLog()
        {
            var sut = Build(nameof(LogAsync_PersistsAuditLog));

            await sut.LogAsync(new AuditLog
            {
                Action = "GET", EntityType = "Ticket", EntityId = "1",
                Success = true, StatusCode = 200, HttpMethod = "GET", Path = "/api/tickets/1"
            });

            var recent = await sut.GetRecentAsync(10);
            Assert.Single(recent);
            Assert.Equal("GET", recent[0].Action);
        }

        // ── LogEventAsync ─────────────────────────────────────────────────────

        [Fact]
        public async Task LogEventAsync_PersistsWithMetadata()
        {
            var sut = Build(nameof(LogEventAsync_PersistsWithMetadata));

            await sut.LogEventAsync(
                action: "CREATE",
                entityType: "Ticket",
                entityId: "42",
                success: true,
                statusCode: 201,
                message: "Ticket created",
                metadata: new { DurationMs = 150 });

            var result = await sut.QueryAsync(new AuditLogQueryDto { Page = 1, PageSize = 10 });
            Assert.Single(result.Items);
            Assert.Equal("CREATE", result.Items[0].Action);
            Assert.Equal(150, result.Items[0].DurationMs);
        }

        [Fact]
        public async Task LogEventAsync_PersistsWithoutMetadata()
        {
            var sut = Build(nameof(LogEventAsync_PersistsWithoutMetadata));

            await sut.LogEventAsync("DELETE", "Ticket", "5", false, 404, "Not found");

            var recent = await sut.GetRecentAsync(10);
            Assert.Single(recent);
            Assert.Equal("DELETE", recent[0].Action);
            Assert.Null(recent[0].DurationMs);
        }

        [Fact]
        public async Task LogEventAsync_PersistsWithNullEntityId()
        {
            var sut = Build(nameof(LogEventAsync_PersistsWithNullEntityId));

            await sut.LogEventAsync("LOGIN", "Auth", null, true, 200, null);

            var recent = await sut.GetRecentAsync(10);
            Assert.Single(recent);
        }

        // ── GetRecentAsync ────────────────────────────────────────────────────

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

        // ── QueryAsync ────────────────────────────────────────────────────────

        [Fact]
        public async Task QueryAsync_FiltersByDateRange()
        {
            var sut = Build(nameof(QueryAsync_FiltersByDateRange));

            await sut.LogAsync(new AuditLog
            {
                Action = "OLD", EntityType = "T", Success = true, StatusCode = 200,
                HttpMethod = "GET", Path = "/old", OccurredAtUtc = DateTime.UtcNow.AddDays(-2)
            });
            await sut.LogAsync(new AuditLog
            {
                Action = "NEW", EntityType = "T", Success = true, StatusCode = 200,
                HttpMethod = "GET", Path = "/new", OccurredAtUtc = DateTime.UtcNow
            });

            var result = await sut.QueryAsync(new AuditLogQueryDto
            {
                FromUtc = DateTime.UtcNow.AddHours(-1),
                Page = 1, PageSize = 50
            });

            Assert.Equal(1, result.TotalCount);
            Assert.Equal("NEW", result.Items[0].Action);
        }

        [Fact]
        public async Task QueryAsync_FiltersByToUtc()
        {
            var sut = Build(nameof(QueryAsync_FiltersByToUtc));

            await sut.LogAsync(new AuditLog
            {
                Action = "OLD", EntityType = "T", Success = true, StatusCode = 200,
                HttpMethod = "GET", Path = "/old", OccurredAtUtc = DateTime.UtcNow.AddDays(-2)
            });
            await sut.LogAsync(new AuditLog
            {
                Action = "NEW", EntityType = "T", Success = true, StatusCode = 200,
                HttpMethod = "GET", Path = "/new", OccurredAtUtc = DateTime.UtcNow
            });

            var result = await sut.QueryAsync(new AuditLogQueryDto
            {
                ToUtc = DateTime.UtcNow.AddDays(-1),
                Page = 1, PageSize = 50
            });

            Assert.Equal(1, result.TotalCount);
            Assert.Equal("OLD", result.Items[0].Action);
        }

        [Fact]
        public async Task QueryAsync_FiltersByAction()
        {
            var sut = Build(nameof(QueryAsync_FiltersByAction));

            await sut.LogAsync(new AuditLog
            {
                Action = "CREATE", EntityType = "T", Success = true, StatusCode = 201,
                HttpMethod = "POST", Path = "/tickets"
            });
            await sut.LogAsync(new AuditLog
            {
                Action = "DELETE", EntityType = "T", Success = true, StatusCode = 200,
                HttpMethod = "DELETE", Path = "/tickets/1"
            });

            var result = await sut.QueryAsync(new AuditLogQueryDto
            {
                Action = "CREATE", Page = 1, PageSize = 50
            });

            Assert.Equal(1, result.TotalCount);
            Assert.Equal("CREATE", result.Items[0].Action);
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

        [Fact]
        public async Task QueryAsync_WithMetadataJson_ParsesDurationMs()
        {
            var sut = Build(nameof(QueryAsync_WithMetadataJson_ParsesDurationMs));

            await sut.LogEventAsync("GET", "Ticket", "1", true, 200, null, new { DurationMs = 42 });

            var result = await sut.QueryAsync(new AuditLogQueryDto { Page = 1, PageSize = 10 });

            Assert.Equal(42, result.Items[0].DurationMs);
        }

        [Fact]
        public async Task QueryAsync_WithCorruptMetadataJson_DoesNotThrow()
        {
            var ctx = DbContextFactory.Create(nameof(QueryAsync_WithCorruptMetadataJson_DoesNotThrow));
            var sut = new AuditLogService(new Repository<long, AuditLog>(ctx));

            // Manually insert a log with corrupt JSON
            ctx.AuditLogs.Add(new AuditLog
            {
                Action = "TEST", EntityType = "T", Success = true, StatusCode = 200,
                HttpMethod = "GET", Path = "/test",
                MetadataJson = "{ not valid json !!!"
            });
            await ctx.SaveChangesAsync();

            var result = await sut.QueryAsync(new AuditLogQueryDto { Page = 1, PageSize = 10 });

            Assert.Equal(1, result.TotalCount);
            Assert.Null(result.Items[0].DurationMs);
        }

        [Fact]
        public async Task QueryAsync_WithMetadataJsonMissingDurationMs_ReturnsNull()
        {
            var sut = Build(nameof(QueryAsync_WithMetadataJsonMissingDurationMs_ReturnsNull));

            await sut.LogEventAsync("GET", "Ticket", "1", true, 200, null, new { OtherField = "value" });

            var result = await sut.QueryAsync(new AuditLogQueryDto { Page = 1, PageSize = 10 });

            Assert.Null(result.Items[0].DurationMs);
        }
    }
}
