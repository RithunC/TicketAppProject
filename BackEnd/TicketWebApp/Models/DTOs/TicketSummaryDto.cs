namespace TicketWebApp.Models.DTOs
{
    public class TicketSummaryDto
    {
        // Totals
        public int Total { get; set; }
        public int Open { get; set; }        // New + Open statuses
        public int InProgress { get; set; }
        public int OnHold { get; set; }      // On Hold status
        public int Resolved { get; set; }
        public int Closed { get; set; }
        public int Active { get; set; }      // All non-closed (Open + InProgress + OnHold)

        // Priority buckets
        public int UrgentPriority { get; set; }
        public int HighPriority { get; set; }
        public int MediumPriority { get; set; }
        public int LowPriority { get; set; }

        // "My work" (for Agents/Admins, counts tickets assigned to them)
        public int AssignedToMe { get; set; }

        // SLA / overdue (DueAt < now and IsClosedState = false)
        public int Overdue { get; set; }
    }
}

namespace TicketWebApp.Models.DTOs
{
    public class AgentWorkloadDto
    {
        public long AgentId { get; set; }
        public string AgentName { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
        public int TotalAssigned { get; set; }
        public int ActiveOpen { get; set; }  // IsClosedState = 0 (New + Open + In Progress + On Hold)
        public int Open { get; set; }
        public int InProgress { get; set; }
        public int OnHold { get; set; }
        public int Resolved { get; set; }
        public int Closed { get; set; }
    }
}
