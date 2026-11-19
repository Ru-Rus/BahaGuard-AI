using BahaGuard.Application.DTOs;
using BahaGuard.Domain.Entities;
using BahaGuard.Infrastructure.AI;
using BahaGuard.Infrastructure.Data;
using BahaGuard.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Xunit;

namespace BahaGuard.UnitTests;

public class PropertyAuditServiceTests
{
    private static readonly GeometryFactory Srid4326Factory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    private static BahaGuardDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<BahaGuardDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new BahaGuardDbContext(options);
    }

    [Fact]
    public async Task AuditPropertyAsync_WithSeededHotspot_ShouldReturnAccurateMetrics()
    {
        using var context = CreateInMemoryContext();

        // Seed 3 historical reports around España UST (14.6091, 120.9903)
        var espanaPoint = Srid4326Factory.CreatePoint(new Coordinate(120.9903, 14.6091));
        var baseTime = DateTimeOffset.UtcNow.AddMonths(-3);

        context.FloodReports.AddRange(
            new FloodReport
            {
                Id = Guid.NewGuid(),
                ExtractedLocationName = "España Blvd cor. Lacson Ave",
                RawDescription = "Waist deep flood",
                Coordinates = espanaPoint,
                EstimatedDepthInches = 28,
                DepthCategory = "Waist",
                PassableMotorcycle = false,
                PassableSedan = false,
                PassableSuvJeepney = false,
                AiConfidenceScore = 0.95m,
                City = "Manila",
                Status = "Resolved",
                CreatedAt = baseTime,
                ExpiresAt = baseTime.AddHours(4)
            },
            new FloodReport
            {
                Id = Guid.NewGuid(),
                ExtractedLocationName = "España Blvd near UST gate",
                RawDescription = "Knee deep flood",
                Coordinates = espanaPoint,
                EstimatedDepthInches = 16,
                DepthCategory = "Knee",
                PassableMotorcycle = false,
                PassableSedan = false,
                PassableSuvJeepney = true,
                AiConfidenceScore = 0.90m,
                City = "Manila",
                Status = "Resolved",
                CreatedAt = baseTime.AddDays(10),
                ExpiresAt = baseTime.AddDays(10).AddHours(3)
            }
        );
        await context.SaveChangesAsync();

        var geminiOpts = Options.Create(new GeminiOptions()); // Empty API key -> rule-based fallback
        var service = new PropertyAuditService(
            context,
            new HttpClient(),
            geminiOpts,
            NullLogger<PropertyAuditService>.Instance);

        var result = await service.AuditPropertyAsync(new PropertyAuditQuery(
            Address: "839 Lacson Ave near UST, Sampaloc, Manila",
            Latitude: null,
            Longitude: null,
            RadiusMeters: 500));

        Assert.NotNull(result);
        Assert.Equal(2, result.HistoricalIncidentsCount);
        Assert.Equal(22, result.AverageDepthInches);
        Assert.Equal(28, result.MaxDepthInches);
        Assert.True(result.RiskScore >= 6, $"RiskScore was {result.RiskScore}");
        Assert.True(result.VehicleVulnerability.MotorcycleHighRisk);
        Assert.True(result.VehicleVulnerability.SedanHighRisk);
        Assert.False(string.IsNullOrWhiteSpace(result.DueDiligenceReport.ExecutiveSummary));
        Assert.False(string.IsNullOrWhiteSpace(result.DueDiligenceReport.GroundFloorRiskAdvice));
    }

    [Fact]
    public async Task AuditPropertyAsync_WhenNoReportsInRadius_ShouldReturnLowRiskScore()
    {
        using var context = CreateInMemoryContext();

        var geminiOpts = Options.Create(new GeminiOptions());
        var service = new PropertyAuditService(
            context,
            new HttpClient(),
            geminiOpts,
            NullLogger<PropertyAuditService>.Instance);

        // Coordinates in safe/dry area with 0 reports
        var result = await service.AuditPropertyAsync(new PropertyAuditQuery(
            Address: "BGC Taguig High Street",
            Latitude: 14.5500,
            Longitude: 121.0500,
            RadiusMeters: 500));

        Assert.NotNull(result);
        Assert.Equal(0, result.HistoricalIncidentsCount);
        Assert.Equal(0, result.AverageDepthInches);
        Assert.Equal(1, result.RiskScore);
        Assert.Equal("Low", result.RiskLevel);
        Assert.Contains("Low Flood Risk", result.DueDiligenceReport.ExecutiveSummary);
    }

    [Fact]
    public async Task AuditPropertyAsync_ShouldResolveKnownManilaLandmarks()
    {
        using var context = CreateInMemoryContext();

        var geminiOpts = Options.Create(new GeminiOptions());
        var service = new PropertyAuditService(
            context,
            new HttpClient(),
            geminiOpts,
            NullLogger<PropertyAuditService>.Instance);

        var result = await service.AuditPropertyAsync(new PropertyAuditQuery(
            Address: "Near DLSU Taft Avenue dorm",
            Latitude: null,
            Longitude: null));

        Assert.Contains("Taft Ave", result.ResolvedLocationName);
        Assert.Equal(14.5702, result.Latitude, 3);
    }
}
