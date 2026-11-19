using System.ComponentModel.DataAnnotations;
using BahaGuard.Application.DTOs;
using BahaGuard.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BahaGuard.Api.Controllers;

[ApiController]
[Route("api/property-audit")]
[Produces("application/json")]
public class PropertyAuditController(
    IPropertyAuditService auditService,
    ILogger<PropertyAuditController> logger) : ControllerBase
{
    /// <summary>
    /// Renter & Dorm Flood Risk Due Diligence Audit.
    /// Aggregates historical spatial database metrics within a 500m radius of a prospective rental/dorm,
    /// and synthesizes an actionable Tenant Due-Diligence Report via Gemini 2.5 Flash.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PropertyAuditResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAudit(
        [FromQuery] string? address,
        [FromQuery, Range(-90.0, 90.0)] double? lat,
        [FromQuery, Range(-180.0, 180.0)] double? lng,
        [FromQuery, Range(10.0, 50000.0)] double radiusMeters = 500,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var query = new PropertyAuditQuery(
                Address: address,
                Latitude: lat,
                Longitude: lng,
                RadiusMeters: radiusMeters);

            var auditResult = await auditService.AuditPropertyAsync(query, cancellationToken);
            return Ok(auditResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to perform property audit for address '{Address}'", address);
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Property audit failed",
                detail: ex.Message);
        }
    }
}
