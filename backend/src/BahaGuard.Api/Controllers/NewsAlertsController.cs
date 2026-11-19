using BahaGuard.Application.DTOs;
using BahaGuard.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BahaGuard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class NewsAlertsController(
    INewsAlertIngestionService ingestionService,
    ILogger<NewsAlertsController> logger) : ControllerBase
{
    /// <summary>
    /// Triggers an immediate news and official disaster feed ingestion cycle.
    /// Extracts flood incidents via Gemini 2.5 Flash, merges duplicates spatially, and broadcasts live pins.
    /// </summary>
    [HttpPost("ingest")]
    [ProducesResponseType(typeof(NewsIngestionResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> IngestFeeds(CancellationToken cancellationToken)
    {
        logger.LogInformation("Manual news feed ingestion cycle triggered via API.");
        var result = await ingestionService.IngestLatestFeedsAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Ingests a direct emergency alert string (e.g., MMDA bulletin, PAGASA warning, or webhook payload).
    /// </summary>
    [HttpPost("ingest-text")]
    [ProducesResponseType(typeof(FloodReportResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> IngestDirectAlert(
        [FromBody] IngestAlertRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RawText))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid alert text",
                detail: "Raw alert text cannot be empty.");
        }

        var report = await ingestionService.IngestDirectAlertAsync(request, cancellationToken);
        if (report == null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Alert not classified as active street flooding",
                detail: "The provided text was evaluated by AI and does not report an active street flood on a recognizable Metro Manila corridor.");
        }

        return Ok(report);
    }

    /// <summary>
    /// Retrieves recent official government and news flood incidents currently active on the map.
    /// </summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(IReadOnlyList<FloodReportResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentOfficialAlerts(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var alerts = await ingestionService.GetRecentOfficialAlertsAsync(limit, cancellationToken);
        return Ok(alerts);
    }
}
