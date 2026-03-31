using TicketWebApp.Models;
using TicketWebApp.Models.Common;
using static TicketWebApp.Models.DTOs.AuditLogDtos;

public interface IAuditLogService
{
    Task LogAsync(AuditLog log);
    Task<PagedResult<AuditLogResponseDto>> QueryAsync(AuditLogQueryDto query);
    Task<IReadOnlyList<AuditLogResponseDto>> GetRecentAsync(int take = 100);
}
