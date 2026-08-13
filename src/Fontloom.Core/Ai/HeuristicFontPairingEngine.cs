using Fontloom.Core.Fonts;

namespace Fontloom.Core.Ai;

public static class HeuristicFontPairingEngine
{
    public static FontAiSuggestionResult BuildFallback(
        FontInfo baseFont,
        IReadOnlyList<FontInfo> libraryFonts,
        bool localAiEnabled,
        bool endpointReachable,
        int maxSuggestions = 3)
    {
        ArgumentNullException.ThrowIfNull(baseFont);
        ArgumentNullException.ThrowIfNull(libraryFonts);

        var candidates = libraryFonts
            .Where(font => !IsSameFace(baseFont, font))
            .DistinctBy(font => (Normalize(font.SourcePath), font.FaceIndex))
            .ToArray();

        var safeMaxSuggestions = Math.Clamp(maxSuggestions, 1, 3);

        var scored = candidates
            .Select(candidate => new
            {
                Font = candidate,
                Classification = FontClassificationRules.Classify(candidate),
                Score = Score(baseFont, candidate)
            })
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Font.Family, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Font.Subfamily, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Font.SourcePath, StringComparer.OrdinalIgnoreCase)
            .Take(safeMaxSuggestions)
            .ToArray();

        var pairings = scored
            .Select(entry => new FontPairingSuggestion(
                entry.Font,
                BuildRationale(baseFont, entry.Font, entry.Classification)))
            .ToArray();

        return new FontAiSuggestionResult(
            LocalAiEnabled: localAiEnabled,
            EndpointReachable: endpointReachable,
            UsedFallback: true,
            Pairings: pairings,
            Description: BuildAutoDescription(baseFont));
    }

    public static string BuildAutoDescription(FontInfo font)
    {
        ArgumentNullException.ThrowIfNull(font);

        var classification = FontClassificationRules.Classify(font);
        var tone = classification switch
        {
            FontClassification.Serif => "classic and editorial",
            FontClassification.SansSerif => "clean and modern",
            FontClassification.Monospace => "technical and code-friendly",
            FontClassification.Display => "expressive and attention-grabbing",
            _ => "versatile"
        };

        var weightTone = font.Weight switch
        {
            <= 300 => "light",
            >= 700 => "bold",
            _ => "balanced"
        };

        var slant = font.IsItalic ? "with an italic slant" : "with an upright posture";

        return $"{font.Family} {font.Subfamily} is a {tone} face with a {weightTone} weight and {slant}.";
    }

    private static int Score(FontInfo baseFont, FontInfo candidate)
    {
        var baseClassification = FontClassificationRules.Classify(baseFont);
        var candidateClassification = FontClassificationRules.Classify(candidate);

        var score = 0;

        if (StringComparer.OrdinalIgnoreCase.Equals(baseFont.Family, candidate.Family))
        {
            score -= 40;
        }

        if (candidateClassification == PreferredCompanion(baseClassification))
        {
            score += 35;
        }
        else if (candidateClassification != baseClassification)
        {
            score += 12;
        }

        var weightDelta = Math.Abs(baseFont.Weight - candidate.Weight);
        score += weightDelta switch
        {
            >= 200 and <= 500 => 20,
            >= 100 => 10,
            _ => 2
        };

        if (baseFont.IsItalic != candidate.IsItalic)
        {
            score += 5;
        }

        if (baseFont.Width != candidate.Width)
        {
            score += 3;
        }

        return score;
    }

    private static FontClassification PreferredCompanion(FontClassification classification)
        => classification switch
        {
            FontClassification.Serif => FontClassification.SansSerif,
            FontClassification.SansSerif => FontClassification.Serif,
            FontClassification.Display => FontClassification.SansSerif,
            FontClassification.Monospace => FontClassification.SansSerif,
            _ => FontClassification.SansSerif
        };

    private static string BuildRationale(FontInfo baseFont, FontInfo candidate, FontClassification candidateClassification)
    {
        var weightDelta = Math.Abs(baseFont.Weight - candidate.Weight);
        var contrast = weightDelta >= 200
            ? "strong weight contrast"
            : weightDelta >= 100
                ? "moderate weight contrast"
                : "subtle weight contrast";

        var classification = candidateClassification switch
        {
            FontClassification.Serif => "serif companion",
            FontClassification.SansSerif => "sans-serif companion",
            FontClassification.Monospace => "monospace companion",
            FontClassification.Display => "display companion",
            _ => "neutral companion"
        };

        return $"{classification} with {contrast}";
    }

    private static bool IsSameFace(FontInfo a, FontInfo b)
        => StringComparer.OrdinalIgnoreCase.Equals(Normalize(a.SourcePath), Normalize(b.SourcePath)) &&
           a.FaceIndex == b.FaceIndex;

    private static string Normalize(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path.Trim();
        }
    }
}
