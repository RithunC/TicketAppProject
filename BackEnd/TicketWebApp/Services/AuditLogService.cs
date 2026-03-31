using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TicketWebApp.Interfaces;
using TicketWebApp.Models;
using TicketWebApp.Models.Common;
using TicketWebApp.Models.DTOs;
using static TicketWebApp.Models.DTOs.AuditLogDtos;

namespace TicketWebApp.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly IRepository<long, AuditLog> _repo;

        public AuditLogService(IRepository<long, AuditLog> repo)
        {
            _repo = repo;
        }

        // ✅ interface method
        public async Task LogAsync(AuditLog log)
        {
            await _repo.Add(log);
        }

        // ✅ Wrapper to simplify logging calls
        public async Task LogEventAsync(
            string action,
            string entityType,
            string? entityId,
            bool success,
            int statusCode,
            string? message,
            object? metadata = null)
        {
            await LogAsync(new AuditLog
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Success = success,
                StatusCode = statusCode,
                Message = message,
                OccurredAtUtc = DateTime.UtcNow,
                MetadataJson = metadata != null
                    ? System.Text.Json.JsonSerializer.Serialize(metadata)
                    : null
            });
        }

        // ✅ interface method
        public async Task<PagedResult<AuditLogResponseDto>> QueryAsync(AuditLogQueryDto query)
        {
            var baseQ = _repo.GetQueryable();

            if (query.FromUtc.HasValue)
                baseQ = baseQ.Where(a => a.OccurredAtUtc >= query.FromUtc);

            if (query.ToUtc.HasValue)
                baseQ = baseQ.Where(a => a.OccurredAtUtc <= query.ToUtc);

            if (!string.IsNullOrWhiteSpace(query.Action))
                baseQ = baseQ.Where(a => a.Action == query.Action);

            int total = await baseQ.CountAsync();

            // ✅ STEP 1: Fetch raw entities first (NO JSON parsing in SQL)
            var logs = await baseQ
                .OrderByDescending(a => a.OccurredAtUtc)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            // ✅ STEP 2: Map to DTO in memory (SAFE)
            var items = logs.Select(a =>
            {
                int? durationMs = null;

                if (!string.IsNullOrEmpty(a.MetadataJson))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(a.MetadataJson);
                        if (doc.RootElement.TryGetProperty("DurationMs", out var durProp))
                        {
                            durationMs = durProp.GetInt32();
                        }
                    }
                    catch
                    {
                        // ignore corrupted JSON safely
                    }
                }

                return new AuditLogResponseDto
                {
                    Id = a.Id,
                    ActorUserId = a.ActorUserId,
                    ActorUserName = a.ActorUserName,
                    ActorRole = a.ActorRole,

                    Action = a.Action,
                    Path = a.Path,
                    Description = a.Message,

                    Success = a.Success,
                    StatusCode = a.StatusCode,
                    OccurredAtUtc = a.OccurredAtUtc,

                    DurationMs = durationMs
                };
            }).ToList();

            return new PagedResult<AuditLogResponseDto>
            {
                Items = items,
                TotalCount = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }


        // ✅ interface method
        public async Task<IReadOnlyList<AuditLogResponseDto>> GetRecentAsync(int take = 100)
        {
            var list = await _repo.GetQueryable()
                .OrderByDescending(a => a.OccurredAtUtc)
                .Take(take)
                .ToListAsync();

            return list.Select(a => new AuditLogResponseDto
            {
                Id = a.Id,
                ActorUserId = a.ActorUserId,
                ActorUserName = a.ActorUserName,
                ActorRole = a.ActorRole,
                Action = a.Action,
                Path = a.Path,
                Success = a.Success,
                StatusCode = a.StatusCode,
                OccurredAtUtc = a.OccurredAtUtc
            }).ToList();
        }
    }
}