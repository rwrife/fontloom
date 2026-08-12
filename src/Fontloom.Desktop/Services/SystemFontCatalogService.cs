using Fontloom.Core.Fonts;
using Fontloom.Core.Organization;

namespace Fontloom.Desktop.Services;

public sealed class SystemFontCatalogService : IFontCatalogService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ttf",
        ".otf",
        ".ttc",
        ".woff",
        ".woff2"
    };

    private readonly IFontOrganizationStore _organizationStore;
    private readonly IFontFileReader _fontFileReader;
    private readonly ISystemFontSource _systemFontSource;
    private readonly CachedFontIndexBuilder _cachedFontIndexBuilder;

    public SystemFontCatalogService(
        IFontOrganizationStore? organizationStore = null,
        IFontFileReader? fontFileReader = null,
        ISystemFontSource? systemFontSource = null)
    {
        _organizationStore = organizationStore ?? new JsonFontOrganizationStore();
        _fontFileReader = fontFileReader ?? new OpenTypeFontFileReader();
        _systemFontSource = systemFontSource ?? SystemFontSourceFactory.CreateDefault(_fontFileReader);
        _cachedFontIndexBuilder = new CachedFontIndexBuilder(_fontFileReader);
    }

    public FontIndex BuildIndex(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var systemFonts = _systemFontSource.EnumerateFonts(cancellationToken: cancellationToken);

        var looseFontFolders = _organizationStore.GetSnapshot().LooseFontFolders;
        var looseFontFiles = EnumerateLooseFontFiles(looseFontFolders, cancellationToken);

        var looseFontCachePath = FontloomStoragePaths.ResolveLooseFontIndexCachePath();
        var looseFontIndex = _cachedFontIndexBuilder.Build(looseFontFiles, looseFontCachePath, cancellationToken);

        var allFonts = new List<FontInfo>(systemFonts.Count + looseFontIndex.Index.Count);
        allFonts.AddRange(systemFonts);
        allFonts.AddRange(looseFontIndex.Index.Query());

        return FontIndex.Create(FontInfoDeduplicator.Deduplicate(allFonts));
    }

    private static IReadOnlyList<string> EnumerateLooseFontFiles(
        IEnumerable<string> looseFontFolders,
        CancellationToken cancellationToken)
    {
        var discoveredFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var looseFontFolder in looseFontFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(looseFontFolder))
            {
                continue;
            }

            var normalizedRoot = looseFontFolder.Trim();
            if (!Directory.Exists(normalizedRoot))
            {
                continue;
            }

            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(normalizedRoot);

            while (pendingDirectories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var currentDirectory = pendingDirectories.Pop();

                IEnumerable<string> childDirectories;
                try
                {
                    childDirectories = Directory.EnumerateDirectories(currentDirectory);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (var childDirectory in childDirectories)
                {
                    pendingDirectories.Push(childDirectory);
                }

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(currentDirectory);
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
                    if (!SupportedExtensions.Contains(Path.GetExtension(file)))
                    {
                        continue;
                    }

                    var normalizedFile = NormalizePath(file);
                    discoveredFiles.Add(normalizedFile);
                }
            }
        }

        return discoveredFiles
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
}
