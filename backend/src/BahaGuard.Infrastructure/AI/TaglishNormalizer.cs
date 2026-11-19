using System.Text.RegularExpressions;
using BahaGuard.Application.DTOs;

namespace BahaGuard.Infrastructure.AI;

/// <summary>
/// Specialized Tagalog/Taglish NLP normalization engine for Metro Manila flood reports.
/// Extracts landmarks, detects water depth indicators, evaluates vehicle passability,
/// and produces structured outputs.
/// </summary>
public static class TaglishNormalizer
{
    private static readonly (string[] Keywords, string Category, int Inches, bool Moto, bool Sedan, bool Suv, string Advisory)[] DepthRules =
    [
        (
            ["dibdib", "chest", "chest-deep", "leeg", "lampas tao", "bubong"],
            "ChestPlus",
            48,
            false,
            false,
            false,
            "Critical flood depth (chest to neck level). Impassable to all vehicles. Evacuation required."
        ),
        (
            ["bewang", "baywang", "waist", "waist-deep", "lubog", "pusod", "tiyan", "lubog na"],
            "Waist",
            28,
            false,
            false,
            false,
            "Waist-deep flood. Impassable to all vehicles including SUVs. Avoid this route."
        ),
        (
            ["tuhod", "knee", "knee-deep", "binti", "hanggang tuhod", "hanggang binti"],
            "Knee",
            15,
            false,
            false,
            true,
            "Knee-deep flood. Not passable to motorcycles and sedans. High-clearance SUVs and trucks use extreme caution."
        ),
        (
            ["gulong", "half tire", "half-tire", "kalahating gulong", "tire-deep", "tires", "tire"],
            "HalfTire",
            10,
            false,
            true,
            true,
            "Half-tire water depth. Impassable to motorcycles. Sedans drive with caution."
        ),
        (
            ["talampakan", "gutter", "gutter-deep", "babaw", "tapon", "ankle"],
            "Gutter",
            4,
            true,
            true,
            true,
            "Gutter-deep water. Passable to all vehicle types. Drive at moderate speed."
        )
    ];

    private static readonly (string[] Patterns, string Normalized)[] ManilaLandmarks =
    [
        (["ust dapitan", "dapitan ust", "dapitan"], "Dapitan St cor. Lacson Ave (UST area), Sampaloc, Manila"),
        (["ust espana", "ust españa", "españa ust", "espana ust"], "España Blvd (UST area), Sampaloc, Manila"),
        (["españa lacson", "espana lacson", "lacson españa", "lacson espana"], "España Blvd cor. Lacson Ave, Sampaloc, Manila"),
        (["ust noval", "noval ust", "p noval", "p. noval"], "P. Noval St (UST area), Sampaloc, Manila"),
        (["españa", "espana"], "España Blvd, Sampaloc, Manila"),
        (["taft pedro gil", "pedro gil taft"], "Taft Ave cor. Pedro Gil St, Malate, Manila"),
        (["taft pgh", "taft padre faura", "padre faura taft"], "Taft Ave cor. Padre Faura St (PGH area), Ermita, Manila"),
        (["taft quirino", "quirino taft"], "Taft Ave cor. Quirino Ave, Malate, Manila"),
        (["taft un", "taft u.n.", "un ave taft"], "Taft Ave cor. United Nations Ave, Ermita, Manila"),
        (["taft"], "Taft Ave, Manila"),
        (["edsa shaw", "shaw edsa", "shaw crossing"], "EDSA cor. Shaw Blvd, Mandaluyong"),
        (["edsa ortigas", "ortigas edsa"], "EDSA cor. Ortigas Ave, Quezon City"),
        (["edsa taft", "pasay rotonda"], "EDSA cor. Taft Ave (Pasay Rotonda), Pasay"),
        (["edsa cubao", "cubao edsa"], "EDSA cor. Aurora Blvd (Cubao), Quezon City"),
        (["edsa"], "EDSA, Metro Manila"),
        (["quiapo", "quezon blvd", "underpass quiapo"], "Quezon Blvd (Quiapo Church area), Manila"),
        (["morayta", "nicanor reyes"], "Nicanor Reyes St (Morayta), Sampaloc, Manila"),
        (["rizal ave", "avenida", "doroteo jose"], "Rizal Ave (Avenida), Santa Cruz, Manila"),
        (["maysilo circle", "boni maysilo"], "Maysilo Circle, Boni Ave, Mandaluyong"),
        (["araneta ave", "g. araneta", "talayan"], "G. Araneta Ave cor. E. Rodriguez Sr. Ave, Quezon City"),
        (["buendia", "gil puyat"], "Gil Puyat Ave (Buendia), Makati/Pasay"),
        (["roces ave", "don a. roces"], "Don A. Roces Ave, Quezon City"),
        (["katipunan", "ateneo"], "Katipunan Ave, Quezon City")
    ];

    private static readonly HashSet<string> SpamKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "spam", "not flood", "not a flood", "sunny day", "fake report", "bogus", "meme", "test fake"
    };

    public static FloodAnalysisResultDto Normalize(string rawDescription, bool hasImage = false)
    {
        var trimmed = rawDescription?.Trim() ?? string.Empty;
        var lower = trimmed.ToLowerInvariant();

        // 1. Spam check
        foreach (var spam in SpamKeywords)
        {
            if (lower.Contains(spam))
            {
                return new FloodAnalysisResultDto(
                    IsValidFloodReport: false,
                    RejectionReason: "Content flagged as non-flood, sunny weather, or invalid report.",
                    NormalizedLocation: "Unknown",
                    EstimatedDepthInches: 0,
                    DepthCategory: "Dry",
                    Passability: new PassabilityDto(true, true, true),
                    ConfidenceScore: 0.96m,
                    CommuterAdvisory: "Incident rejected: does not report street flood hazard."
                );
            }
        }

        // 2. Depth and passability evaluation
        int depthInches = 6;
        string depthCategory = "HalfTire";
        bool passableMoto = false;
        bool passableSedan = true;
        bool passableSuv = true;
        string advisory = "Flood detected. Passable to sedans and SUVs with caution.";

        foreach (var rule in DepthRules)
        {
            if (rule.Keywords.Any(k => lower.Contains(k)))
            {
                depthCategory = rule.Category;
                depthInches = rule.Inches;
                passableMoto = rule.Moto;
                passableSedan = rule.Sedan;
                passableSuv = rule.Suv;
                advisory = rule.Advisory;
                break;
            }
        }

        // 3. Normalized landmark extraction
        string normalizedLocation = string.Empty;
        foreach (var landmark in ManilaLandmarks)
        {
            if (landmark.Patterns.Any(p => lower.Contains(p)))
            {
                normalizedLocation = landmark.Normalized;
                break;
            }
        }

        if (string.IsNullOrEmpty(normalizedLocation))
        {
            // Clean up general text
            var cleanText = Regex.Replace(trimmed, @"[^\w\s\.,\-]", "");
            normalizedLocation = char.ToUpperInvariant(cleanText[0]) + cleanText[1..Math.Min(cleanText.Length, 60)];
        }

        // 4. Confidence Score (multimodal image gives higher confidence)
        decimal confidence = hasImage ? 0.94m : 0.88m;

        return new FloodAnalysisResultDto(
            IsValidFloodReport: true,
            RejectionReason: null,
            NormalizedLocation: normalizedLocation,
            EstimatedDepthInches: depthInches,
            DepthCategory: depthCategory,
            Passability: new PassabilityDto(passableMoto, passableSedan, passableSuv),
            ConfidenceScore: confidence,
            CommuterAdvisory: advisory
        );
    }
}
