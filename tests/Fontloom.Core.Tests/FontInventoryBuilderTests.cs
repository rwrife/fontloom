using FluentAssertions;
using Fontloom.Core.Fonts;

namespace Fontloom.Core.Tests;

public class FontInventoryBuilderTests
{
    [Fact]
    public void EnumerateFonts_MergesSystemAndLooseFonts_AndDeduplicatesByFamilyStyleAndPath()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var duplicatePath = Path.Combine(tempDirectory.FullName, "Inter-Regular.ttf");
            var uniquePath = Path.Combine(tempDirectory.FullName, "Inter-Bold.ttf");
            File.WriteAllText(duplicatePath, "placeholder");
            File.WriteAllText(uniquePath, "placeholder");

            var duplicateFont = CreateFontInfo(duplicatePath, "Inter", "Regular");
            var uniqueFont = CreateFontInfo(uniquePath, "Inter", "Bold");

            var systemSource = new StubSystemFontSource([duplicateFont, duplicateFont]);
            var fileReader = new StubFontFileReader(new Dictionary<string, IReadOnlyList<FontInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                [duplicatePath] = [duplicateFont],
                [uniquePath] = [uniqueFont]
            });

            var inventoryBuilder = new FontInventoryBuilder(systemSource, fileReader);

            var result = inventoryBuilder.EnumerateFonts([tempDirectory.FullName]);

            result.Should().HaveCount(2);
            result.Should().ContainSingle(font => font.Family == "Inter" && font.Subfamily == "Regular");
            result.Should().ContainSingle(font => font.Family == "Inter" && font.Subfamily == "Bold");
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void EnumerateFonts_WhenCancelled_ThrowsOperationCanceledException()
    {
        var systemSource = new StubSystemFontSource([]);
        var fileReader = new StubFontFileReader(new Dictionary<string, IReadOnlyList<FontInfo>>());
        var inventoryBuilder = new FontInventoryBuilder(systemSource, fileReader);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var act = () => inventoryBuilder.EnumerateFonts(cancellationToken: cancellation.Token);

        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void EnumerateFonts_ReportsProgressAcrossSystemAndLooseEnumeration()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var loosePath = Path.Combine(tempDirectory.FullName, "NotoSans-Regular.ttf");
            File.WriteAllText(loosePath, "placeholder");

            var systemFont = CreateFontInfo("/System/Library/Fonts/SystemFont.ttf", "System Font", "Regular");
            var looseFont = CreateFontInfo(loosePath, "Noto Sans", "Regular");

            var systemSource = new StubSystemFontSource([systemFont], processedFileCount: 3);
            var fileReader = new StubFontFileReader(new Dictionary<string, IReadOnlyList<FontInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                [loosePath] = [looseFont]
            });

            var inventoryBuilder = new FontInventoryBuilder(systemSource, fileReader);
            var progressEvents = new List<SystemFontEnumerationProgress>();
            var progress = new CapturingProgress(progressEvents);

            _ = inventoryBuilder.EnumerateFonts([loosePath], progress);

            progressEvents.Should().NotBeEmpty();
            progressEvents.Should().Contain(report => report.ProcessedFileCount == 3 && report.CurrentPath == systemFont.SourcePath);
            progressEvents.Should().Contain(report => report.ProcessedFileCount == 4 && report.CurrentPath == loosePath);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    private static FontInfo CreateFontInfo(string path, string family, string subfamily)
        => new(
            SourcePath: path,
            FaceIndex: 0,
            Family: family,
            Subfamily: subfamily,
            Weight: 400,
            Width: FontWidthClass.Normal,
            IsItalic: false,
            Format: FontContainerFormat.TrueType,
            Coverage: new GlyphCoverageSummary(
                GlyphCount: 100,
                MappedCodePointCount: 100,
                SupportsBasicLatin: true,
                SupportsLatin1Supplement: true,
                SupportsLatinExtendedA: false,
                SupportsGreekAndCoptic: false,
                SupportsCyrillic: false,
                SupportsCjkUnifiedIdeographs: false));

    private sealed class StubSystemFontSource : ISystemFontSource
    {
        private readonly IReadOnlyList<FontInfo> _fonts;
        private readonly int _processedFileCount;

        public StubSystemFontSource(IReadOnlyList<FontInfo> fonts, int processedFileCount = 1)
        {
            _fonts = fonts;
            _processedFileCount = processedFileCount;
        }

        public IReadOnlyList<FontInfo> EnumerateFonts(
            IProgress<SystemFontEnumerationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentPath = _fonts.FirstOrDefault()?.SourcePath;
            progress?.Report(new SystemFontEnumerationProgress(_processedFileCount, _fonts.Count, currentPath));
            return _fonts;
        }
    }

    private sealed class StubFontFileReader : IFontFileReader
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<FontInfo>> _responses;

        public StubFontFileReader(IReadOnlyDictionary<string, IReadOnlyList<FontInfo>> responses)
        {
            _responses = responses;
        }

        public IReadOnlyList<FontInfo> Read(string path)
        {
            if (_responses.TryGetValue(path, out var fonts))
            {
                return fonts;
            }

            throw new FontReadException(
                FontReadErrorCode.FileNotFound,
                path,
                $"No fixture mapping available for '{path}'.");
        }
    }

    private sealed class CapturingProgress : IProgress<SystemFontEnumerationProgress>
    {
        private readonly List<SystemFontEnumerationProgress> _events;

        public CapturingProgress(List<SystemFontEnumerationProgress> events)
        {
            _events = events;
        }

        public void Report(SystemFontEnumerationProgress value)
            => _events.Add(value);
    }
}
