using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketWebApp.Interfaces;
using TicketWebApp.Models.DTOs;
using static TicketWebApp.Models.DTOs.AuditLogDtos;

namespace TicketWebApp.Controllers
{
    [ApiController]
    [Route("api/auditlogs")]
    [Authorize(Roles = "Admin")]
    public class AuditLogsController : ControllerBase
    {
        private readonly IAuditLogService _audit;

        public AuditLogsController(IAuditLogService audit)
        {
            _audit = audit;
        }

        [HttpGet("recent")]
        public async Task<IActionResult> Recent([FromQuery] int take = 100)
        {
            var result = await _audit.GetRecentAsync(take);
            return Ok(result);
        }

        [HttpPost("query")]
        public async Task<IActionResult> Query([FromBody] AuditLogQueryDto query)
        {
            var result = await _audit.QueryAsync(query);
            return Ok(result);
        }
    }
}