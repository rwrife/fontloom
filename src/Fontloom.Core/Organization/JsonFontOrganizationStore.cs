using System.Text.Json;

namespace Fontloom.Core.Organization;

public sealed class JsonFontOrganizationStore : IFontOrganizationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _storagePath;
    private readonly object _sync = new();

    private StoreDocument _document;

    public JsonFontOrganizationStore(string? storagePath = null)
    {
        _storagePath = storagePath ?? FontloomStoragePaths.ResolveOrganizationStorePath();
        _document = LoadDocument(_storagePath);
    }

    public FontOrganizationSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return CreateSnapshot(_document);
        }
    }

    public bool SetFavorite(string fontPath, bool isFavorite)
    {
        var normalizedPath = FontOrganizationNormalization.NormalizePathOrNull(fontPath);
        if (normalizedPath is null)
        {
            return false;
        }

        return Mutate(document =>
        {
            return isFavorite
                ? document.Favorites.Add(normalizedPath)
                : document.Favorites.Remove(normalizedPath);
        });
    }

    public bool SetTags(string fontPath, IEnumerable<string> tags)
    {
        var normalizedPath = FontOrganizationNormalization.NormalizePathOrNull(fontPath);
        if (normalizedPath is null)
        {
            return false;
        }

        var normalizedTags = FontOrganizationNormalization.NormalizeUniqueTags(tags);

        return Mutate(document =>
        {
            if (normalizedTags.Count == 0)
            {
                return document.TagsByPath.Remove(normalizedPath);
            }

            if (document.TagsByPath.TryGetValue(normalizedPath, out var existing) &&
                existing.Count == normalizedTags.Count &&
                normalizedTags.All(tag => existing.Contains(tag)))
            {
                return false;
            }

            document.TagsByPath[normalizedPath] = new HashSet<string>(normalizedTags, StringComparer.OrdinalIgnoreCase);
            return true;
        });
    }

    public bool AddTag(string fontPath, string tag)
    {
        var normalizedPath = FontOrganizationNormalization.NormalizePathOrNull(fontPath);
        var normalizedTag = FontOrganizationNormalization.NormalizeTagOrNull(tag);
        if (normalizedPath is null || normalizedTag is null)
        {
            return false;
        }

        return Mutate(document =>
        {
            if (!document.TagsByPath.TryGetValue(normalizedPath, out var tags))
            {
                tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                document.TagsByPath[normalizedPath] = tags;
            }

            return tags.Add(normalizedTag);
        });
    }

    public bool RemoveTag(string fontPath, string tag)
    {
        var normalizedPath = FontOrganizationNormalization.NormalizePathOrNull(fontPath);
        var normalizedTag = FontOrganizationNormalization.NormalizeTagOrNull(tag);
        if (normalizedPath is null || normalizedTag is null)
        {
            return false;
        }

        return Mutate(document =>
        {
            if (!document.TagsByPath.TryGetValue(normalizedPath, out var tags))
            {
                return false;
            }

            var removed = tags.Remove(normalizedTag);
            if (tags.Count == 0)
            {
                document.TagsByPath.Remove(normalizedPath);
            }

            return removed;
        });
    }

    public bool CreateCollection(string collectionName)
    {
        var normalizedName = FontOrganizationNormalization.NormalizeCollectionNameOrNull(collectionName);
        if (normalizedName is null)
        {
            return false;
        }

        return Mutate(document =>
        {
            if (document.Collections.ContainsKey(normalizedName))
            {
                return false;
            }

            document.Collections[normalizedName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return true;
        });
    }

    public bool DeleteCollection(string collectionName)
    {
        var normalizedName = FontOrganizationNormalization.NormalizeCollectionNameOrNull(collectionName);
        if (normalizedName is null)
        {
            return false;
        }

        return Mutate(document => document.Collections.Remove(normalizedName));
    }

    public bool AddFontToCollection(string collectionName, string fontPath)
    {
        var normalizedName = FontOrganizationNormalization.NormalizeCollectionNameOrNull(collectionName);
        var normalizedPath = FontOrganizationNormalization.NormalizePathOrNull(fontPath);
        if (normalizedName is null || normalizedPath is null)
        {
            return false;
        }

        return Mutate(document =>
        {
            if (!document.Collections.TryGetValue(normalizedName, out var members))
            {
                members = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                document.Collections[normalizedName] = members;
            }

            return members.Add(normalizedPath);
        });
    }

    public bool RemoveFontFromCollection(string collectionName, string fontPath)
    {
        var normalizedName = FontOrganizationNormalization.NormalizeCollectionNameOrNull(collectionName);
        var normalizedPath = FontOrganizationNormalization.NormalizePathOrNull(fontPath);
        if (normalizedName is null || normalizedPath is null)
        {
            return false;
        }

        return Mutate(document =>
        {
            if (!document.Collections.TryGetValue(normalizedName, out var members))
            {
                return false;
            }

            return members.Remove(normalizedPath);
        });
    }

    public bool AddLooseFontFolder(string folderPath)
    {
        var normalizedPath = FontOrganizationNormalization.NormalizePathOrNull(folderPath);
        if (normalizedPath is null)
        {
            return false;
        }

        return Mutate(document => document.LooseFontFolders.Add(normalizedPath));
    }

    public bool RemoveLooseFontFolder(string folderPath)
    {
        var normalizedPath = FontOrganizationNormalization.NormalizePathOrNull(folderPath);
        if (normalizedPath is null)
        {
            return false;
        }

        return Mutate(document => document.LooseFontFolders.Remove(normalizedPath));
    }

    private bool Mutate(Func<StoreDocument, bool> mutator)
    {
        lock (_sync)
        {
            if (!mutator(_document))
            {
                return false;
            }

            SaveDocument(_storagePath, _document);
            return true;
        }
    }

    private static StoreDocument LoadDocument(string storagePath)
    {
        if (!File.Exists(storagePath))
        {
            return new StoreDocument();
        }

        try
        {
            var json = File.ReadAllText(storagePath);
            var rawDocument = JsonSerializer.Deserialize<StoreDocumentData>(json, SerializerOptions);
            return Normalize(rawDocument);
        }
        catch
        {
            return new StoreDocument();
        }
    }

    private static void SaveDocument(string storagePath, StoreDocument document)
    {
        var directory = Path.GetDirectoryName(storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var data = new StoreDocumentData
        {
            Version = 1,
            FavoriteFontPaths = document.Favorites
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            TagsByFontPath = document.TagsByPath
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value
                        .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    StringComparer.OrdinalIgnoreCase),
            Collections = document.Collections
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    StringComparer.OrdinalIgnoreCase),
            LooseFontFolders = document.LooseFontFolders
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };

        var json = JsonSerializer.Serialize(data, SerializerOptions);
        File.WriteAllText(storagePath, json);
    }

    private static StoreDocument Normalize(StoreDocumentData? raw)
    {
        var document = new StoreDocument();

        foreach (var favoritePath in FontOrganizationNormalization.NormalizeUniquePaths(raw?.FavoriteFontPaths))
        {
            document.Favorites.Add(favoritePath);
        }

        if (raw?.TagsByFontPath is not null)
        {
            foreach (var pair in raw.TagsByFontPath)
            {
                var normalizedPath = FontOrganizationNormalization.NormalizePathOrNull(pair.Key);
                if (normalizedPath is null)
                {
                    continue;
                }

                var tags = FontOrganizationNormalization.NormalizeUniqueTags(pair.Value);
                if (tags.Count == 0)
                {
                    continue;
                }

                document.TagsByPath[normalizedPath] = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
            }
        }

        if (raw?.Collections is not null)
        {
            foreach (var pair in raw.Collections)
            {
                var normalizedName = FontOrganizationNormalization.NormalizeCollectionNameOrNull(pair.Key);
                if (normalizedName is null)
                {
                    continue;
                }

                var members = FontOrganizationNormalization.NormalizeUniquePaths(pair.Value);
                document.Collections[normalizedName] = new HashSet<string>(members, StringComparer.OrdinalIgnoreCase);
            }
        }

        foreach (var folderPath in FontOrganizationNormalization.NormalizeUniquePaths(raw?.LooseFontFolders))
        {
            document.LooseFontFolders.Add(folderPath);
        }

        return document;
    }

    private static FontOrganizationSnapshot CreateSnapshot(StoreDocument document)
    {
        return new FontOrganizationSnapshot(
            FavoriteFontPaths: document.Favorites
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            TagsByFontPath: document.TagsByPath
                .ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyCollection<string>)pair.Value
                        .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    StringComparer.OrdinalIgnoreCase),
            Collections: document.Collections
                .ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyCollection<string>)pair.Value
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    StringComparer.OrdinalIgnoreCase),
            LooseFontFolders: document.LooseFontFolders
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private sealed class StoreDocument
    {
        public HashSet<string> Favorites { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, HashSet<string>> TagsByPath { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, HashSet<string>> Collections { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> LooseFontFolders { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class StoreDocumentData
    {
        public int Version { get; set; } = 1;

        public string[] FavoriteFontPaths { get; set; } = Array.Empty<string>();

        public Dictionary<string, string[]> TagsByFontPath { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string[]> Collections { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public string[] LooseFontFolders { get; set; } = Array.Empty<string>();
    }
}
