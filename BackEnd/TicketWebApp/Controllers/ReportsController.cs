using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketWebApp.Interfaces;

namespace TicketWebApp.Controllers
{
    [ApiController]
    [Route("api/reports")]
    [Authorize] // All endpoints require authentication
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reports;

        public ReportsController(IReportService reports)
        {
            _reports = reports;
        }

        private long? CurrentUserId()
        {
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return long.TryParse(id, out var uid) ? uid : (long?)null;
        }

        // -----------------------------------------------------------
        // GET api/reports/tickets/summary
        // Admin/Agent => global; Employee => restricted to own tickets
        // -----------------------------------------------------------
        [HttpGet("tickets/summary")]
        [Authorize(Roles = "Admin,Agent,Employee")]
        public async Task<IActionResult> GetTicketSummary()
        {
            try
            {
                var currentUserId = CurrentUserId();
                if (currentUserId is null)
                    return Unauthorized();

                var isAdmin = User.IsInRole("Admin");
                var isAgent = User.IsInRole("Agent");
                var isEmployee = User.IsInRole("Employee");

                var summary = await _reports.GetTicketSummaryAsync(
                    currentUserId.Value,
                    isAdmin,
                    isAgent,
                    isEmployee
                );

                return Ok(summary);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", detail = ex.Message });
            }
        }

        // GET api/reports/agents/workload — Admin only
        [HttpGet("agents/workload")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAgentWorkload()
        {
            try
            {
                var workload = await _reports.GetAgentWorkloadAsync();
                return Ok(workload);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", detail = ex.Message });
            }
        }
    }
}