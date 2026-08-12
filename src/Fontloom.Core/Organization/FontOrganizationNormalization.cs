namespace Fontloom.Core.Organization;

internal static class FontOrganizationNormalization
{
    public static string? NormalizePathOrNull(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var trimmed = path.Trim();

        try
        {
            return Path.GetFullPath(trimmed);
        }
        catch
        {
            return trimmed;
        }
    }

    public static string? NormalizeTagOrNull(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        return tag.Trim();
    }

    public static string? NormalizeCollectionNameOrNull(string? collectionName)
    {
        if (string.IsNullOrWhiteSpace(collectionName))
        {
            return null;
        }

        return collectionName.Trim();
    }

    public static IReadOnlyCollection<string> NormalizeUniquePaths(IEnumerable<string>? paths)
    {
        if (paths is null)
        {
            return Array.Empty<string>();
        }

        return paths
            .Select(NormalizePathOrNull)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyCollection<string> NormalizeUniqueTags(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return Array.Empty<string>();
        }

        return tags
            .Select(NormalizeTagOrNull)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
