namespace Fontloom.Core.Fonts;

public sealed class FontInventoryBuilder
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ttf",
        ".otf",
        ".ttc",
        ".woff",
        ".woff2"
    };

    private readonly ISystemFontSource _systemFontSource;
    private readonly IFontFileReader _fontFileReader;

    public FontInventoryBuilder(ISystemFontSource systemFontSource, IFontFileReader fontFileReader)
    {
        ArgumentNullException.ThrowIfNull(systemFontSource);
        ArgumentNullException.ThrowIfNull(fontFileReader);

        _systemFontSource = systemFontSource;
        _fontFileReader = fontFileReader;
    }

    public IReadOnlyList<FontInfo> EnumerateFonts(
        IEnumerable<string>? looseFontPaths = null,
        IProgress<SystemFontEnumerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var discoveredFaces = new List<FontInfo>();
        var lastSystemProcessedFileCount = 0;

        IProgress<SystemFontEnumerationProgress>? systemProgress = null;
        if (progress is not null)
        {
            systemProgress = new Progress<SystemFontEnumerationProgress>(reported =>
            {
                lastSystemProcessedFileCount = reported.ProcessedFileCount;
                progress.Report(reported);
            });
        }

        discoveredFaces.AddRange(_systemFontSource.EnumerateFonts(systemProgress, cancellationToken));

        var looseProcessedFileCount = 0;
        foreach (var file in ExpandLooseFontPaths(looseFontPaths))
        {
            cancellationToken.ThrowIfCancellationRequested();
            looseProcessedFileCount++;

            try
            {
                discoveredFaces.AddRange(_fontFileReader.Read(file));
            }
            catch (FontReadException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            progress?.Report(new SystemFontEnumerationProgress(
                ProcessedFileCount: lastSystemProcessedFileCount + looseProcessedFileCount,
                DiscoveredFaceCount: discoveredFaces.Count,
                CurrentPath: file));
        }

        return FontInfoDeduplicator.Deduplicate(discoveredFaces);
    }

    private static IEnumerable<string> ExpandLooseFontPaths(IEnumerable<string>? looseFontPaths)
    {
        if (looseFontPaths is null)
        {
            yield break;
        }

        var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var looseFontPath in looseFontPaths)
        {
            if (string.IsNullOrWhiteSpace(looseFontPath))
            {
                continue;
            }

            var normalizedInput = looseFontPath.Trim();

            if (File.Exists(normalizedInput))
            {
                if (IsSupportedFontFile(normalizedInput) && seenFiles.Add(NormalizePath(normalizedInput)))
                {
                    yield return normalizedInput;
                }

                continue;
            }

            if (!Directory.Exists(normalizedInput))
            {
                continue;
            }

            foreach (var fontFile in EnumerateDirectoryFontFiles(normalizedInput))
            {
                if (seenFiles.Add(NormalizePath(fontFile)))
                {
                    yield return fontFile;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateDirectoryFontFiles(string rootDirectory)
    {
        var pending = new Stack<string>();
        pending.Push(rootDirectory);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            IEnumerable<string> subDirectories;
            try
            {
                subDirectories = Directory.EnumerateDirectories(current);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var subDirectory in subDirectories)
            {
                pending.Push(subDirectory);
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                if (IsSupportedFontFile(file))
                {
                    yield return file;
                }
            }
        }
    }

    private static bool IsSupportedFontFile(string path)
        => SupportedExtensions.Contains(Path.GetExtension(path));

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }
}
