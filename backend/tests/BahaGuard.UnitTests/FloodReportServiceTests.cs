using BahaGuard.Application.DTOs;
using BahaGuard.Application.Interfaces;
using BahaGuard.Application.Services;
using BahaGuard.Domain.Entities;
using BahaGuard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BahaGuard.UnitTests;

public class FloodReportServiceTests
{
    private static BahaGuardDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<BahaGuardDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new BahaGuardDbContext(options);
    }

    [Fact]
    public async Task CreateReportAsync_ShouldSaveReportAndReturnDto()
    {
        using var context = CreateInMemoryContext();

        var mockGemini = new Mock<IGeminiVisionService>();
        mockGemini.Setup(g => g.AnalyzeIncidentAsync(
            It.IsAny<string>(),
            It.IsAny<Stream?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FloodAnalysisResultDto(
                IsValidFloodReport: true,
                RejectionReason: null,
                NormalizedLocation: "España Blvd cor. Lacson Ave, Manila",
                EstimatedDepthInches: 12,
                DepthCategory: "Knee",
                Passability: new PassabilityDto(false, false, true),
                ConfidenceScore: 0.95m,
                CommuterAdvisory: "Avoid light vehicles."
            ));

        var service = new FloodReportService(
            context,
            mockGemini.Object,
            NullLogger<FloodReportService>.Instance);

        var request = new CreateFloodReportDto(
            RawDescription: "Baha sa españa lacson",
            Latitude: 14.6091,
            Longitude: 120.9903,
            ReporterAlias: "JuanRider",
            Barangay: "Sampaloc",
            City: "Manila"
        );

        var result = await service.CreateReportAsync(request);

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("España Blvd cor. Lacson Ave, Manila", result.ExtractedLocationName);
        Assert.Equal(14.6091, result.Latitude);
        Assert.Equal(120.9903, result.Longitude);
        Assert.False(result.PassableMotorcycle);
        Assert.False(result.PassableSedan);
        Assert.True(result.PassableSuvJeepney);

        // Verify saved to database
        var savedInDb = await context.FloodReports.FirstOrDefaultAsync(r => r.Id == result.Id);
        Assert.NotNull(savedInDb);
        Assert.Equal("Active", savedInDb.Status);
    }

    [Fact]
    public async Task CreateReportAsync_ShouldThrowWhenAiRejectsReport()
    {
        using var context = CreateInMemoryContext();

        var mockGemini = new Mock<IGeminiVisionService>();
        mockGemini.Setup(g => g.AnalyzeIncidentAsync(
            It.IsAny<string>(),
            It.IsAny<Stream?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FloodAnalysisResultDto(
                IsValidFloodReport: false,
                RejectionReason: "Not flood related.",
                NormalizedLocation: "Unknown",
                EstimatedDepthInches: 0,
                DepthCategory: "Dry",
                Passability: new PassabilityDto(true, true, true),
                ConfidenceScore: 0.99m,
                CommuterAdvisory: "Rejected."
            ));

        var service = new FloodReportService(
            context,
            mockGemini.Object,
            NullLogger<FloodReportService>.Instance);

        var request = new CreateFloodReportDto(
            RawDescription: "Just random sunny day photo",
            Latitude: 14.6091,
            Longitude: 120.9903
        );

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateReportAsync(request));

        Assert.Contains("Not flood related", exception.Message);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnReportWhenFound()
    {
        using var context = CreateInMemoryContext();

        var report = FloodReport.Create(
            rawDescription: "Taft Pedro Gil baha",
            extractedLocationName: "Taft Ave cor. Pedro Gil, Manila",
            latitude: 14.5775,
            longitude: 120.9880,
            estimatedDepthInches: 8,
            depthCategory: "HalfTire",
            passableMotorcycle: false,
            passableSedan: true,
            passableSuvJeepney: true,
            aiConfidenceScore: 0.90m
        );

        context.FloodReports.Add(report);
        await context.SaveChangesAsync();

        var mockGemini = new Mock<IGeminiVisionService>();
        var service = new FloodReportService(
            context,
            mockGemini.Object,
            NullLogger<FloodReportService>.Instance);

        var found = await service.GetByIdAsync(report.Id);

        Assert.NotNull(found);
        Assert.Equal(report.Id, found.Id);
        Assert.Equal(14.5775, found.Latitude);
        Assert.Equal(120.9880, found.Longitude);
    }
}
