using BahaGuard.Application.DTOs;
using BahaGuard.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace BahaGuard.Infrastructure.AI;

/// <summary>
/// Phase 1 Placeholder/Mock implementation of the Gemini Flood Analyzer.
/// Evaluates commuter report descriptions and returns structured flood intelligence.
/// Will be replaced/extended with real Gemini 2.5 Flash Multimodal Vision calls in Phase 2.
/// </summary>
public class GeminiFloodAnalyzer : IGeminiVisionService
{
    private readonly ILogger<GeminiFloodAnalyzer> _logger;

    public GeminiFloodAnalyzer(ILogger<GeminiFloodAnalyzer> logger)
    {
        _logger = logger;
    }

    public Task<FloodAnalysisResultDto> AnalyzeIncidentAsync(
        string rawDescription,
        Stream? imageStream = null,
        string? mimeType = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing flood incident report (Phase 1 Mock): {Description}", rawDescription);

        var lower = rawDescription.ToLowerInvariant();

        // Spam or test rejection heuristic
        if (lower.Contains("spam") || lower.Contains("not flood") || lower.Contains("test fake"))
        {
            return Task.FromResult(new FloodAnalysisResultDto(
                IsValidFloodReport: false,
                RejectionReason: "Uploaded incident content does not depict street flooding in Metro Manila.",
                NormalizedLocation: "Unknown",
                EstimatedDepthInches: 0,
                DepthCategory: "Dry",
                Passability: new PassabilityDto(true, true, true),
                ConfidenceScore: 0.95m,
                CommuterAdvisory: "Incident rejected: non-flood content."
            ));
        }

        // Taglish Depth & Passability Heuristic
        int depthInches = 8;
        string depthCategory = "HalfTire";
        bool passableMotorcycle = false;
        bool passableSedan = true;
        bool passableSuv = true;
        string advisory = "Water level rising. Light vehicles advise alternate route.";

        if (lower.Contains("tuhod") || lower.Contains("knee") || lower.Contains("binti"))
        {
            depthInches = 15;
            depthCategory = "Knee";
            passableMotorcycle = false;
            passableSedan = false;
            passableSuv = true;
            advisory = "Knee-deep flood. Not passable to light vehicles and sedans. SUVs and trucks use caution.";
        }
        else if (lower.Contains("bewang") || lower.Contains("waist") || lower.Contains("lubog"))
        {
            depthInches = 28;
            depthCategory = "Waist";
            passableMotorcycle = false;
            passableSedan = false;
            passableSuv = false;
            advisory = "Waist-deep severe flood. Impassable to all types of vehicles. Avoid this corridor.";
        }
        else if (lower.Contains("gutter") || lower.Contains("gutter-deep") || lower.Contains("talampakan"))
        {
            depthInches = 4;
            depthCategory = "Gutter";
            passableMotorcycle = true;
            passableSedan = true;
            passableSuv = true;
            advisory = "Gutter-deep water. Passable to all vehicles. Drive with moderate caution.";
        }
        else if (lower.Contains("gulong") || lower.Contains("tire") || lower.Contains("half tire"))
        {
            depthInches = 10;
            depthCategory = "HalfTire";
            passableMotorcycle = false;
            passableSedan = true;
            passableSuv = true;
            advisory = "Half-tire deep water. Motorcycles not advised. Sedans proceed with caution.";
        }

        // Normalized location extraction heuristic for Manila landmarks
        string normalizedLocation = "Metro Manila Street";
        if (lower.Contains("españa") || lower.Contains("espana"))
        {
            normalizedLocation = lower.Contains("lacson") 
                ? "España Blvd cor. Lacson Ave, Sampaloc, Manila" 
                : "España Blvd (UST area), Sampaloc, Manila";
        }
        else if (lower.Contains("taft"))
        {
            normalizedLocation = lower.Contains("pedro gil") 
                ? "Taft Ave cor. Pedro Gil St, Malate, Manila" 
                : "Taft Ave (PGH/UN Ave area), Manila";
        }
        else if (lower.Contains("edsa"))
        {
            normalizedLocation = "EDSA, Metro Manila";
        }
        else if (lower.Contains("quiapo"))
        {
            normalizedLocation = "Quezon Blvd (Quiapo Church area), Manila";
        }
        else if (lower.Contains("morayta") || lower.Contains("nicanor reyes"))
        {
            normalizedLocation = "Nicanor Reyes St (Morayta), Sampaloc, Manila";
        }
        else
        {
            normalizedLocation = char.ToUpperInvariant(rawDescription[0]) + rawDescription[1..Math.Min(rawDescription.Length, 60)];
        }

        var result = new FloodAnalysisResultDto(
            IsValidFloodReport: true,
            RejectionReason: null,
            NormalizedLocation: normalizedLocation,
            EstimatedDepthInches: depthInches,
            DepthCategory: depthCategory,
            Passability: new PassabilityDto(passableMotorcycle, passableSedan, passableSuv),
            ConfidenceScore: 0.91m,
            CommuterAdvisory: advisory
        );

        return Task.FromResult(result);
    }
}
