namespace Fontloom.Core.Fonts;

public abstract class DirectorySystemFontSource : ISystemFontSource
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ttf",
        ".otf",
        ".ttc",
        ".woff",
        ".woff2"
    };

    private readonly IFontFileReader _fontFileReader;
    private readonly IReadOnlyList<string> _fontDirectories;

    protected DirectorySystemFontSource(IFontFileReader fontFileReader, IEnumerable<string> fontDirectories)
    {
        ArgumentNullException.ThrowIfNull(fontFileReader);
        ArgumentNullException.ThrowIfNull(fontDirectories);

        _fontFileReader = fontFileReader;
        _fontDirectories = fontDirectories
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<FontInfo> EnumerateFonts(
        IProgress<SystemFontEnumerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var discoveredFaces = new List<FontInfo>();
        var processedFileCount = 0;

        foreach (var directory in _fontDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in EnumerateFontFiles(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                processedFileCount++;

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
                    ProcessedFileCount: processedFileCount,
                    DiscoveredFaceCount: discoveredFaces.Count,
                    CurrentPath: file));
            }
        }

        return FontInfoDeduplicator.Deduplicate(discoveredFaces);
    }

    private static IEnumerable<string> EnumerateFontFiles(string rootDirectory)
    {
        var pending = new Stack<string>();
        pending.Push(rootDirectory);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();

            IEnumerable<string> subDirectories;
            try
            {
                subDirectories = Directory.EnumerateDirectories(directory);
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
                files = Directory.EnumerateFiles(directory);
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
                var extension = Path.GetExtension(file);
                if (SupportedExtensions.Contains(extension))
                {
                    yield return file;
                }
            }
        }
    }
}
