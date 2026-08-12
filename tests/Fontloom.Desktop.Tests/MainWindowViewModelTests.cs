using FluentAssertions;
using Fontloom.Core.Fonts;
using Fontloom.Core.Organization;
using Fontloom.Core.Specimens;
using Fontloom.Desktop.Services;
using Fontloom.Desktop.ViewModels;

namespace Fontloom.Desktop.Tests;

public class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_AutoLoad_PopulatesGalleryAndSelection()
    {
        var service = new StubCatalogService(CreateIndex(
            CreateFont("Inter Sans", "Regular", 400, false, "/fonts/inter-regular.ttf", CreateCoverage(new CodePointRange(0x20, 0x7E))),
            CreateFont("JetBrains Mono", "Regular", 400, false, "/fonts/jetbrains-mono.ttf", CreateCoverage(new CodePointRange(0x20, 0x7E))),
            CreateFont("Playfair Serif", "Regular", 400, false, "/fonts/playfair.ttf", CreateCoverage(new CodePointRange(0x20, 0x7E)))));

        var viewModel = new MainWindowViewModel(service);

        service.BuildCallCount.Should().Be(1);
        viewModel.FilteredFonts.Should().HaveCount(3);
        viewModel.SelectedFont.Should().NotBeNull();
        viewModel.StatusMessage.Should().Contain("Loaded 3 fonts");
    }

    [Fact]
    public void ClassificationFilters_CanNarrowToSingleFacet()
    {
        var service = new StubCatalogService(CreateIndex(
            CreateFont("Inter Sans", "Regular", 400, false, "/fonts/inter-regular.ttf", CreateCoverage(new CodePointRange(0x20, 0x7E))),
            CreateFont("JetBrains Mono", "Regular", 400, false, "/fonts/jetbrains-mono.ttf", CreateCoverage(new CodePointRange(0x20, 0x7E))),
            CreateFont("Playfair Serif", "Regular", 400, false, "/fonts/playfair.ttf", CreateCoverage(new CodePointRange(0x20, 0x7E)))));

        var viewModel = new MainWindowViewModel(service);

        viewModel.ShowSerif = false;
        viewModel.ShowSansSerif = false;
        viewModel.ShowDisplay = false;
        viewModel.ShowUnknown = false;

        viewModel.FilteredFonts.Should().ContainSingle();
        viewModel.FilteredFonts.Single().Family.Should().Be("JetBrains Mono");
    }

    [Fact]
    public void RequireGlyphCoverage_UsesSampleTextWhenFiltering()
    {
        var service = new StubCatalogService(CreateIndex(
            CreateFont("Ascii Sans", "Regular", 400, false, "/fonts/ascii.ttf", CreateCoverage(new CodePointRange(0x20, 0x7E))),
            CreateFont("Greek Sans", "Regular", 400, false, "/fonts/greek.ttf", CreateCoverage(new CodePointRange(0x20, 0x7E), new CodePointRange(0x390, 0x3FF)))));

        var viewModel = new MainWindowViewModel(service);

        viewModel.RequireGlyphCoverage = true;
        viewModel.SampleText = "AΩ";

        viewModel.FilteredFonts.Should().ContainSingle();
        viewModel.FilteredFonts.Single().Family.Should().Be("Greek Sans");
    }

    [Fact]
    public void PreviewControls_AreClampedToSupportedRanges()
    {
        var service = new StubCatalogService(CreateIndex());
        var viewModel = new MainWindowViewModel(service);

        viewModel.PreviewSize = 300;
        viewModel.PreviewWeight = 42;

        viewModel.PreviewSize.Should().Be(96);
        viewModel.PreviewWeight.Should().Be(100);
    }

    [Fact]
    public void FavoriteToggle_UpdatesStoreAndFavoriteFilter()
    {
        var service = new StubCatalogService(CreateIndex(
            CreateFont("Inter Sans", "Regular", 400, false, "/fonts/inter-regular.ttf", CreateCoverage(new CodePointRange(0x20, 0x7E))),
            CreateFont("JetBrains Mono", "Regular", 400, false, "/fonts/jetbrains-mono.ttf", CreateCoverage(new CodePointRange(0x20, 0x7E)))));

        var store = new InMemoryFontOrganizationStore();
        var viewModel = new MainWindowViewModel(service, store);

        viewModel.SelectedFont = viewModel.FilteredFonts.Single(font => font.Family == "Inter Sans");
        viewModel.ToggleSelectedFavorite();

        viewModel.ShowFavoritesOnly = true;

        viewModel.FilteredFonts.Should().ContainSingle();
        viewModel.FilteredFonts.Single().Family.Should().Be("Inter Sans");
    }

    [Fact]
    public void SaveTags_UpdatesTagFacetFilter()
    {
        var service = new StubCatalogService(CreateIndex(
            CreateFont("Inter Sans", "Regular", 400, false, "/fonts/inter-regular.ttf", CreateCoverage(new CodePointRange(0x20, 0x7E))),
            CreateFont("JetBrains Mono", "Regular", 400, false, "/fonts/jetbrains-mono.ttf", CreateCoverage(new CodePointRange(0x20, 0x7E)))));

        var store = new InMemoryFontOrganizationStore();
        var viewModel = new MainWindowViewModel(service, store);

        viewModel.SelectedFont = viewModel.FilteredFonts.Single(font => font.Family == "JetBrains Mono");
        viewModel.SelectedFontTagsEditor = "coding, mono";
        viewModel.SaveSelectedFontTags();

        viewModel.TagFacetOptions.Should().Contain("coding");
        viewModel.ActiveTagFacet = "coding";

        viewModel.FilteredFonts.Should().ContainSingle();
        viewModel.FilteredFonts.Single().Family.Should().Be("JetBrains Mono");
    }

    [Fact]
    public void ComparisonTray_PinsAndCapsAtSixFonts()
    {
        var service = new StubCatalogService(CreateIndex(
            CreateFont("Font One", "Regular", 400, false, "/fonts/one.ttf", CreateCoverage(new CodePointRange(0x20, 0x7E))),
            CreateFont("Font Two", "Regular", 400, false, "/fonts/two.ttf", CreateCoverage(new CodePointRange(0x20, 0x7E))),
            CreateFont("Font Three", "Regular", 400, false, "/fonts/three.ttf", CreateCoverage(new CodePointRange(0x20, 0x7E))),
            CreateFont("Font Four", "Regular", 400, false, "/fonts/four.ttf", CreateCoverage(new CodePointRange(0x20, 0x7E))),
            CreateFont("Font Five", "Regular", 400, false, "/fonts/five.ttf", CreateCoverage(new CodePointRange(0x20, 0x7E))),
            CreateFont("Font Six", "Regular", 400, false, "/fonts/six.ttf", CreateCoverage(new CodePointRange(0x20, 0x7E))),
            CreateFont("Font Seven", "Regular", 400, false, "/fonts/seven.ttf", CreateCoverage(new CodePointRange(0x20, 0x7E)))));

        var viewModel = new MainWindowViewModel(service);

        foreach (var familyName in new[] { "Font One", "Font Two", "Font Three", "Font Four", "Font Five", "Font Six" })
        {
            viewModel.SelectedFont = viewModel.FilteredFonts.Single(font => font.Family == familyName);
            viewModel.ToggleSelectedFontComparisonPin();
        }

        viewModel.ComparisonTrayFonts.Should().HaveCount(6);
        viewModel.SelectedFont.Should().NotBeNull();
        viewModel.IsSelectedFontPinnedForComparison.Should().BeTrue();

        viewModel.SelectedFont = viewModel.FilteredFonts.Single(font => font.Family == "Font Seven");
        viewModel.ToggleSelectedFontComparisonPin();

        viewModel.ComparisonTrayFonts.Should().HaveCount(6);
        viewModel.StatusMessage.Should().Contain("full");
    }

    [Fact]
    public void SpecimenExport_UsesConfiguredPathsAndCallsExporter()
    {
        var service = new StubCatalogService(CreateIndex(
            CreateFont("Inter Sans", "Regular", 400, false, "/fonts/inter-regular.ttf", CreateCoverage(new CodePointRange(0x20, 0x7E))),
            CreateFont("JetBrains Mono", "Regular", 400, false, "/fonts/jetbrains-mono.ttf", CreateCoverage(new CodePointRange(0x20, 0x7E)))));

        var exporter = new RecordingSpecimenExporter();
        var store = new InMemoryFontOrganizationStore();
        var viewModel = new MainWindowViewModel(service, store, exporter);

        var tempExportDirectory = Path.Combine(Path.GetTempPath(), "fontloom-tests", Guid.NewGuid().ToString("N"));
        viewModel.ExportDirectoryInput = tempExportDirectory;
        viewModel.ExportFileStemInput = "brand-specimen";

        viewModel.SelectedFont = viewModel.FilteredFonts.Single(font => font.Family == "Inter Sans");
        viewModel.ExportSelectedFontSpecimenPng();

        exporter.PngExports.Should().ContainSingle();
        exporter.PngExports[0].outputPath.Should().EndWith("brand-specimen.png");

        viewModel.NewCollectionName = "Brand kit";
        viewModel.CreateCollection();
        viewModel.SelectedCollectionName = "Brand kit";
        viewModel.ToggleSelectedFontCollectionMembership();
        viewModel.ExportSelectedCollectionSpecimenPdf();

        exporter.PdfExports.Should().ContainSingle();
        exporter.PdfExports[0].outputPath.Should().EndWith("brand-specimen.pdf");
        exporter.PdfExports[0].options.CollectionLabel.Should().Be("Brand kit");
    }

    private static FontIndex CreateIndex(params FontInfo[] fonts)
        => FontIndex.Create(fonts);

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

    private sealed class StubCatalogService : IFontCatalogService
    {
        private readonly FontIndex _fontIndex;

        public StubCatalogService(FontIndex fontIndex)
        {
            _fontIndex = fontIndex;
        }

        public int BuildCallCount { get; private set; }

        public FontIndex BuildIndex(CancellationToken cancellationToken = default)
        {
            BuildCallCount++;
            cancellationToken.ThrowIfCancellationRequested();
            return _fontIndex;
        }
    }

    private sealed class RecordingSpecimenExporter : ISpecimenExporter
    {
        public List<(FontInfo font, string outputPath, SpecimenExportOptions options)> PngExports { get; } = new();

        public List<(IReadOnlyList<FontInfo> fonts, string outputPath, SpecimenExportOptions options)> PdfExports { get; } = new();

        public void ExportFontPng(FontInfo font, string outputPath, SpecimenExportOptions options)
            => PngExports.Add((font, outputPath, options));

        public void ExportCollectionPdf(IReadOnlyList<FontInfo> fonts, string outputPath, SpecimenExportOptions options)
            => PdfExports.Add((fonts, outputPath, options));
    }
}
