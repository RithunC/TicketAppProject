using Microsoft.EntityFrameworkCore;
using TicketWebApp.Interfaces;
using TicketWebApp.Models;
using TicketWebApp.Models.DTOs;

namespace TicketWebApp.Services
{
    public class ReportService : IReportService
    {
        private readonly IRepository<long, Ticket> _ticketRepo;
        private readonly IRepository<int, Status> _statusRepo;
        private readonly IRepository<int, Priority> _priorityRepo;
        private readonly IRepository<long, User> _userRepo;

        public ReportService(
            IRepository<long, Ticket> ticketRepo,
            IRepository<int, Status> statusRepo,
            IRepository<int, Priority> priorityRepo,
            IRepository<long, User> userRepo)
        {
            _ticketRepo = ticketRepo;
            _statusRepo = statusRepo;
            _priorityRepo = priorityRepo;
            _userRepo = userRepo;
        }
        public async Task<TicketSummaryDto> GetTicketSummaryAsync(long currentUserId, bool isAdmin, bool isAgent, bool isEmployee)
        {
            // Ensure required lookups exist
            var statusConfigured = await _statusRepo.GetQueryable().AnyAsync();
            if (!statusConfigured)
                throw new InvalidOperationException("No statuses are configured.");

            var prioritiesConfigured = await _priorityRepo.GetQueryable().AnyAsync();
            if (!prioritiesConfigured)
                throw new InvalidOperationException("No priorities are configured.");

            // Base query
            var q = _ticketRepo.GetQueryable()
                .Include(t => t.Status)
                .Include(t => t.Priority)
                .AsQueryable();

            // Scope by role: Employees only see their own tickets
            if (isEmployee && !isAdmin && !isAgent)
            {
                if (currentUserId <= 0)
                    throw new ArgumentException("Invalid current user id for employee scope.", nameof(currentUserId));

                q = q.Where(t => t.CreatedByUserId == currentUserId);
            }

            // Materialize for aggregations
            var items = await q
                .Select(t => new
                {
                    t.Id,
                    StatusName = t.Status != null ? t.Status.Name : null,
                    IsClosedState = t.Status != null && t.Status.IsClosedState,
                    PriorityName = t.Priority != null ? t.Priority.Name : null,
                    t.CurrentAssigneeUserId,
                    t.CreatedByUserId,
                    t.DueAt
                })
                .ToListAsync(); //after this u have List<T> in server memory

            var now = DateTime.Now; // Use local time — DueAt stored as datetime2 (no timezone)

            bool IsStatus(string? s, string target) =>
                !string.IsNullOrWhiteSpace(s) && s.Trim().Equals(target, StringComparison.OrdinalIgnoreCase);

            // Status buckets — use exact name matching for known statuses
            var openCount       = items.Count(t => IsStatus(t.StatusName, "Open") || IsStatus(t.StatusName, "New"));
            var inProgressCount = items.Count(t => IsStatus(t.StatusName, "In Progress"));
            var onHoldCount     = items.Count(t => IsStatus(t.StatusName, "On Hold"));
            var resolvedCount   = items.Count(t => IsStatus(t.StatusName, "Resolved"));
            var closedCount     = items.Count(t => IsStatus(t.StatusName, "Closed"));
            var activeCount     = items.Count(t => !t.IsClosedState); // all non-terminal

            // Priority buckets
            int urgentPriority = items.Count(t => string.Equals(t.PriorityName, "Urgent", StringComparison.OrdinalIgnoreCase));
            int highPriority   = items.Count(t => string.Equals(t.PriorityName, "High",   StringComparison.OrdinalIgnoreCase));
            int mediumPriority = items.Count(t => string.Equals(t.PriorityName, "Medium", StringComparison.OrdinalIgnoreCase));
            int lowPriority    = items.Count(t => string.Equals(t.PriorityName, "Low",    StringComparison.OrdinalIgnoreCase));

            // Overdue: DueAt < now and not in a closed/terminal state
            int overdue = items.Count(t =>
                t.DueAt.HasValue
                && t.DueAt.Value < now
                && !t.IsClosedState);

            // Assigned to me (for Admin/Agent dashboards)
            int assignedToMe = (isAdmin || isAgent)
                ? items.Count(t => t.CurrentAssigneeUserId.HasValue && t.CurrentAssigneeUserId.Value == currentUserId)
                : 0;

            return new TicketSummaryDto
            {
                Total          = items.Count,
                Open           = openCount,
                InProgress     = inProgressCount,
                OnHold         = onHoldCount,
                Resolved       = resolvedCount,
                Closed         = closedCount,
                Active         = activeCount,
                UrgentPriority = urgentPriority,
                HighPriority   = highPriority,
                MediumPriority = mediumPriority,
                LowPriority    = lowPriority,
                AssignedToMe   = assignedToMe,
                Overdue        = overdue
            };
        }

        public async Task<IReadOnlyList<AgentWorkloadDto>> GetAgentWorkloadAsync()
        {
            // Load all active agents
            var agents = await _userRepo.GetQueryable()
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Where(u => u.IsActive && u.Role != null && u.Role.Name == "Agent")
                .Select(u => new { u.Id, u.DisplayName, DeptName = u.Department != null ? u.Department.Name : null })
                .ToListAsync();

            if (!agents.Any()) return new List<AgentWorkloadDto>();

            var agentIds = agents.Select(a => a.Id).ToList();
            var now = DateTime.Now;

            // Load ALL tickets assigned to these agents (including closed/resolved)
            var tickets = await _ticketRepo.GetQueryable()
                .Include(t => t.Status)
                .Where(t => t.CurrentAssigneeUserId != null && agentIds.Contains(t.CurrentAssigneeUserId.Value))
                .Select(t => new
                {
                    t.CurrentAssigneeUserId,
                    StatusName = t.Status != null ? t.Status.Name : null,
                    IsClosedState = t.Status != null && t.Status.IsClosedState,
                    t.DueAt
                })
                .ToListAsync();

            bool IsStatus(string? s, string target) =>
                !string.IsNullOrWhiteSpace(s) && s.Trim().Equals(target, StringComparison.OrdinalIgnoreCase);

            return agents.Select(a =>
            {
                var mine = tickets.Where(t => t.CurrentAssigneeUserId == a.Id).ToList();
                return new AgentWorkloadDto
                {
                    AgentId        = a.Id,
                    AgentName      = a.DisplayName,
                    DepartmentName = a.DeptName,
                    TotalAssigned  = mine.Count,
                    ActiveOpen     = mine.Count(t => !t.IsClosedState),
                    Open           = mine.Count(t => IsStatus(t.StatusName, "New") || IsStatus(t.StatusName, "Open")),
                    InProgress     = mine.Count(t => IsStatus(t.StatusName, "In Progress")),
                    OnHold         = mine.Count(t => IsStatus(t.StatusName, "On Hold")),
                    Resolved       = mine.Count(t => IsStatus(t.StatusName, "Resolved")),
                    Closed         = mine.Count(t => IsStatus(t.StatusName, "Closed"))
                };
            }).OrderByDescending(a => a.TotalAssigned).ToList();
        }
    }
}