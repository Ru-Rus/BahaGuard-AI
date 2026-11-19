using BahaGuard.Application.DTOs;
using BahaGuard.Application.Interfaces;
using BahaGuard.Domain.Entities;
using BahaGuard.Infrastructure.Data;
using BahaGuard.Infrastructure.Options;
using BahaGuard.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BahaGuard.UnitTests;

public class NewsAlertIngestionServiceTests
{
    private static BahaGuardDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<BahaGuardDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new BahaGuardDbContext(options);
    }

    private static IOptions<NewsFeedOptions> CreateOptions(double deduplicationRadius = 250.0)
    {
        return Microsoft.Extensions.Options.Options.Create(new NewsFeedOptions
        {
            DeduplicationRadiusMeters = deduplicationRadius,
            AutoIngestionEnabled = true,
            IncludeMockFeedsInDevelopment = false
        });
    }

    [Fact]
    public async Task IngestLatestFeedsAsync_WhenColdStart_ShouldCreateNewOfficialReports()
    {
        using var context = CreateInMemoryContext();

        var mockReader = new Mock<INewsFeedReader>();
        mockReader.Setup(r => r.FetchLatestAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new NewsAlertItem(
                    Title: "MMDA Alert: Gutter-deep flood along Taft Ave Pedro Gil",
                    Description: "Gutter-deep flood (4 inches) reported along Taft Avenue corner Pedro Gil, Manila.",
                    Source: "Official MMDA Advisory",
                    PublishedAt: DateTimeOffset.UtcNow
                )
            ]);

        var mockExtractor = new Mock<INewsFloodExtractor>();
        mockExtractor.Setup(e => e.ExtractFloodIncidentAsync(It.IsAny<NewsAlertItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedNewsFloodDto(
                IsFloodIncident: true,
                LocationName: "Taft Ave cor. Pedro Gil, Manila",
                City: "Manila",
                Barangay: "Ermita",
                Latitude: 14.5776,
                Longitude: 120.9886,
                EstimatedDepthInches: 4,
                DepthCategory: "Gutter",
                PassableMotorcycle: true,
                PassableSedan: true,
                PassableSuvJeepney: true,
                ConfidenceScore: 0.95m,
                Source: "Official MMDA Advisory",
                Summary: "Gutter-deep flood along Taft Avenue corner Pedro Gil."
            ));

        var mockNotifier = new Mock<IFloodAlertNotifier>();

        var service = new NewsAlertIngestionService(
            context,
            mockReader.Object,
            mockExtractor.Object,
            CreateOptions(),
            NullLogger<NewsAlertIngestionService>.Instance,
            mockNotifier.Object
        );

        var result = await service.IngestLatestFeedsAsync();

        Assert.Equal(1, result.TotalItemsPolled);
        Assert.Equal(1, result.FloodIncidentsDetected);
        Assert.Equal(1, result.NewReportsCreated);
        Assert.Equal(0, result.DuplicatesMerged);
        Assert.Single(result.CreatedReports);
        Assert.Equal("Official MMDA Advisory", result.CreatedReports[0].ReporterAlias);

        // Verify entity persisted in database
        var savedReport = await context.FloodReports.FirstOrDefaultAsync();
        Assert.NotNull(savedReport);
        Assert.Equal("Taft Ave cor. Pedro Gil, Manila", savedReport.ExtractedLocationName);
        Assert.Equal("Active", savedReport.Status);

        // Verify SignalR broadcast triggered
        mockNotifier.Verify(n => n.BroadcastNewReportAsync(It.IsAny<FloodReportResponseDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestLatestFeedsAsync_WhenReportExistsWithinRadius_ShouldMergeAndCorroborate()
    {
        using var context = CreateInMemoryContext();

        // 1. Pre-seed an active crowdsourced report at España (14.6091, 120.9903) with 10" depth
        var existingReport = FloodReport.Create(
            rawDescription: "Medyo mataas na baha sa may UST España",
            extractedLocationName: "España Blvd near UST",
            latitude: 14.6091,
            longitude: 120.9903,
            estimatedDepthInches: 10,
            depthCategory: "HalfTire",
            passableMotorcycle: false,
            passableSedan: true,
            passableSuvJeepney: true,
            aiConfidenceScore: 0.80m,
            reporterAlias: "CommuterJuan",
            lifetime: TimeSpan.FromHours(1)
        );
        context.FloodReports.Add(existingReport);
        await context.SaveChangesAsync();

        var initialExpiry = existingReport.ExpiresAt;

        // 2. Mock incoming official bulletin at nearly same location (50 meters away: 14.6093, 120.9905)
        // reporting higher flood depth (15" Knee deep)
        var mockReader = new Mock<INewsFeedReader>();
        mockReader.Setup(r => r.FetchLatestAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new NewsAlertItem(
                    Title: "PAGASA Warning: Knee-deep flood along España Boulevard UST Area",
                    Description: "Severe baha along España in front of UST. 15 inches depth.",
                    Source: "DOST-PAGASA Bulletin",
                    PublishedAt: DateTimeOffset.UtcNow
                )
            ]);

        var mockExtractor = new Mock<INewsFloodExtractor>();
        mockExtractor.Setup(e => e.ExtractFloodIncidentAsync(It.IsAny<NewsAlertItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedNewsFloodDto(
                IsFloodIncident: true,
                LocationName: "España Blvd (UST Basin)",
                City: "Manila",
                Barangay: "Sampaloc",
                Latitude: 14.6093,
                Longitude: 120.9905,
                EstimatedDepthInches: 15,
                DepthCategory: "Knee",
                PassableMotorcycle: false,
                PassableSedan: false,
                PassableSuvJeepney: true,
                ConfidenceScore: 0.96m,
                Source: "DOST-PAGASA Bulletin",
                Summary: "Knee-deep flood along España in front of UST."
            ));

        var mockNotifier = new Mock<IFloodAlertNotifier>();

        var service = new NewsAlertIngestionService(
            context,
            mockReader.Object,
            mockExtractor.Object,
            CreateOptions(deduplicationRadius: 250.0),
            NullLogger<NewsAlertIngestionService>.Instance,
            mockNotifier.Object
        );

        var result = await service.IngestLatestFeedsAsync();

        // Should merge rather than creating a duplicate pin
        Assert.Equal(0, result.NewReportsCreated);
        Assert.Equal(1, result.DuplicatesMerged);
        Assert.Single(result.MergedReports);

        // Database should still only have 1 report
        Assert.Equal(1, await context.FloodReports.CountAsync());

        var updatedReport = await context.FloodReports.FirstAsync();
        Assert.True(updatedReport.ExpiresAt > initialExpiry, "ExpiresAt should be extended from official alert corroboration");
        Assert.True(updatedReport.AiConfidenceScore > 0.80m, "Confidence score should be reinforced");
        Assert.Equal(15, updatedReport.EstimatedDepthInches); // Upgraded to 15"
        Assert.Equal("Knee", updatedReport.DepthCategory);
        Assert.False(updatedReport.PassableSedan); // Sedan is now impassable
        Assert.Contains("DOST-PAGASA Bulletin", updatedReport.RawDescription);
    }

    [Fact]
    public async Task IngestLatestFeedsAsync_WhenReportIsDistant_ShouldCreateSeparatePin()
    {
        using var context = CreateInMemoryContext();

        // Existing report in Manila España (14.6091, 120.9903)
        context.FloodReports.Add(FloodReport.Create(
            rawDescription: "España baha",
            extractedLocationName: "España Blvd",
            latitude: 14.6091,
            longitude: 120.9903,
            estimatedDepthInches: 8,
            depthCategory: "HalfTire",
            passableMotorcycle: false,
            passableSedan: true,
            passableSuvJeepney: true,
            aiConfidenceScore: 0.85m
        ));
        await context.SaveChangesAsync();

        // Incoming alert in Quezon City Araneta Ave (14.6200, 121.0135) - over 2.7 km away!
        var mockReader = new Mock<INewsFeedReader>();
        mockReader.Setup(r => r.FetchLatestAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new NewsAlertItem(
                    Title: "GMA Alert: Waist-deep flooding shuts down Araneta Avenue",
                    Description: "Araneta Ave waist deep flood.",
                    Source: "GMA Disaster Desk",
                    PublishedAt: DateTimeOffset.UtcNow
                )
            ]);

        var mockExtractor = new Mock<INewsFloodExtractor>();
        mockExtractor.Setup(e => e.ExtractFloodIncidentAsync(It.IsAny<NewsAlertItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedNewsFloodDto(
                IsFloodIncident: true,
                LocationName: "Araneta Ave cor. E. Rodriguez",
                City: "Quezon City",
                Barangay: "Tatalon",
                Latitude: 14.6200,
                Longitude: 121.0135,
                EstimatedDepthInches: 28,
                DepthCategory: "Waist",
                PassableMotorcycle: false,
                PassableSedan: false,
                PassableSuvJeepney: false,
                ConfidenceScore: 0.95m,
                Source: "GMA Disaster Desk",
                Summary: "Waist-deep flood shuts down Araneta Avenue."
            ));

        var service = new NewsAlertIngestionService(
            context,
            mockReader.Object,
            mockExtractor.Object,
            CreateOptions(deduplicationRadius: 250.0),
            NullLogger<NewsAlertIngestionService>.Instance
        );

        var result = await service.IngestLatestFeedsAsync();

        Assert.Equal(1, result.NewReportsCreated);
        Assert.Equal(0, result.DuplicatesMerged);
        Assert.Equal(2, await context.FloodReports.CountAsync());
    }

    [Fact]
    public async Task IngestLatestFeedsAsync_WhenNonFloodItem_ShouldSkip()
    {
        using var context = CreateInMemoryContext();

        var mockReader = new Mock<INewsFeedReader>();
        mockReader.Setup(r => r.FetchLatestAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new NewsAlertItem(
                    Title: "Classes suspended across Metro Manila",
                    Description: "Classes at all levels suspended due to bad weather.",
                    Source: "DepEd Bulletin",
                    PublishedAt: DateTimeOffset.UtcNow
                )
            ]);

        var mockExtractor = new Mock<INewsFloodExtractor>();
        mockExtractor.Setup(e => e.ExtractFloodIncidentAsync(It.IsAny<NewsAlertItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExtractedNewsFloodDto?)null); // Non-flood item

        var service = new NewsAlertIngestionService(
            context,
            mockReader.Object,
            mockExtractor.Object,
            CreateOptions(),
            NullLogger<NewsAlertIngestionService>.Instance
        );

        var result = await service.IngestLatestFeedsAsync();

        Assert.Equal(1, result.TotalItemsPolled);
        Assert.Equal(0, result.FloodIncidentsDetected);
        Assert.Equal(0, result.NewReportsCreated);
        Assert.Equal(1, result.ItemsSkippedNonFlood);
        Assert.Equal(0, await context.FloodReports.CountAsync());
    }

    [Fact]
    public async Task IngestDirectAlertAsync_WithValidAlert_ShouldCreateIncident()
    {
        using var context = CreateInMemoryContext();

        var mockReader = new Mock<INewsFeedReader>();
        var mockExtractor = new Mock<INewsFloodExtractor>();
        mockExtractor.Setup(e => e.ExtractFloodIncidentAsync(It.IsAny<NewsAlertItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedNewsFloodDto(
                IsFloodIncident: true,
                LocationName: "Rizal Ave cor. Blumentritt",
                City: "Manila",
                Barangay: "Santa Cruz",
                Latitude: 14.6225,
                Longitude: 120.9830,
                EstimatedDepthInches: 10,
                DepthCategory: "HalfTire",
                PassableMotorcycle: false,
                PassableSedan: true,
                PassableSuvJeepney: true,
                ConfidenceScore: 0.92m,
                Source: "Direct Webhook",
                Summary: "Half-tire flood along Blumentritt."
            ));

        var service = new NewsAlertIngestionService(
            context,
            mockReader.Object,
            mockExtractor.Object,
            CreateOptions(),
            NullLogger<NewsAlertIngestionService>.Instance
        );

        var result = await service.IngestDirectAlertAsync(new IngestAlertRequestDto(
            RawText: "Half tire flood at Blumentritt",
            Source: "Direct Webhook"
        ));

        Assert.NotNull(result);
        Assert.Equal("Rizal Ave cor. Blumentritt", result.ExtractedLocationName);
        Assert.Equal(1, await context.FloodReports.CountAsync());
    }
}
