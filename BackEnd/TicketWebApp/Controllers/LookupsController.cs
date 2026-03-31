using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketWebApp.Interfaces;

namespace TicketWebApp.Controllers
{
    [ApiController]
    [Route("api/lookups")]
    [Authorize]
    public class LookupsController : ControllerBase
    {
        private readonly ILookupService _lookups;

        public LookupsController(ILookupService lookups)
        {
            _lookups = lookups;
        }

        // ----------------------------------------------------------
        [HttpGet("departments")]
        public async Task<IActionResult> GetDepartments()
        {
            try
            {
                var list = await _lookups.GetDepartmentsAsync();
                return Ok(list);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", detail = ex.Message });
            }
        }

        // ----------------------------------------------------------
        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            try
            {
                var list = await _lookups.GetRolesAsync();
                return Ok(list);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", detail = ex.Message });
            }
        }

        // ----------------------------------------------------------
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var list = await _lookups.GetCategoriesAsync();
                return Ok(list);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", detail = ex.Message });
            }
        }

        // ----------------------------------------------------------
        [HttpGet("priorities")]
        public async Task<IActionResult> GetPriorities()
        {
            try
            {
                var list = await _lookups.GetPrioritiesAsync();
                return Ok(list);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", detail = ex.Message });
            }
        }

        // ----------------------------------------------------------
        [HttpGet("statuses")]
        public async Task<IActionResult> GetStatuses()
        {
            try
            {
                var list = await _lookups.GetStatusesAsync();
                return Ok(list);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", detail = ex.Message });
            }
        }
    }
}