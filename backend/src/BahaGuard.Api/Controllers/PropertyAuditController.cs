using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace BahaGuard.Api.Controllers;

[ApiController]
[Route("api/property-audit")]
[Produces("application/json")]
public class PropertyAuditController : ControllerBase
{
    /// <summary>
    /// Renter & Dorm Flood Risk Due Diligence Audit (Phase 4 Roadmap).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult GetAudit(
        [FromQuery] string? address,
        [FromQuery, Range(-90.0, 90.0)] double? lat,
        [FromQuery, Range(-180.0, 180.0)] double? lng)
    {
        return Ok(new
        {
            Address = address ?? "Metro Manila",
            Latitude = lat,
            Longitude = lng,
            Status = "Upcoming in Phase 4",
            Message = "Historical spatial flood risk analysis and AI tenant due-diligence report will be activated in Phase 4."
        });
    }
}
