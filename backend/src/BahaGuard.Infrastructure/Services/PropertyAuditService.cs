using System.Text.Json;
using System.Text.Json.Nodes;
using BahaGuard.Application.DTOs;
using BahaGuard.Application.Interfaces;
using BahaGuard.Infrastructure.AI;
using BahaGuard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace BahaGuard.Infrastructure.Services;

/// <summary>
/// Spatial Property Flood-Risk Auditor.
/// Aggregates historical spatial database metrics within a 500m radius of a prospective rental/dorm,
/// and synthesizes a renter due-diligence report via Gemini with intelligent rule-based fallbacks.
/// </summary>
public class PropertyAuditService(
    BahaGuardDbContext dbContext,
    HttpClient httpClient,
    IOptions<GeminiOptions> geminiOptions,
    ILogger<PropertyAuditService> logger) : IPropertyAuditService
{
    private static readonly GeometryFactory Srid4326Factory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    private readonly GeminiOptions _options = geminiOptions.Value;

    private static readonly (string Keyword, string Name, double Lat, double Lng)[] ManilaLandmarks =
    [
        ("españa", "España Blvd (UST Basin), Sampaloc, Manila", 14.6091, 120.9903),
        ("ust", "UST Campus Perimeter (España / Dapitan), Manila", 14.6091, 120.9903),
        ("dapitan", "Dapitan St (UST Dapitan Gate), Sampaloc, Manila", 14.6115, 120.9918),
        ("lacson", "Lacson Ave cor. España Blvd, Sampaloc, Manila", 14.6095, 120.9905),
        ("taft", "Taft Ave (DLSU / PGH Corridor), Manila", 14.5702, 120.9915),
        ("pedro gil", "Taft Ave cor. Pedro Gil St, Ermita, Manila", 14.5776, 120.9886),
        ("dlsu", "DLSU Manila (Taft Ave cor. Vito Cruz), Malate, Manila", 14.5648, 120.9932),
        ("vito cruz", "Vito Cruz (P. Ocampo St cor. Taft), Malate, Manila", 14.5648, 120.9932),
        ("quiapo", "Quezon Blvd / Plaza Miranda, Quiapo, Manila", 14.5990, 120.9842),
        ("blumentritt", "Rizal Ave cor. Blumentritt St, Santa Cruz, Manila", 14.6225, 120.9830),
        ("santa mesa", "Santa Mesa (PUP Basin), Manila", 14.5985, 121.0110),
        ("pup", "PUP Main Campus (Anonas St), Santa Mesa, Manila", 14.5985, 121.0110),
        ("araneta", "Araneta Ave cor. E. Rodriguez, Tatalon, Quezon City", 14.6200, 121.0135),
        ("katipunan", "Katipunan Ave (Ateneo / Miriam), Quezon City", 14.6400, 121.0750),
        ("edsa", "EDSA Cubao / Ortigas Corridor, Metro Manila", 14.6190, 121.0510)
    ];

    public async Task<PropertyAuditResponseDto> AuditPropertyAsync(
        PropertyAuditQuery query,
        CancellationToken cancellationToken = default)
    {
        var (resolvedName, lat, lng) = ResolveCoordinates(query);
        var auditPoint = Srid4326Factory.CreatePoint(new Coordinate(lng, lat));
        var radiusMeters = query.RadiusMeters > 0 ? query.RadiusMeters : 500;

        logger.LogInformation("Auditing property '{Address}' at ({Lat}, {Lng}) with radius {Radius}m",
            resolvedName, lat, lng, radiusMeters);

        // Spatial query: Fetch historical and active records within radius
        var surroundingReports = await dbContext.FloodReports
            .AsNoTracking()
            .Where(r => r.Coordinates.Distance(auditPoint) <= radiusMeters)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        // Aggregate spatial statistics
        int incidentsCount = surroundingReports.Count;
        double avgDepth = incidentsCount > 0
            ? Math.Round(surroundingReports.Average(r => r.EstimatedDepthInches), 1)
            : 0;
        int maxDepth = incidentsCount > 0
            ? surroundingReports.Max(r => r.EstimatedDepthInches)
            : 0;

        string predominantCategory = incidentsCount > 0
            ? surroundingReports
                .GroupBy(r => r.DepthCategory)
                .OrderByDescending(g => g.Count())
                .First().Key
            : "Dry";

        double avgRecedingHours = incidentsCount > 0
            ? Math.Round(surroundingReports
                .Select(r => (r.ExpiresAt - r.CreatedAt).TotalHours)
                .Average(), 1)
            : 0;

        // Calculate Risk Score (1-10) and Level
        var (riskScore, riskLevel) = CalculateRiskScore(incidentsCount, maxDepth, avgDepth);

        // Vehicle Vulnerabilities
        bool motoHighRisk = surroundingReports.Any(r => !r.PassableMotorcycle);
        bool sedanHighRisk = surroundingReports.Any(r => !r.PassableSedan);
        bool suvHighRisk = surroundingReports.Any(r => !r.PassableSuvJeepney);

        string parkingAdvisory = riskScore switch
        {
            >= 7 => "Basement and ground-level street parking at severe water intrusion risk during Habagat and tropical depressions. Relocate vehicles to elevated parking garages before heavy rainfall.",
            >= 4 => "Ground-level street parking vulnerable to gutter and tire-deep ponding. Low-clearance sedans and motorcycles should be moved to elevated slots.",
            _ => "Street has adequate drainage with minimal recorded inundation. Standard street parking generally safe."
        };

        var vehicleVulnerability = new VehicleVulnerabilityDto(
            MotorcycleHighRisk: motoHighRisk,
            SedanHighRisk: sedanHighRisk,
            SuvHighRisk: suvHighRisk,
            ParkingAdvisory: parkingAdvisory
        );

        // Synthesize Due Diligence Report via Gemini or local intelligent analyzer
        var dueDiligence = await SynthesizeDueDiligenceAsync(
            resolvedName,
            riskScore,
            riskLevel,
            incidentsCount,
            avgDepth,
            maxDepth,
            avgRecedingHours,
            cancellationToken);

        return new PropertyAuditResponseDto(
            Address: query.Address ?? resolvedName,
            ResolvedLocationName: resolvedName,
            Latitude: lat,
            Longitude: lng,
            RiskScore: riskScore,
            RiskLevel: riskLevel,
            HistoricalIncidentsCount: incidentsCount,
            AverageDepthInches: avgDepth,
            MaxDepthInches: maxDepth,
            PredominantDepthCategory: predominantCategory,
            EstimatedRecedingTimeHours: avgRecedingHours,
            VehicleVulnerability: vehicleVulnerability,
            DueDiligenceReport: dueDiligence,
            AuditedAt: DateTimeOffset.UtcNow
        );
    }

    private static (string ResolvedName, double Lat, double Lng) ResolveCoordinates(PropertyAuditQuery query)
    {
        if (query.Latitude.HasValue && query.Longitude.HasValue)
        {
            return (query.Address ?? "Custom GPS Location", query.Latitude.Value, query.Longitude.Value);
        }

        var text = (query.Address ?? string.Empty).ToLowerInvariant();

        foreach (var landmark in ManilaLandmarks)
        {
            if (text.Contains(landmark.Keyword))
            {
                return (landmark.Name, landmark.Lat, landmark.Lng);
            }
        }

        // Default to UST España basin as central Manila baseline
        return ("Metro Manila (España Basin Default)", 14.6091, 120.9903);
    }

    private static (int Score, string Level) CalculateRiskScore(int count, int maxDepth, double avgDepth)
    {
        if (count == 0)
        {
            return (1, "Low");
        }

        // Frequency component (1 to 5)
        int freqScore = Math.Min(5, count);

        // Depth severity component (1 to 5)
        int depthScore = maxDepth switch
        {
            >= 36 => 5, // Chest deep+
            >= 24 => 4, // Waist deep
            >= 14 => 3, // Knee deep
            >= 8 => 2,  // Half tire
            _ => 1      // Gutter deep
        };

        int totalScore = Math.Clamp(freqScore + depthScore, 1, 10);

        string level = totalScore switch
        {
            >= 9 => "Extreme",
            >= 7 => "High",
            >= 4 => "Moderate",
            _ => "Low"
        };

        return (totalScore, level);
    }

    private async Task<TenantDueDiligenceDto> SynthesizeDueDiligenceAsync(
        string locationName,
        int riskScore,
        string riskLevel,
        int incidentCount,
        double avgDepth,
        int maxDepth,
        double avgRecedingHours,
        CancellationToken cancellationToken)
    {
        var apiKey = _options.ApiKey;
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var prompt = $"""
                    Synthesize a concise, practical tenant and dorm renter due-diligence audit report for this location in Metro Manila:
                    - Address: {locationName}
                    - Historical Inundation Incidents: {incidentCount} recorded events (500m radius)
                    - Calculated Risk Score: {riskScore}/10 ({riskLevel} Risk)
                    - Average Depth: {avgDepth} inches, Max Depth: {maxDepth} inches
                    - Average Water Subsidence/Receding Duration: {avgRecedingHours} hours

                    Return structured JSON with these 4 keys:
                    1. executiveSummary (1 clear punchy sentence assessing flood danger for someone planning to sign a 1-year lease here)
                    2. groundFloorRiskAdvice (advice on 1st-floor vs upper-floor living units, dampness, and mold)
                    3. electricalAndUtilityRisk (backup generator elevation, elevator ground flooding, sub-meter flooding risk)
                    4. evacuationAndAccessAdvisory (walkability to LRT/MRT or main roads during heavy Habagat rainfall)
                    """;

                var model = string.IsNullOrWhiteSpace(_options.Model) ? "gemini-2.5-flash" : _options.Model;
                var url = $"{_options.BaseUrl.TrimEnd('/')}/models/{model}:generateContent?key={apiKey}";

                var requestBody = new JsonObject
                {
                    ["contents"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["role"] = "user",
                            ["parts"] = new JsonArray { new JsonObject { ["text"] = prompt } }
                        }
                    },
                    ["generationConfig"] = new JsonObject
                    {
                        ["temperature"] = 0.2,
                        ["responseMimeType"] = "application/json"
                    }
                };

                using var content = new StringContent(requestBody.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                    var parsed = JsonNode.Parse(responseJson);
                    var textContent = parsed?["candidates"]?[0]?["content"]?[0]?["parts"]?[0]?["text"]?.GetValue<string>();

                    if (!string.IsNullOrWhiteSpace(textContent))
                    {
                        var doc = JsonNode.Parse(textContent);
                        return new TenantDueDiligenceDto(
                            ExecutiveSummary: doc?["executiveSummary"]?.GetValue<string>() ?? $"{riskLevel} flood risk corridor with {incidentCount} recorded incidents.",
                            GroundFloorRiskAdvice: doc?["groundFloorRiskAdvice"]?.GetValue<string>() ?? "Inquire with landlord about past water intrusion into ground-floor units.",
                            ElectricalAndUtilityRisk: doc?["electricalAndUtilityRisk"]?.GetValue<string>() ?? "Verify generator elevation and backup emergency circuits.",
                            EvacuationAndAccessAdvisory: doc?["evacuationAndAccessAdvisory"]?.GetValue<string>() ?? "Identify elevated walkways towards main transit lines."
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Gemini due diligence synthesis failed. Falling back to rule-based engine.");
            }
        }

        // Resilient intelligent rule-based generation
        return GenerateRuleBasedDueDiligence(riskScore, riskLevel, incidentCount, maxDepth, avgRecedingHours);
    }

    private static TenantDueDiligenceDto GenerateRuleBasedDueDiligence(
        int score,
        string level,
        int count,
        int maxDepth,
        double recedingHours)
    {
        if (score >= 7)
        {
            return new TenantDueDiligenceDto(
                ExecutiveSummary: $"{level} Flood Vulnerability: High spatial frequency of {count} severe inundation events logged within 500m (peak {maxDepth}\"), typical receding time ~{recedingHours} hours.",
                GroundFloorRiskAdvice: "Strong recommendation to choose units on the 2nd floor or higher. Ground-floor dorms and apartment units along this corridor have historical precedent for storm runoff intrusion.",
                ElectricalAndUtilityRisk: "Inspect building generator placement: confirm emergency standby power is situated above street flood levels (elevated mezzanine or rooftop).",
                EvacuationAndAccessAdvisory: "Street becomes impassable for sedans and motorcycles during intense monsoon downpours. Plan elevated foot routes towards major transit arteries."
            );
        }

        if (score >= 4)
        {
            return new TenantDueDiligenceDto(
                ExecutiveSummary: $"Moderate Inundation Basin: {count} historical ponding incidents recorded with average depth reaching gutter-to-tire level.",
                GroundFloorRiskAdvice: "Ground-floor units generally safe from major flooding, but verify perimeter door sills and exterior drainage gates.",
                ElectricalAndUtilityRisk: "Low risk of direct electrical substation flooding; standard street-level utilities are operational during normal seasonal rain.",
                EvacuationAndAccessAdvisory: "Expect slow traffic and gutter puddles during afternoon thunderstorms; high-clearance jeepneys and elevated walkways remain passable."
            );
        }

        return new TenantDueDiligenceDto(
            ExecutiveSummary: "Low Flood Risk: Spatial records show minimal recorded street ponding in this immediate 500m radius.",
            GroundFloorRiskAdvice: "Ground-floor living spaces and dorm units are well elevated above municipal drainage overflow lines.",
            ElectricalAndUtilityRisk: "Normal residential utility resilience; no recorded history of flood-triggered sub-meter shutoffs.",
            EvacuationAndAccessAdvisory: "Excellent street access during typhoons; main commute routes to transit hubs remain navigable."
        );
    }
}
