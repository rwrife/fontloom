namespace Fontloom.Core.Organization;

public sealed class InMemoryFontOrganizationStore : IFontOrganizationStore
{
    private readonly object _sync = new();

    private readonly HashSet<string> _favorites = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _tagsByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _collections = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _looseFontFolders = new(StringComparer.OrdinalIgnoreCase);

    public FontOrganizationSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return new FontOrganizationSnapshot(
                FavoriteFontPaths: _favorites
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                TagsByFontPath: _tagsByPath
                    .ToDictionary(
                        pair => pair.Key,
                        pair => (IReadOnlyCollection<string>)pair.Value
                            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                            .ToArray(),
                        StringComparer.OrdinalIgnoreCase),
                Collections: _collections
                    .ToDictionary(
                        pair => pair.Key,
                        pair => (IReadOnlyCollection<string>)pair.Value
                            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                            .ToArray(),
                        StringComparer.OrdinalIgnoreCase),
                LooseFontFolders: _looseFontFolders
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        }
    }

    public bool SetFavorite(string fontPath, bool isFavorite)
    {
        var normalizedPath = FontOrganizationNormalization.NormalizePathOrNull(fontPath);
        if (normalizedPath is null)
        {
            return false;
        }

        lock (_sync)
        {
            return isFavorite
                ? _favorites.Add(normalizedPath)
                : _favorites.Remove(normalizedPath);
        }
    }

    public bool SetTags(string fontPath, IEnumerable<string> tags)
    {
        var normalizedPath = FontOrganizationNormalization.NormalizePathOrNull(fontPath);
        if (normalizedPath is null)
        {
            return false;
        }

        var normalizedTags = FontOrganizationNormalization.NormalizeUniqueTags(tags);

        lock (_sync)
        {
            if (normalizedTags.Count == 0)
            {
                return _tagsByPath.Remove(normalizedPath);
            }

            if (_tagsByPath.TryGetValue(normalizedPath, out var existing) &&
                existing.Count == normalizedTags.Count &&
                normalizedTags.All(tag => existing.Contains(tag)))
            {
                return false;
            }

            _tagsByPath[normalizedPath] = new HashSet<string>(normalizedTags, StringComparer.OrdinalIgnoreCase);
            return true;
        }
    }

    public bool AddTag(string fontPath, string tag)
    {
        var normalizedPath = FontOrganizationNormalization.NormalizePathOrNull(fontPath);
        var normalizedTag = FontOrganizationNormalization.NormalizeTagOrNull(tag);
        if (normalizedPath is null || normalizedTag is null)
        {
            return false;
        }

        lock (_sync)
        {
            if (!_tagsByPath.TryGetValue(normalizedPath, out var tagSet))
            {
                tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _tagsByPath[normalizedPath] = tagSet;
            }

            return tagSet.Add(normalizedTag);
        }
    }

    public bool RemoveTag(string fontPath, string tag)
    {
        var normalizedPath = FontOrganizationNormalization.NormalizePathOrNull(fontPath);
        var normalizedTag = FontOrganizationNormalization.NormalizeTagOrNull(tag);
        if (normalizedPath is null || normalizedTag is null)
        {
            return false;
        }

        lock (_sync)
        {
            if (!_tagsByPath.TryGetValue(normalizedPath, out var tagSet))
            {
                return false;
            }

            var removed = tagSet.Remove(normalizedTag);
            if (tagSet.Count == 0)
            {
                _tagsByPath.Remove(normalizedPath);
            }

            return removed;
        }
    }

    public bool CreateCollection(string collectionName)
    {
        var normalizedName = FontOrganizationNormalization.NormalizeCollectionNameOrNull(collectionName);
        if (normalizedName is null)
        {
            return false;
        }

        lock (_sync)
        {
            if (_collections.ContainsKey(normalizedName))
            {
                return false;
            }

            _collections[normalizedName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return true;
        }
    }

    public bool DeleteCollection(string collectionName)
    {
        var normalizedName = FontOrganizationNormalization.NormalizeCollectionNameOrNull(collectionName);
        if (normalizedName is null)
        {
            return false;
        }

        lock (_sync)
        {
            return _collections.Remove(normalizedName);
        }
    }

    public bool AddFontToCollection(string collectionName, string fontPath)
    {
        var normalizedName = FontOrganizationNormalization.NormalizeCollectionNameOrNull(collectionName);
        var normalizedPath = FontOrganizationNormalization.NormalizePathOrNull(fontPath);
        if (normalizedName is null || normalizedPath is null)
        {
            return false;
        }

        lock (_sync)
        {
            if (!_collections.TryGetValue(normalizedName, out var members))
            {
                members = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _collections[normalizedName] = members;
            }

            return members.Add(normalizedPath);
        }
    }

    public bool RemoveFontFromCollection(string collectionName, string fontPath)
    {
        var normalizedName = FontOrganizationNormalization.NormalizeCollectionNameOrNull(collectionName);
        var normalizedPath = FontOrganizationNormalization.NormalizePathOrNull(fontPath);
        if (normalizedName is null || normalizedPath is null)
        {
            return false;
        }

        lock (_sync)
        {
            if (!_collections.TryGetValue(normalizedName, out var members))
            {
                return false;
            }

            return members.Remove(normalizedPath);
        }
    }

    public bool AddLooseFontFolder(string folderPath)
    {
        var normalizedPath = FontOrganizationNormalization.NormalizePathOrNull(folderPath);
        if (normalizedPath is null)
        {
            return false;
        }

        lock (_sync)
        {
            return _looseFontFolders.Add(normalizedPath);
        }
    }

    public bool RemoveLooseFontFolder(string folderPath)
    {
        var normalizedPath = FontOrganizationNormalization.NormalizePathOrNull(folderPath);
        if (normalizedPath is null)
        {
            return false;
        }

        lock (_sync)
        {
            return _looseFontFolders.Remove(normalizedPath);
        }
    }
}
