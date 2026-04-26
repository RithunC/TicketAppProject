using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketWebApp.Interfaces;
using TicketWebApp.Models.DTOs;
using static TicketWebApp.Models.DTOs.TicketAssignmentDtos;
using static TicketWebApp.Models.DTOs.TicketDtos;
namespace TicketWebApp.Controllers
{
    [Route("api/tickets")] //baseurl
    [ApiController] //auto validation and binding
    [Authorize] //all endpoints require login
    public class TicketsController : ControllerBase
    {
        private readonly ITicketService _tickets;
        private readonly IAutoAssignmentService _autoAssign;

        public TicketsController(ITicketService tickets, IAutoAssignmentService autoAssign)
        {
            _tickets = tickets;
            _autoAssign = autoAssign;
        }

        private long? CurrentUserId() //extracts userid from jwt token
        {
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value; //grt logged in userid
            return long.TryParse(id, out var uid) ? uid : (long?)null; //conversion succeeds return uid
        }

        // --------------------------------------------------------------
        // POST: Create Ticket  (Employee only)
        // --------------------------------------------------------------
        [HttpPost]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Create(
            [FromBody] TicketCreateDto dto,
            [FromHeader(Name = "X-User-Id")] long? xUserId = null)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var currentUserId = xUserId ?? CurrentUserId();
                if (currentUserId is null)
                    return Unauthorized();

                var created = await _tickets.CreateAsync(currentUserId.Value, dto);

                await _autoAssign.AutoAssignAsync(created.Id, currentUserId.Value,
                    new TicketAutoAssignRequestDto
                    {
                        DepartmentId = created.DepartmentId,
                        CategoryId = created.CategoryId,
                        Note = "Auto-assign on ticket create"
                    });

                return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", detail = ex.Message });
            }
        }

        // --------------------------------------------------------------
        // GET: Get a single ticket
        // --------------------------------------------------------------
        [HttpGet("{id:long}")]
        public async Task<IActionResult> Get(long id)
        {
            try
            {
                var t = await _tickets.GetAsync(id);
                return t == null ? NotFound() : Ok(t);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", detail = ex.Message });
            }
        }

        // --------------------------------------------------------------
        // POST: Query tickets (Admin/Agent)
        // --------------------------------------------------------------
        [HttpPost("query")]
        [Authorize(Roles = "Admin,Agent")]
        public async Task<IActionResult> Query([FromBody] TicketQueryDto query)
        {
            try
            {
                var result = await _tickets.QueryAsync(query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", detail = ex.Message });
            }
        }

        // --------------------------------------------------------------
        // PATCH: Update ticket
        // --------------------------------------------------------------
        [HttpPatch("{id:long}")]
        [Authorize(Roles = "Employee,Agent,Admin")]
        public async Task<IActionResult> Update(long id, [FromBody] TicketUpdateDto dto)
        {
            try
            {
                var existing = await _tickets.GetAsync(id);
                if (existing == null)
                    return NotFound();

                var currentUserId = CurrentUserId();
                if (currentUserId is null)
                    return Unauthorized();

                var isEmployee = User.IsInRole("Employee");

                if (isEmployee)
                {
                    if (existing.CreatedByUserId != currentUserId.Value)
                        return Forbid();

                    // employees restricted fields
                    dto.DepartmentId = null;
                    dto.CategoryId = null;
                }

                var updated = await _tickets.UpdateAsync(id, dto);
                return updated == null ? NotFound() : Ok(updated);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", detail = ex.Message });
            }
        }

        // --------------------------------------------------------------
        // POST: Manual assignment (Admin/Agent)
        // --------------------------------------------------------------
        [HttpPost("{id:long}/assign")]
        [Authorize(Roles = "Admin,Agent")]
        public async Task<IActionResult> Assign(
            long id,
            [FromBody] TicketAssignRequestDto dto,
            [FromHeader(Name = "X-User-Id")] long? xUserId = null)
        {
            try
            {
                var currentUserId = xUserId ?? CurrentUserId();
                if (currentUserId is null)
                    return Unauthorized();

                var res = await _tickets.AssignAsync(id, currentUserId.Value, dto);
                return res == null ? NotFound() : Ok(res);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", detail = ex.Message });
            }
        }

        // --------------------------------------------------------------
        // POST: Auto-assign (Admin/Agent)
        // --------------------------------------------------------------
        [HttpPost("{id:long}/autoAssign")]
        [Authorize(Roles = "Admin,Agent")]
        public async Task<IActionResult> AutoAssign(
            long id,
            [FromBody] TicketAutoAssignRequestDto dto,
            [FromHeader(Name = "X-User-Id")] long? xUserId = null)
        {
            try
            {
                var currentUserId = xUserId ?? CurrentUserId();
                if (currentUserId is null)
                    return Unauthorized();

                var res = await _autoAssign.AutoAssignAsync(id, currentUserId.Value, dto);
                return res == null ? NotFound(new { error = "No eligible agent found" }) : Ok(res);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", detail = ex.Message });
            }
        }

        // --------------------------------------------------------------
        // POST: Change Status (Admin/Agent)
        // --------------------------------------------------------------
        [HttpPost("{id:long}/status")]
        [Authorize(Roles = "Admin,Agent")]
        public async Task<IActionResult> UpdateStatus(
            long id,
            [FromBody] TicketStatusUpdateDto dto,
            [FromHeader(Name = "X-User-Id")] long? xUserId = null)
        {
            try
            {
                var currentUserId = xUserId ?? CurrentUserId();
                if (currentUserId is null)
                    return Unauthorized();

                var ok = await _tickets.UpdateStatusAsync(id, currentUserId.Value, dto);
                return ok ? Ok() : NotFound();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                // Return structured error codes the frontend can act on
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", detail = ex.Message });
            }
        }

        // --------------------------------------------------------------
        // POST: Request employee feedback before closing (Admin/Agent)
        // --------------------------------------------------------------
        [HttpPost("{id:long}/request-feedback")]
        [Authorize(Roles = "Admin,Agent")]
        public async Task<IActionResult> RequestFeedback(long id, [FromBody] RequestFeedbackDto dto)
        {
            try
            {
                var result = await _tickets.RequestFeedbackAsync(id, dto.PendingStatusId);
                return result == null ? NotFound() : Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", detail = ex.Message });
            }
        }

        // --------------------------------------------------------------
        // POST: Employee responds to feedback request
        // --------------------------------------------------------------
        [HttpPost("{id:long}/feedback-response")]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> RespondFeedback(long id, [FromBody] FeedbackResponseDto dto)
        {
            try
            {
                var currentUserId = CurrentUserId();
                if (currentUserId is null) return Unauthorized();

                var result = await _tickets.RespondFeedbackAsync(id, currentUserId.Value, dto.Approved);
                return result == null ? NotFound() : Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", detail = ex.Message });
            }
        }

        // --------------------------------------------------------------
        // GET: Status History
        // --------------------------------------------------------------
        [HttpGet("{id:long}/history")]
        [Authorize(Roles = "Admin,Agent,Employee")]
        public async Task<IActionResult> GetHistory(long id)
        {
            try
            {
                var ticket = await _tickets.GetAsync(id);
                if (ticket == null)
                    return NotFound();

                var currentUserId = CurrentUserId();
                if (currentUserId is null)
                    return Unauthorized();

                var isEmployee = User.IsInRole("Employee");
                var isAgent = User.IsInRole("Agent");
                var isAdmin = User.IsInRole("Admin");

                if (isEmployee && ticket.CreatedByUserId != currentUserId.Value)
                    return Forbid();

                var history = await _tickets.GetStatusHistoryAsync(id);
                return Ok(history);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", detail = ex.Message });
            }
        }
        // ✅ GET: All tickets created by an employee
        [HttpGet("employee/{employeeId:long}")]
        [Authorize(Roles = "Admin,Agent,Employee")]
        public async Task<IActionResult> GetTicketsByEmployee(long employeeId)
        {
            var query = new TicketQueryDto
            {
                CreatedByUserId = employeeId,
                Page = 1,
                PageSize = 5000
            };

            var result = await _tickets.QueryAsync(query);
            return Ok(result);
        }
        // ✅ GET: All tickets assigned to an agent
        [HttpGet("agent/{agentId:long}")]
        [Authorize(Roles = "Admin,Agent")]
        public async Task<IActionResult> GetTicketsAssignedToAgent(long agentId)
        {
            var query = new TicketQueryDto
            {
                AssigneeUserId = agentId,
                Page = 1,
                PageSize = 5000
            };

            var result = await _tickets.QueryAsync(query);
            return Ok(result);
        }
    }
}