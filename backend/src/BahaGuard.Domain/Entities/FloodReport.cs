using NetTopologySuite.Geometries;

namespace BahaGuard.Domain.Entities;

public class FloodReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? ReporterAlias { get; set; }
    public string RawDescription { get; set; } = string.Empty;
    public string ExtractedLocationName { get; set; } = string.Empty;
    public string? Barangay { get; set; }
    public string City { get; set; } = "Manila";

    /// <summary>
    /// Spatial Point (SRID 4326, WGS84 GPS coordinates: X=Longitude, Y=Latitude).
    /// </summary>
    public Point Coordinates { get; set; } = null!;

    public int EstimatedDepthInches { get; set; }
    public string DepthCategory { get; set; } = "Gutter";
    public bool PassableMotorcycle { get; set; }
    public bool PassableSedan { get; set; }
    public bool PassableSuvJeepney { get; set; }
    public decimal AiConfidenceScore { get; set; }

    public string? ImageUrl { get; set; }
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }

    public static FloodReport Create(
        string rawDescription,
        string extractedLocationName,
        double latitude,
        double longitude,
        int estimatedDepthInches,
        string depthCategory,
        bool passableMotorcycle,
        bool passableSedan,
        bool passableSuvJeepney,
        decimal aiConfidenceScore,
        string? reporterAlias = null,
        string? barangay = null,
        string city = "Manila",
        string? imageUrl = null,
        TimeSpan? lifetime = null)
    {
        var now = DateTimeOffset.UtcNow;
        var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        // In GIS/WGS84, X is Longitude and Y is Latitude
        var coordinates = geometryFactory.CreatePoint(new Coordinate(longitude, latitude));

        return new FloodReport
        {
            Id = Guid.NewGuid(),
            ReporterAlias = string.IsNullOrWhiteSpace(reporterAlias) ? null : reporterAlias.Trim(),
            RawDescription = rawDescription.Trim(),
            ExtractedLocationName = extractedLocationName.Trim(),
            Barangay = string.IsNullOrWhiteSpace(barangay) ? null : barangay.Trim(),
            City = string.IsNullOrWhiteSpace(city) ? "Manila" : city.Trim(),
            Coordinates = coordinates,
            EstimatedDepthInches = estimatedDepthInches,
            DepthCategory = depthCategory,
            PassableMotorcycle = passableMotorcycle,
            PassableSedan = passableSedan,
            PassableSuvJeepney = passableSuvJeepney,
            AiConfidenceScore = aiConfidenceScore,
            ImageUrl = imageUrl,
            Status = "Active",
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime ?? TimeSpan.FromHours(3))
        };
    }
}
