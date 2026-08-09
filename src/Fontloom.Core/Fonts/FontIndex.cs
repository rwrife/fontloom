namespace Fontloom.Core.Fonts;

public sealed class FontIndex
{
    private readonly IReadOnlyList<IndexedFont> _fonts;

    private FontIndex(IReadOnlyList<IndexedFont> fonts)
    {
        _fonts = fonts;
    }

    public int Count => _fonts.Count;

    public static FontIndex Create(IEnumerable<FontInfo> fonts)
    {
        ArgumentNullException.ThrowIfNull(fonts);

        var indexedFonts = fonts
            .Select(font => new IndexedFont(
                Font: font,
                Classification: Classify(font),
                IsMonospace: IsMonospace(font)))
            .OrderBy(font => font.Font.Family, StringComparer.OrdinalIgnoreCase)
            .ThenBy(font => font.Font.Subfamily, StringComparer.OrdinalIgnoreCase)
            .ThenBy(font => font.Font.Weight)
            .ThenBy(font => font.Font.IsItalic)
            .ThenBy(font => font.Font.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(font => font.Font.FaceIndex)
            .ToArray();

        return new FontIndex(indexedFonts);
    }

    public IReadOnlyList<FontInfo> Query(FontIndexQuery? query = null)
    {
        IEnumerable<IndexedFont> filtered = _fonts;

        if (query is not null)
        {
            if (!string.IsNullOrWhiteSpace(query.FamilyNameContains))
            {
                var term = query.FamilyNameContains.Trim();
                filtered = filtered.Where(font =>
                    font.Font.Family.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            if (query.Classifications is { Count: > 0 })
            {
                var accepted = new HashSet<FontClassification>(query.Classifications);
                filtered = filtered.Where(font => accepted.Contains(font.Classification));
            }

            if (query.MinimumWeight.HasValue)
            {
                var minimumWeight = query.MinimumWeight.Value;
                filtered = filtered.Where(font => font.Font.Weight >= minimumWeight);
            }

            if (query.MaximumWeight.HasValue)
            {
                var maximumWeight = query.MaximumWeight.Value;
                filtered = filtered.Where(font => font.Font.Weight <= maximumWeight);
            }

            if (query.IsItalic.HasValue)
            {
                var isItalic = query.IsItalic.Value;
                filtered = filtered.Where(font => font.Font.IsItalic == isItalic);
            }

            if (query.IsMonospace.HasValue)
            {
                var isMonospace = query.IsMonospace.Value;
                filtered = filtered.Where(font => font.IsMonospace == isMonospace);
            }

            if (!string.IsNullOrWhiteSpace(query.SupportsText))
            {
                var sampleText = query.SupportsText;
                filtered = filtered.Where(font => font.Font.Coverage.SupportsText(sampleText));
            }
        }

        return filtered.Select(font => font.Font).ToArray();
    }

    private static FontClassification Classify(FontInfo font)
    {
        var combinedName = $"{font.Family} {font.Subfamily}";

        if (ContainsAny(combinedName, "mono", "code", "console", "fixed"))
        {
            return FontClassification.Monospace;
        }

        if (ContainsAny(combinedName, "display", "script", "decorative", "blackletter"))
        {
            return FontClassification.Display;
        }

        if (ContainsAny(combinedName, "sans", "grotesk", "grotesque"))
        {
            return FontClassification.SansSerif;
        }

        if (ContainsAny(combinedName, "serif", "roman", "times", "garamond", "georgia", "baskerville", "palatino"))
        {
            return FontClassification.Serif;
        }

        return FontClassification.Unknown;
    }

    private static bool IsMonospace(FontInfo font)
        => Classify(font) == FontClassification.Monospace;

    private static bool ContainsAny(string value, params string[] terms)
    {
        foreach (var term in terms)
        {
            if (value.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record IndexedFont(FontInfo Font, FontClassification Classification, bool IsMonospace);
}
