namespace Fontloom.Core.Fonts;

public static class FontClassificationRules
{
    public static FontClassification Classify(FontInfo font)
    {
        ArgumentNullException.ThrowIfNull(font);

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

    public static bool IsMonospace(FontInfo font)
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
}
