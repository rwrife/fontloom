namespace Fontloom.Core.Fonts;

internal static class FontInfoDeduplicator
{
    public static IReadOnlyList<FontInfo> Deduplicate(IEnumerable<FontInfo> fonts)
    {
        var deduplicated = new List<FontInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var font in fonts)
        {
            var key = BuildKey(font);
            if (seen.Add(key))
            {
                deduplicated.Add(font);
            }
        }

        return deduplicated;
    }

    private static string BuildKey(FontInfo font)
    {
        var normalizedPath = NormalizePath(font.SourcePath);
        var family = font.Family.Trim();
        var subfamily = font.Subfamily.Trim();

        return $"{normalizedPath}|{family}|{subfamily}";
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path).Trim();
        }
        catch
        {
            return path.Trim();
        }
    }
}
