using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketWebApp.Interfaces;
using TicketWebApp.Models.DTOs;

namespace TicketWebApp.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _users;

        public UsersController(IUserService users)
        {
            _users = users;
        }

        // ---------------------------------------------------------
        // GET api/users/{id}
        // ---------------------------------------------------------
        [HttpGet("{id:long}")]
        [Authorize]
        public async Task<IActionResult> Get(long id)
        {
            try
            {
                var u = await _users.GetAsync(id);
                return Ok(u); // service already throws if not found
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    return NotFound(new { error = ex.Message });

                return StatusCode(500, new { error = "Server error", detail = ex.Message });
            }
        }

        // ---------------------------------------------------------
        // GET api/users/agents?departmentId=1
        // Admin, Agent
        // ---------------------------------------------------------
        [HttpGet("agents")]
        [Authorize(Roles = "Admin,Agent")]
        public async Task<IActionResult> GetAgents([FromQuery] int? departmentId = null)
        {
            try
            {
                var list = await _users.GetAgentsAsync(departmentId);
                return Ok(list);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("No agents", StringComparison.OrdinalIgnoreCase))
                    return NotFound(new { error = ex.Message });

                return StatusCode(500, new { error = "Server error", detail = ex.Message });
            }
        }

        // ---------------------------------------------------------
        // GET api/users/me
        // ---------------------------------------------------------
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMe()
        {
            try
            {
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!long.TryParse(idClaim, out var myId))
                    return Unauthorized();

                var u = await _users.GetAsync(myId);
                return Ok(u);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    return NotFound(new { error = ex.Message });

                return StatusCode(500, new { error = "Server error", detail = ex.Message });
            }
        }

        // ---------------------------------------------------------
        // PUT api/users/me
        // ---------------------------------------------------------
        [HttpPut("me")]
        [Authorize]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest(new { error = "No data provided." });

                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!long.TryParse(idClaim, out var myId))
                    return Unauthorized();

                var updated = await _users.UpdateProfileAsync(myId, model);
                return Ok(updated);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    return NotFound(new { error = ex.Message });

                return StatusCode(500, new { error = "Server error", detail = ex.Message });
            }
        }
        // ✅ GET: All users (Admin only)
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _users.GetAllAsync();
            return Ok(users);
        }
    }
}