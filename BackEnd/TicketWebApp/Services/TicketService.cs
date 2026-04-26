using Microsoft.EntityFrameworkCore;
using TicketWebApp.Interfaces;
using TicketWebApp.Models;
using TicketWebApp.Models.Common;
using TicketWebApp.Models.DTOs;
using static TicketWebApp.Models.DTOs.TicketAssignmentDtos;
using static TicketWebApp.Models.DTOs.TicketDtos;

namespace TicketWebApp.Services
{
    public class TicketService : ITicketService
    {
        private readonly IRepository<long, Ticket> _ticketRepo;
        private readonly IRepository<long, TicketAssignment> _assignmentRepo;
        private readonly IRepository<long, TicketStatusHistory> _statusHistoryRepo;
        private readonly IRepository<int, Status> _statusRepo;
        private readonly IRepository<long, User> _userRepo;

        public TicketService(
            IRepository<long, Ticket> ticketRepo,
            IRepository<long, TicketAssignment> assignmentRepo,
            IRepository<long, TicketStatusHistory> statusHistoryRepo,
            IRepository<int, Status> statusRepo,
            IRepository<long, User> userRepo)
        {
            _ticketRepo = ticketRepo;
            _assignmentRepo = assignmentRepo;
            _statusHistoryRepo = statusHistoryRepo;
            _statusRepo = statusRepo;
            _userRepo = userRepo;
        }

        public async Task<TicketResponseDto> CreateAsync(long createdByUserId, TicketCreateDto dto)
        {
            // Ensure required default status exists
            var statusNewId = await _statusRepo.GetQueryable() //query status table
                .Where(s => s.Name == "New") //filter new status
                .Select(s => s.Id) 
                .FirstOrDefaultAsync(); //get id or 0

            if (statusNewId == 0)
                throw new InvalidOperationException("Required status 'New' is not configured.");

            var ticket = new Ticket //create object,creating new ticket entity
            {
                DepartmentId = dto.DepartmentId, //Mapping DTO → Database entity.
                CategoryId = dto.CategoryId,
                PriorityId = dto.PriorityId,
                StatusId = statusNewId, // sets default values = New
                Title = dto.Title,
                Description = dto.Description,
                DueAt = dto.DueAt.HasValue
                    ? DateTime.SpecifyKind(dto.DueAt.Value.ToUniversalTime(), DateTimeKind.Utc)
                    : (DateTime?)null,
                CreatedByUserId = createdByUserId,
                CreatedAt = DateTime.UtcNow
            };

            await _ticketRepo.Add(ticket); //save to db the new ticket

            var created = await _ticketRepo.GetQueryable() //reload ticket with related tables
                .Include(t => t.Priority).Include(t => t.Status)
                .Include(t => t.Department).Include(t => t.Category)
                .Include(t => t.CreatedBy).Include(t => t.CurrentAssignee)
                .FirstAsync(t => t.Id == ticket.Id);

            return ToResponse(created); // convert to dto, ensures api never exposes entity directly for safety
        }

        public async Task<TicketResponseDto?> GetAsync(long id) //getting a ticket by id
        {
            var t = await _ticketRepo.GetQueryable()
                .Include(x => x.Priority).Include(x => x.Status)
                .Include(x => x.Department).Include(x => x.Category)
                .Include(x => x.CreatedBy).Include(x => x.CurrentAssignee)
                .FirstOrDefaultAsync(x => x.Id == id);

            return t == null ? null : ToResponse(t); //return null if not found
        }

        public async Task<PagedResult<TicketListItemDto>> QueryAsync(TicketQueryDto q) //filter,sort,pagination
        {
            var query = _ticketRepo.GetQueryable() //Builds base query,return Iqueryable
                .Include(t => t.Priority)
                .Include(t => t.Status)
                .Include(t => t.Department)
                .Include(t => t.Category)
                .Include(t => t.CurrentAssignee)
                .Include(t => t.CreatedBy)
                .AsQueryable();

            if (q.DepartmentId.HasValue) query = query.Where(t => t.DepartmentId == q.DepartmentId); //Applies filters ONLY when user requests it known as dynamic query building
            if (q.CategoryId.HasValue) query = query.Where(t => t.CategoryId == q.CategoryId);
            if (q.PriorityId.HasValue) query = query.Where(t => t.PriorityId == q.PriorityId);
            if (q.StatusId.HasValue) query = query.Where(t => t.StatusId == q.StatusId);
            if (q.CreatedByUserId.HasValue) query = query.Where(t => t.CreatedByUserId == q.CreatedByUserId);
            if (q.AssigneeUserId.HasValue) query = query.Where(t => t.CurrentAssigneeUserId == q.AssigneeUserId);
            if (q.CreatedFrom.HasValue) query = query.Where(t => t.CreatedAt >= q.CreatedFrom);
            if (q.CreatedTo.HasValue) query = query.Where(t => t.CreatedAt <= q.CreatedTo);

            query = (q.SortBy?.ToLower()) switch
            {
                "priority" => q.Desc
                    ? query.OrderBy(t => t.Priority!.Rank).ThenByDescending(t => t.CreatedAt)
                    : query.OrderByDescending(t => t.Priority!.Rank).ThenBy(t => t.CreatedAt),
                "dueat" => q.Desc ? query.OrderByDescending(t => t.DueAt) : query.OrderBy(t => t.DueAt),
                _ => q.Desc ? query.OrderByDescending(t => t.CreatedAt) : query.OrderBy(t => t.CreatedAt)
            };

            var total = await query.CountAsync();

            var items = await query.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize) //pagination
                .Select(t => new TicketListItemDto //select only necessary fields in dto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Priority = t.Priority!.Name,
                    PriorityRank = t.Priority!.Rank,
                    Status = t.Status!.Name,
                    IsClosedState = t.Status!.IsClosedState,
                    Department = t.Department != null ? t.Department.Name : null,
                    Category = t.Category != null ? t.Category.Name : null,
                    CreatedBy = t.CreatedBy!.DisplayName,
                    Assignee = t.CurrentAssignee != null ? t.CurrentAssignee.DisplayName : null,
                    CreatedAt = t.CreatedAt,
                    DueAt = t.DueAt,
                    FeedbackStatus = t.FeedbackStatus
                }).ToListAsync();

            return new PagedResult<TicketListItemDto>
            {
                Items = items,
                TotalCount = total,
                Page = q.Page,
                PageSize = q.PageSize
            };
        }

        public async Task<TicketResponseDto?> UpdateAsync(long id, TicketUpdateDto dto)
        {
            var t = await _ticketRepo.Get(id);
            if (t == null) return null;

            if (dto.DepartmentId.HasValue) t.DepartmentId = dto.DepartmentId;
            if (dto.CategoryId.HasValue) t.CategoryId = dto.CategoryId;
            if (dto.PriorityId.HasValue) t.PriorityId = dto.PriorityId.Value;
            if (!string.IsNullOrWhiteSpace(dto.Title)) t.Title = dto.Title!;
            if (dto.Description != null) t.Description = dto.Description;
            if (dto.DueAt.HasValue) t.DueAt = DateTime.SpecifyKind(dto.DueAt.Value.ToUniversalTime(), DateTimeKind.Utc);

            t.UpdatedAt = DateTime.UtcNow;
            await _ticketRepo.Update(id, t); //update ticket in db

            return await GetAsync(id);//fetch updated ticket again
        }

        public async Task<bool> UpdateStatusAsync(long id, long changedByUserId, TicketStatusUpdateDto dto)
        {
            var t = await _ticketRepo.GetQueryable()
                .Include(x => x.Status)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return false;

            // Ensure the target status exists
            var newStatus = await _statusRepo.GetQueryable()
                .FirstOrDefaultAsync(s => s.Id == dto.NewStatusId);
            if (newStatus == null)
                throw new KeyNotFoundException("Status not found.");

            // Block terminal status changes unless employee has approved
            if (newStatus.IsClosedState)
            {
                if (t.FeedbackStatus == "None")
                    throw new InvalidOperationException("FEEDBACK_REQUIRED");
                if (t.FeedbackStatus == "Pending")
                    throw new InvalidOperationException("FEEDBACK_PENDING");
                if (t.FeedbackStatus == "Declined")
                    throw new InvalidOperationException("FEEDBACK_DECLINED");
                // FeedbackStatus == "Approved" — allowed, reset after closing
                t.FeedbackStatus = "None";
                t.PendingCloseStatusId = null;
            }

            var old = t.StatusId;
            t.StatusId = dto.NewStatusId;
            t.UpdatedAt = DateTime.UtcNow;

            await _ticketRepo.Update(id, t);

            await _statusHistoryRepo.Add(new TicketStatusHistory
            {
                TicketId = id,
                OldStatusId = old,
                NewStatusId = dto.NewStatusId,
                ChangedByUserId = changedByUserId,
                ChangedAt = DateTime.UtcNow,
                Note = dto.Note
            });

            return true;
        }

        public async Task<TicketResponseDto?> RequestFeedbackAsync(long ticketId, int pendingStatusId)
        {
            var t = await _ticketRepo.Get(ticketId);
            if (t == null) return null;

            t.FeedbackStatus = "Pending";
            t.PendingCloseStatusId = pendingStatusId;
            t.UpdatedAt = DateTime.UtcNow;
            await _ticketRepo.Update(ticketId, t);

            return await GetAsync(ticketId);
        }

        public async Task<TicketResponseDto?> RespondFeedbackAsync(long ticketId, long employeeUserId, bool approved)
        {
            var t = await _ticketRepo.GetQueryable()
                .Include(x => x.Status)
                .FirstOrDefaultAsync(x => x.Id == ticketId);
            if (t == null) return null;

            // Only the ticket creator can respond
            if (t.CreatedByUserId != employeeUserId)
                throw new UnauthorizedAccessException("Only the ticket creator can respond to feedback.");

            if (t.FeedbackStatus != "Pending")
                throw new InvalidOperationException("No pending feedback request for this ticket.");

            t.FeedbackStatus = approved ? "Approved" : "Declined";
            t.UpdatedAt = DateTime.UtcNow;
            await _ticketRepo.Update(ticketId, t);

            // If approved, automatically apply the pending status change
            if (approved && t.PendingCloseStatusId.HasValue)
            {
                var old = t.StatusId;
                t.StatusId = t.PendingCloseStatusId.Value;
                t.FeedbackStatus = "None";
                t.PendingCloseStatusId = null;
                t.UpdatedAt = DateTime.UtcNow;
                await _ticketRepo.Update(ticketId, t);

                await _statusHistoryRepo.Add(new TicketStatusHistory
                {
                    TicketId = ticketId,
                    OldStatusId = old,
                    NewStatusId = t.StatusId,
                    ChangedByUserId = employeeUserId,
                    ChangedAt = DateTime.UtcNow,
                    Note = "Status updated after employee confirmed resolution."
                });
            }

            return await GetAsync(ticketId);
        }

        public async Task<TicketAssignmentResponseDto?> AssignAsync(long id, long assignedByUserId, TicketAssignRequestDto dto)
        {
            var t = await _ticketRepo.GetQueryable()
                .Include(x => x.Status)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return null;

            // Ensure assignee exists
            var assigneeExists = await _userRepo.GetQueryable()
                .AnyAsync(u => u.Id == dto.AssignedToUserId);
            if (!assigneeExists)
                throw new KeyNotFoundException("Assignee user not found.");

            var current = await _assignmentRepo.GetQueryable()
                .Where(a => a.TicketId == id && a.UnassignedAt == null)
                .OrderByDescending(a => a.AssignedAt)
                .FirstOrDefaultAsync();

            if (current != null)
            {
                current.UnassignedAt = DateTime.UtcNow;
                await _assignmentRepo.Update(current.Id, current);
            }

            var assignment = new TicketAssignment
            {
                TicketId = id,
                AssignedToUserId = dto.AssignedToUserId,
                AssignedByUserId = assignedByUserId,
                AssignedAt = DateTime.UtcNow,
                Note = dto.Note
            };
            await _assignmentRepo.Add(assignment);

            t.CurrentAssigneeUserId = dto.AssignedToUserId;
            await _ticketRepo.Update(t.Id, t);

            var inProgress = await _statusRepo.GetQueryable()
                .FirstOrDefaultAsync(s => s.Name == "In Progress");
            if (inProgress != null && t.StatusId != inProgress.Id)
            {
                var oldStatusId = t.StatusId;
                t.StatusId = inProgress.Id;
                await _ticketRepo.Update(t.Id, t);

                await _statusHistoryRepo.Add(new TicketStatusHistory
                {
                    TicketId = t.Id,
                    OldStatusId = oldStatusId,
                    NewStatusId = inProgress.Id,
                    ChangedByUserId = assignedByUserId,
                    ChangedAt = DateTime.UtcNow,
                    Note = "Status moved to In Progress on assignment"
                });
            }

            var assignee = await _userRepo.Get(dto.AssignedToUserId);

            return new TicketAssignmentResponseDto
            {
                Id = assignment.Id,
                TicketId = id,
                AssignedToUserId = dto.AssignedToUserId,
                AssignedToName = assignee?.DisplayName ?? "",
                AssignedByUserId = assignedByUserId,
                AssignedByName = "",
                AssignedAt = assignment.AssignedAt,
                UnassignedAt = assignment.UnassignedAt,
                Note = assignment.Note
            };
        }

        private static TicketResponseDto ToResponse(Ticket t) => new TicketResponseDto
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            DepartmentId = t.DepartmentId,
            DepartmentName = t.Department?.Name,
            CategoryId = t.CategoryId,
            CategoryName = t.Category?.Name,
            PriorityId = t.PriorityId,
            PriorityName = t.Priority?.Name ?? "",
            PriorityRank = t.Priority?.Rank ?? 0,
            StatusId = t.StatusId,
            StatusName = t.Status?.Name ?? "",
            IsClosedState = t.Status?.IsClosedState ?? false,
            CreatedByUserId = t.CreatedByUserId,
            CreatedByUserName = t.CreatedBy?.UserName ?? "",
            CurrentAssigneeUserId = t.CurrentAssigneeUserId,
            CurrentAssigneeUserName = t.CurrentAssignee?.UserName,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            DueAt = t.DueAt,
            FeedbackStatus = t.FeedbackStatus,
            PendingCloseStatusId = t.PendingCloseStatusId
        };

        public async Task<IReadOnlyList<TicketStatusHistoryDto>> GetStatusHistoryAsync(long ticketId)
        {
            var rows = await _statusHistoryRepo.GetQueryable()
                .Include(h => h.OldStatus)
                .Include(h => h.NewStatus)
                .Include(h => h.ChangedBy)
                .Where(h => h.TicketId == ticketId)
                .OrderByDescending(h => h.ChangedAt)
                .ToListAsync();

            var list = rows.Select(h => new TicketStatusHistoryDto
            {
                Id = h.Id,
                TicketId = h.TicketId,
                OldStatusId = h.OldStatusId,
                OldStatusName = h.OldStatus?.Name,
                NewStatusId = h.NewStatusId,
                NewStatusName = h.NewStatus?.Name ?? string.Empty,
                ChangedByUserId = h.ChangedByUserId,
                ChangedByUserName = h.ChangedBy?.DisplayName ?? string.Empty,
                ChangedAt = h.ChangedAt,
                Note = h.Note
            }).ToList();

            return list;
        }
    }
}