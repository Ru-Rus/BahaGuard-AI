using System.ComponentModel.DataAnnotations;
using BahaGuard.Api.Hubs;
using BahaGuard.Application.DTOs;
using BahaGuard.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;

namespace BahaGuard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ReportsController(
    IFloodReportService floodReportService,
    IHubContext<FloodAlertHub, IFloodAlertClient> hubContext,
    IWebHostEnvironment environment,
    ILogger<ReportsController> logger) : ControllerBase
{
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB limit

    /// <summary>
    /// Submit a flood incident report with GPS coordinates, Taglish description, and optional photo.
    /// Evaluated by AI and broadcast to nearby commuters in real time.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("ReportsPostPolicy")]
    [ProducesResponseType(typeof(FloodReportResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateReport(
        [FromForm] CreateReportFormRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        string? savedImageUrl = null;
        Stream? imageStream = null;
        string? mimeType = null;

        if (request.Photo != null && request.Photo.Length > 0)
        {
            if (request.Photo.Length > MaxFileSizeBytes)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid file size",
                    detail: "Uploaded photo exceeds the 10MB maximum limit.");
            }

            var extension = Path.GetExtension(request.Photo.FileName);
            if (string.IsNullOrEmpty(extension) || !AllowedImageExtensions.Contains(extension))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid file format",
                    detail: "Only JPG, PNG, and WEBP image formats are accepted.");
            }

            if (!AllowedMimeTypes.Contains(request.Photo.ContentType))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid content type",
                    detail: "File MIME type does not match supported image formats.");
            }

            // Save uploaded photo securely
            var uploadsFolder = Path.Combine(environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads"), "reports");
            Directory.CreateDirectory(uploadsFolder);

            var safeFileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsFolder, safeFileName);

            await using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await request.Photo.CopyToAsync(fileStream, cancellationToken);
            }

            savedImageUrl = $"/uploads/reports/{safeFileName}";
            imageStream = request.Photo.OpenReadStream();
            mimeType = request.Photo.ContentType;
        }

        try
        {
            var dto = new CreateFloodReportDto(
                RawDescription: request.RawDescription,
                Latitude: request.Latitude,
                Longitude: request.Longitude,
                ReporterAlias: request.ReporterAlias,
                Barangay: request.Barangay,
                City: string.IsNullOrWhiteSpace(request.City) ? "Manila" : request.City.Trim(),
                ImageUrl: savedImageUrl
            );

            var createdReport = await floodReportService.CreateReportAsync(
                dto,
                imageStream,
                mimeType,
                cancellationToken);

            // Broadcast to SignalR city alert subscribers
            var cityGroup = createdReport.City.Trim().ToLowerInvariant();
            await hubContext.Clients.Group(cityGroup).ReceiveNewFloodReport(createdReport);

            return CreatedAtAction(nameof(GetById), new { id = createdReport.Id }, createdReport);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Flood report validation or AI rejection: {Message}", ex.Message);
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Report rejected by flood analyzer",
                detail: ex.Message);
        }
        finally
        {
            if (imageStream != null)
            {
                await imageStream.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Find active flood reports within a radius (in meters) of GPS coordinates, with optional vehicle passability filter.
    /// </summary>
    [HttpGet("nearby")]
    [ProducesResponseType(typeof(IReadOnlyList<FloodReportResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetNearby(
        [FromQuery, Range(-90.0, 90.0)] double lat,
        [FromQuery, Range(-180.0, 180.0)] double lng,
        [FromQuery, Range(10.0, 50000.0)] double radiusMeters = 1000,
        [FromQuery] string? vehicleType = null,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var query = new NearbyReportsQuery(
            Latitude: lat,
            Longitude: lng,
            RadiusMeters: radiusMeters,
            VehicleType: vehicleType
        );

        var reports = await floodReportService.GetNearbyReportsAsync(query, cancellationToken);
        return Ok(reports);
    }

    /// <summary>
    /// Retrieve a specific flood report by its unique ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FloodReportResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var report = await floodReportService.GetByIdAsync(id, cancellationToken);
        if (report == null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Flood report with ID '{id}' was not found.");
        }

        return Ok(report);
    }
}

public class CreateReportFormRequest
{
    [Required(ErrorMessage = "Description is required.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Description must be between 3 and 500 characters.")]
    public string RawDescription { get; set; } = string.Empty;

    [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90 degrees.")]
    public double Latitude { get; set; }

    [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180 degrees.")]
    public double Longitude { get; set; }

    [StringLength(50, ErrorMessage = "Reporter alias cannot exceed 50 characters.")]
    public string? ReporterAlias { get; set; }

    [StringLength(100, ErrorMessage = "Barangay cannot exceed 100 characters.")]
    public string? Barangay { get; set; }

    [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string City { get; set; } = "Manila";

    public IFormFile? Photo { get; set; }
}
