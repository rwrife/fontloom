using System.Diagnostics;
using FluentAssertions;
using Fontloom.Core.Fonts;

namespace Fontloom.Core.Tests;

public class FontIndexTests
{
    [Fact]
    public void Query_AppliesFacetFilters_AndReturnsDeterministicOrder()
    {
        var coverage = CreateCoverage(new CodePointRange(0x20, 0x7E));

        var fonts = new[]
        {
            CreateFont("Inter Sans", "Bold", 700, false, "/fonts/inter-bold.ttf", coverage),
            CreateFont("Inter Sans", "Regular", 400, false, "/fonts/inter-regular.ttf", coverage),
            CreateFont("JetBrains Mono", "Regular", 400, false, "/fonts/jetbrains-mono.ttf", coverage),
            CreateFont("Inter Sans", "Regular", 500, true, "/fonts/inter-regular-italic.ttf", coverage),
            CreateFont("Playfair Display", "Regular", 400, false, "/fonts/playfair-display.ttf", coverage)
        };

        var index = FontIndex.Create(fonts);

        var query = new FontIndexQuery(
            FamilyNameContains: "inter",
            Classifications: new[] { FontClassification.SansSerif },
            MinimumWeight: 500,
            MaximumWeight: 900,
            IsItalic: false,
            IsMonospace: false,
            SupportsText: "Hello");

        var filtered = index.Query(query);

        filtered.Should().HaveCount(1);
        filtered.Single().SourcePath.Should().Be("/fonts/inter-bold.ttf");

        var ordered = index.Query();
        ordered.Select(font => font.SourcePath).Should().ContainInOrder(
            "/fonts/inter-bold.ttf",
            "/fonts/inter-regular.ttf",
            "/fonts/inter-regular-italic.ttf",
            "/fonts/jetbrains-mono.ttf",
            "/fonts/playfair-display.ttf");
    }

    [Fact]
    public void Query_SupportsText_UsesCmapCoverageRanges()
    {
        var asciiOnly = CreateCoverage(new CodePointRange(0x20, 0x7E));
        var asciiAndGreek = CreateCoverage(
            new CodePointRange(0x20, 0x7E),
            new CodePointRange(0x390, 0x3FF));

        var index = FontIndex.Create(
        [
            CreateFont("Inter Sans", "Regular", 400, false, "/fonts/inter.ttf", asciiOnly),
            CreateFont("Noto Sans", "Regular", 400, false, "/fonts/noto.ttf", asciiAndGreek)
        ]);

        var result = index.Query(new FontIndexQuery(SupportsText: "AΩ"));

        result.Should().HaveCount(1);
        result.Single().Family.Should().Be("Noto Sans");
    }

    [Fact]
    public void CachedBuilder_PersistsAndReloadsCache_AndRefreshesOnlyStaleEntries()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var fileA = Path.Combine(tempDirectory.FullName, "A.ttf");
            var fileB = Path.Combine(tempDirectory.FullName, "B.ttf");
            var cachePath = Path.Combine(tempDirectory.FullName, "index-cache.json");

            File.WriteAllText(fileA, "v1");
            File.WriteAllText(fileB, "v1");

            var coverage = CreateCoverage(new CodePointRange(0x20, 0x7E));
            var reader = new StubFontFileReader();
            reader.SetResponse(fileA, [CreateFont("Family A Sans", "Regular", 400, false, fileA, coverage)]);
            reader.SetResponse(fileB, [CreateFont("Family B Serif", "Regular", 400, false, fileB, coverage)]);

            var builder = new CachedFontIndexBuilder(reader);

            var firstBuild = builder.Build([fileA, fileB], cachePath);
            firstBuild.RefreshedFileCount.Should().Be(2);
            firstBuild.LoadedFromCacheFileCount.Should().Be(0);
            firstBuild.FailedFileCount.Should().Be(0);
            reader.ReadCallCount.Should().Be(2);
            File.Exists(cachePath).Should().BeTrue();

            var secondBuild = builder.Build([fileA, fileB], cachePath);
            secondBuild.RefreshedFileCount.Should().Be(0);
            secondBuild.LoadedFromCacheFileCount.Should().Be(2);
            secondBuild.FailedFileCount.Should().Be(0);
            reader.ReadCallCount.Should().Be(2);

            File.AppendAllText(fileB, "-updated");
            reader.SetResponse(fileB, [CreateFont("Family B Serif", "Bold", 700, false, fileB, coverage)]);

            var thirdBuild = builder.Build([fileA, fileB], cachePath);
            thirdBuild.RefreshedFileCount.Should().Be(1);
            thirdBuild.LoadedFromCacheFileCount.Should().Be(1);
            thirdBuild.FailedFileCount.Should().Be(0);
            reader.ReadCallCount.Should().Be(3);

            var refreshed = thirdBuild.Index.Query(new FontIndexQuery(FamilyNameContains: "Family B"));
            refreshed.Should().ContainSingle(font => font.Subfamily == "Bold" && font.Weight == 700);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Query_ThousandFontIndex_CompletesWithinHalfSecondBudget()
    {
        var coverage = CreateCoverage(new CodePointRange(0x20, 0x7E));

        var fonts = Enumerable.Range(0, 1000)
            .Select(index => CreateFont(
                family: $"Family {index % 50} Sans",
                subfamily: index % 2 == 0 ? "Regular" : "Bold",
                weight: index % 2 == 0 ? 400 : 700,
                isItalic: index % 3 == 0,
                sourcePath: $"/fonts/font-{index:D4}.ttf",
                coverage: coverage))
            .ToArray();

        var index = FontIndex.Create(fonts);

        _ = index.Query(new FontIndexQuery(
            FamilyNameContains: "Family 1",
            Classifications: new[] { FontClassification.SansSerif },
            MinimumWeight: 400,
            MaximumWeight: 700,
            IsItalic: false,
            SupportsText: "The quick brown fox"));

        var stopwatch = Stopwatch.StartNew();

        for (var iteration = 0; iteration < 200; iteration++)
        {
            _ = index.Query(new FontIndexQuery(
                FamilyNameContains: "Family 1",
                Classifications: new[] { FontClassification.SansSerif },
                MinimumWeight: 400,
                MaximumWeight: 700,
                IsItalic: false,
                SupportsText: "The quick brown fox"));
        }

        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(500));
    }

    private static FontInfo CreateFont(
        string family,
        string subfamily,
        int weight,
        bool isItalic,
        string sourcePath,
        GlyphCoverageSummary coverage)
        => new(
            SourcePath: sourcePath,
            FaceIndex: 0,
            Family: family,
            Subfamily: subfamily,
            Weight: weight,
            Width: FontWidthClass.Normal,
            IsItalic: isItalic,
            Format: FontContainerFormat.TrueType,
            Coverage: coverage);

    private static GlyphCoverageSummary CreateCoverage(params CodePointRange[] ranges)
    {
        var normalizedRanges = ranges
            .OrderBy(range => range.Start)
            .ToArray();

        var mappedCodePointCount = normalizedRanges
            .Sum(range => checked((int)(range.End - range.Start + 1)));

        return new GlyphCoverageSummary(
            GlyphCount: mappedCodePointCount,
            MappedCodePointCount: mappedCodePointCount,
            SupportsBasicLatin: HasIntersectingRange(normalizedRanges, 0x0020, 0x007E),
            SupportsLatin1Supplement: HasIntersectingRange(normalizedRanges, 0x00A0, 0x00FF),
            SupportsLatinExtendedA: HasIntersectingRange(normalizedRanges, 0x0100, 0x017F),
            SupportsGreekAndCoptic: HasIntersectingRange(normalizedRanges, 0x0370, 0x03FF),
            SupportsCyrillic: HasIntersectingRange(normalizedRanges, 0x0400, 0x04FF),
            SupportsCjkUnifiedIdeographs: HasIntersectingRange(normalizedRanges, 0x4E00, 0x9FFF),
            CoveredCodePointRanges: normalizedRanges);
    }

    private static bool HasIntersectingRange(IEnumerable<CodePointRange> ranges, uint startInclusive, uint endInclusive)
        => ranges.Any(range => range.End >= startInclusive && range.Start <= endInclusive);

    private sealed class StubFontFileReader : IFontFileReader
    {
        private readonly Dictionary<string, IReadOnlyList<FontInfo>> _responses =
            new(StringComparer.OrdinalIgnoreCase);

        public int ReadCallCount { get; private set; }

        public void SetResponse(string path, IReadOnlyList<FontInfo> fonts)
        {
            _responses[path] = fonts;
        }

        public IReadOnlyList<FontInfo> Read(string path)
        {
            ReadCallCount++;

            if (_responses.TryGetValue(path, out var fonts))
            {
                return fonts;
            }

            throw new FontReadException(
                FontReadErrorCode.FileNotFound,
                path,
                $"No stub response mapped for '{path}'.");
        }
    }
}
