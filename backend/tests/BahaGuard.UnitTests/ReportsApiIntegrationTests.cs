using System.Net;
using System.Net.Http.Json;
using BahaGuard.Application.DTOs;
using BahaGuard.Application.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace BahaGuard.UnitTests;

public class ReportsApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<IFloodReportService> _mockFloodReportService = new();

    public ReportsApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IFloodReportService>(_ => _mockFloodReportService.Object);
            });
        });
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthy()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", content);
    }

    [Fact]
    public async Task PropertyAudit_ShouldReturnStub()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/property-audit?address=España%20Blvd&lat=14.6091&lng=120.9903");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Upcoming in Phase 4", content);
    }

    [Fact]
    public async Task PostReport_WithValidData_ShouldReturnCreated()
    {
        var expectedReportId = Guid.NewGuid();
        _mockFloodReportService.Setup(s => s.CreateReportAsync(
            It.IsAny<CreateFloodReportDto>(),
            It.IsAny<Stream?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FloodReportResponseDto(
                Id: expectedReportId,
                ReporterAlias: "ManilaRider",
                RawDescription: "Baha sa España Lacson gutter deep",
                ExtractedLocationName: "España Blvd cor. Lacson Ave, Sampaloc, Manila",
                Barangay: "Sampaloc",
                City: "Manila",
                Latitude: 14.6091,
                Longitude: 120.9903,
                EstimatedDepthInches: 4,
                DepthCategory: "Gutter",
                PassableMotorcycle: true,
                PassableSedan: true,
                PassableSuvJeepney: true,
                AiConfidenceScore: 0.95m,
                ImageUrl: null,
                Status: "Active",
                CreatedAt: DateTimeOffset.UtcNow,
                ExpiresAt: DateTimeOffset.UtcNow.AddHours(3),
                DistanceMeters: 0
            ));

        var client = _factory.CreateClient();

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("14.6091"), "Latitude");
        form.Add(new StringContent("120.9903"), "Longitude");
        form.Add(new StringContent("Baha sa España Lacson gutter deep"), "RawDescription");
        form.Add(new StringContent("ManilaRider"), "ReporterAlias");
        form.Add(new StringContent("Sampaloc"), "Barangay");
        form.Add(new StringContent("Manila"), "City");

        var response = await client.PostAsync("/api/reports", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<FloodReportResponseDto>();
        Assert.NotNull(result);
        Assert.Equal(expectedReportId, result.Id);
        Assert.Equal("España Blvd cor. Lacson Ave, Sampaloc, Manila", result.ExtractedLocationName);
        Assert.Equal("Gutter", result.DepthCategory);
        Assert.True(result.PassableMotorcycle);
    }

    [Fact]
    public async Task PostReport_WithInvalidCoordinates_ShouldReturnBadRequest()
    {
        var client = _factory.CreateClient();

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("999.0"), "Latitude"); // Invalid latitude > 90
        form.Add(new StringContent("120.9903"), "Longitude");
        form.Add(new StringContent("Invalid test coordinate"), "RawDescription");

        var response = await client.PostAsync("/api/reports", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostReport_WhenSpam_ShouldReturnBadRequestWithAnalyzerDetail()
    {
        _mockFloodReportService.Setup(s => s.CreateReportAsync(
            It.IsAny<CreateFloodReportDto>(),
            It.IsAny<Stream?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Incident was rejected as non-flood or spam content."));

        var client = _factory.CreateClient();

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("14.6091"), "Latitude");
        form.Add(new StringContent("120.9903"), "Longitude");
        form.Add(new StringContent("This is spam bogus report not flood"), "RawDescription");

        var response = await client.PostAsync("/api/reports", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("flood analyzer", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetNearbyReports_WithValidParams_ShouldReturnOk()
    {
        _mockFloodReportService.Setup(s => s.GetNearbyReportsAsync(
            It.IsAny<NearbyReportsQuery>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FloodReportResponseDto>
            {
                new(
                    Id: Guid.NewGuid(),
                    ReporterAlias: "TestUser",
                    RawDescription: "España flood",
                    ExtractedLocationName: "España Blvd",
                    Barangay: "Sampaloc",
                    City: "Manila",
                    Latitude: 14.6091,
                    Longitude: 120.9903,
                    EstimatedDepthInches: 6,
                    DepthCategory: "HalfTire",
                    PassableMotorcycle: false,
                    PassableSedan: true,
                    PassableSuvJeepney: true,
                    AiConfidenceScore: 0.90m,
                    ImageUrl: null,
                    Status: "Active",
                    CreatedAt: DateTimeOffset.UtcNow,
                    ExpiresAt: DateTimeOffset.UtcNow.AddHours(3),
                    DistanceMeters: 250.5
                )
            });

        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/reports/nearby?lat=14.6091&lng=120.9903&radiusMeters=1000&vehicleType=sedan");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<FloodReportResponseDto>>();
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal(250.5, list[0].DistanceMeters);
    }

    [Fact]
    public async Task PostReport_WhenRateLimitExceeded_ShouldReturn429TooManyRequests()
    {
        _mockFloodReportService.Setup(s => s.CreateReportAsync(
            It.IsAny<CreateFloodReportDto>(),
            It.IsAny<Stream?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FloodReportResponseDto(
                Id: Guid.NewGuid(),
                ReporterAlias: "Flooder",
                RawDescription: "Flooding",
                ExtractedLocationName: "Manila",
                Barangay: null,
                City: "Manila",
                Latitude: 14.6,
                Longitude: 120.9,
                EstimatedDepthInches: 5,
                DepthCategory: "Gutter",
                PassableMotorcycle: true,
                PassableSedan: true,
                PassableSuvJeepney: true,
                AiConfidenceScore: 0.9m,
                ImageUrl: null,
                Status: "Active",
                CreatedAt: DateTimeOffset.UtcNow,
                ExpiresAt: DateTimeOffset.UtcNow.AddHours(3)
            ));

        var client = _factory.CreateClient();

        HttpResponseMessage? lastResponse = null;

        // Policy allows 10 requests per minute per IP. The 11th should be throttled.
        for (int i = 0; i < 11; i++)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent("14.6"), "Latitude");
            form.Add(new StringContent("120.9"), "Longitude");
            form.Add(new StringContent("Rate limit check submission"), "RawDescription");

            lastResponse = await client.PostAsync("/api/reports", form);
        }

        Assert.NotNull(lastResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse.StatusCode);
    }
}
