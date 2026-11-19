using BahaGuard.Domain.Entities;
using Xunit;

namespace BahaGuard.UnitTests;

public class FloodReportEntityTests
{
    [Fact]
    public void Create_ShouldInitializeSpatialPointWithCorrectSridAndCoordinates()
    {
        // Manila coordinates: Lat 14.6091, Lng 120.9903 (España Blvd)
        double latitude = 14.6091;
        double longitude = 120.9903;

        var report = FloodReport.Create(
            rawDescription: "España baha",
            extractedLocationName: "España Blvd, Manila",
            latitude: latitude,
            longitude: longitude,
            estimatedDepthInches: 12,
            depthCategory: "Knee",
            passableMotorcycle: false,
            passableSedan: false,
            passableSuvJeepney: true,
            aiConfidenceScore: 0.95m,
            reporterAlias: "ManilaRider"
        );

        Assert.NotNull(report.Coordinates);
        Assert.Equal(4326, report.Coordinates.SRID);
        // NetTopologySuite: X is Longitude, Y is Latitude
        Assert.Equal(longitude, report.Coordinates.X);
        Assert.Equal(latitude, report.Coordinates.Y);
        Assert.Equal("Active", report.Status);
        Assert.True(report.ExpiresAt > report.CreatedAt);
    }
}
