using System.ComponentModel.DataAnnotations;

namespace BahaGuard.Application.DTOs;

public record CreateFloodReportDto(
    [Required(ErrorMessage = "Raw description is required.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Description must be between 3 and 500 characters.")]
    string RawDescription,

    [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90 degrees.")]
    double Latitude,

    [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180 degrees.")]
    double Longitude,

    [StringLength(50, ErrorMessage = "Reporter alias cannot exceed 50 characters.")]
    string? ReporterAlias = null,

    [StringLength(100, ErrorMessage = "Barangay cannot exceed 100 characters.")]
    string? Barangay = null,

    [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    string City = "Manila",

    string? ImageUrl = null
);

public record NearbyReportsQuery(
    [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90 degrees.")]
    double Latitude,

    [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180 degrees.")]
    double Longitude,

    [Range(10.0, 50000.0, ErrorMessage = "Radius must be between 10 meters and 50,000 meters.")]
    double RadiusMeters = 1000,

    string? VehicleType = null
);
