using System.Text.Json;

namespace Fontloom.Core.Fonts;

public sealed record FontIndexBuildResult(
    FontIndex Index,
    string CachePath,
    int LoadedFromCacheFileCount,
    int RefreshedFileCount,
    int FailedFileCount);

public sealed class CachedFontIndexBuilder
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ttf",
        ".otf",
        ".ttc",
        ".woff",
        ".woff2"
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly IFontFileReader _fontFileReader;

    public CachedFontIndexBuilder(IFontFileReader fontFileReader)
    {
        ArgumentNullException.ThrowIfNull(fontFileReader);
        _fontFileReader = fontFileReader;
    }

    public FontIndexBuildResult Build(
        IEnumerable<string> fontFiles,
        string? cachePath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fontFiles);

        cachePath ??= ResolveDefaultCachePath();

        var cachedEntries = LoadCacheEntries(cachePath);
        var nextCacheEntries = new List<CacheEntry>();

        var discoveredFonts = new List<FontInfo>();
        var loadedFromCacheFileCount = 0;
        var refreshedFileCount = 0;
        var failedFileCount = 0;

        foreach (var fontFile in NormalizeInputFiles(fontFiles))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedPath = NormalizePath(fontFile);
            if (!File.Exists(normalizedPath))
            {
                continue;
            }

            FontFileStamp stamp;
            try
            {
                var fileInfo = new FileInfo(normalizedPath);
                stamp = new FontFileStamp(fileInfo.Length, fileInfo.LastWriteTimeUtc.Ticks);
            }
            catch (IOException)
            {
                failedFileCount++;
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                failedFileCount++;
                continue;
            }

            if (cachedEntries.TryGetValue(normalizedPath, out var cachedEntry) && cachedEntry.Stamp == stamp)
            {
                discoveredFonts.AddRange(cachedEntry.Fonts);
                nextCacheEntries.Add(cachedEntry);
                loadedFromCacheFileCount++;
                continue;
            }

            IReadOnlyList<FontInfo> refreshedFonts;
            try
            {
                refreshedFonts = _fontFileReader.Read(normalizedPath);
            }
            catch (FontReadException)
            {
                failedFileCount++;
                continue;
            }
            catch (IOException)
            {
                failedFileCount++;
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                failedFileCount++;
                continue;
            }

            discoveredFonts.AddRange(refreshedFonts);
            nextCacheEntries.Add(new CacheEntry(normalizedPath, stamp, refreshedFonts.ToArray()));
            refreshedFileCount++;
        }

        SaveCacheEntries(cachePath, nextCacheEntries);

        var deduplicatedFonts = FontInfoDeduplicator.Deduplicate(discoveredFonts);
        return new FontIndexBuildResult(
            Index: FontIndex.Create(deduplicatedFonts),
            CachePath: cachePath,
            LoadedFromCacheFileCount: loadedFromCacheFileCount,
            RefreshedFileCount: refreshedFileCount,
            FailedFileCount: failedFileCount);
    }

    public static string ResolveDefaultCachePath()
    {
        string appDataRoot;
        if (OperatingSystem.IsWindows())
        {
            appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else if (OperatingSystem.IsMacOS())
        {
            appDataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support");
        }
        else
        {
            appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        return Path.Combine(appDataRoot, "fontloom", "font-index-cache.json");
    }

    private static IEnumerable<string> NormalizeInputFiles(IEnumerable<string> fontFiles)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in fontFiles)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            if (!SupportedExtensions.Contains(Path.GetExtension(path)))
            {
                continue;
            }

            var normalizedPath = NormalizePath(path);
            if (seen.Add(normalizedPath))
            {
                yield return normalizedPath;
            }
        }
    }

    private static Dictionary<string, CacheEntry> LoadCacheEntries(string cachePath)
    {
        if (!File.Exists(cachePath))
        {
            return new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(cachePath);
            var cacheDocument = JsonSerializer.Deserialize<CacheDocument>(json, SerializerOptions);
            if (cacheDocument is null)
            {
                return new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
            }

            return cacheDocument.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.SourcePath))
                .ToDictionary(
                    entry => NormalizePath(entry.SourcePath),
                    entry => new CacheEntry(
                        NormalizePath(entry.SourcePath),
                        new FontFileStamp(entry.FileLength, entry.LastWriteUtcTicks),
                        entry.Fonts ?? []),
                    StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void SaveCacheEntries(string cachePath, IReadOnlyList<CacheEntry> entries)
    {
        var directory = Path.GetDirectoryName(cachePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var document = new CacheDocument(
            Version: 1,
            Entries: entries
                .Select(entry => new CacheDocumentEntry(
                    SourcePath: entry.SourcePath,
                    LastWriteUtcTicks: entry.Stamp.LastWriteUtcTicks,
                    FileLength: entry.Stamp.FileLength,
                    Fonts: entry.Fonts.ToArray()))
                .ToArray());

        var json = JsonSerializer.Serialize(document, SerializerOptions);
        File.WriteAllText(cachePath, json);
    }

    private static string NormalizePath(string path)
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

    private sealed record FontFileStamp(long FileLength, long LastWriteUtcTicks);

    private sealed record CacheEntry(string SourcePath, FontFileStamp Stamp, IReadOnlyList<FontInfo> Fonts);

    private sealed record CacheDocument(int Version, IReadOnlyList<CacheDocumentEntry> Entries);

    private sealed record CacheDocumentEntry(
        string SourcePath,
        long LastWriteUtcTicks,
        long FileLength,
        IReadOnlyList<FontInfo>? Fonts);
}
