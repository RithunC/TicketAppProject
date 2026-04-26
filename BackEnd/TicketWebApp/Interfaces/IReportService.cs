using TicketWebApp.Models.DTOs;

namespace TicketWebApp.Interfaces
{
    public interface IReportService
    {
        Task<TicketSummaryDto> GetTicketSummaryAsync(long currentUserId, bool isAdmin, bool isAgent, bool isEmployee);
        Task<IReadOnlyList<AgentWorkloadDto>> GetAgentWorkloadAsync();
    }
}