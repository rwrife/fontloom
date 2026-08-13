using FluentAssertions;
using Fontloom.Core.Fonts;
using Fontloom.Core.Specimens;

namespace Fontloom.Core.Tests;

public class SkiaSpecimenExporterTests
{
    [Fact]
    public void ExportFontPng_CreatesNonEmptyPngFile()
    {
        var exporter = new SkiaSpecimenExporter();
        var font = CreateFont("Inter Sans", "Regular", "/tmp/inter-regular.ttf");

        var outputDir = Path.Combine(Path.GetTempPath(), "fontloom-export-tests", Guid.NewGuid().ToString("N"));
        var outputPath = Path.Combine(outputDir, "single-font.png");

        exporter.ExportFontPng(font, outputPath, SpecimenExportOptions.Default);

        File.Exists(outputPath).Should().BeTrue();
        var bytes = File.ReadAllBytes(outputPath);
        bytes.Length.Should().BeGreaterThan(16);
        bytes[0].Should().Be(0x89);
        bytes[1].Should().Be((byte)'P');
        bytes[2].Should().Be((byte)'N');
        bytes[3].Should().Be((byte)'G');
    }

    [Fact]
    public void ExportCollectionPdf_CreatesPdfFile()
    {
        var exporter = new SkiaSpecimenExporter();
        var fonts = new[]
        {
            CreateFont("Inter Sans", "Regular", "/tmp/inter-regular.ttf"),
            CreateFont("JetBrains Mono", "Regular", "/tmp/jetbrains-mono.ttf")
        };

        var outputDir = Path.Combine(Path.GetTempPath(), "fontloom-export-tests", Guid.NewGuid().ToString("N"));
        var outputPath = Path.Combine(outputDir, "collection.pdf");

        exporter.ExportCollectionPdf(
            fonts,
            outputPath,
            SpecimenExportOptions.Default with { CollectionLabel = "Brand system" });

        File.Exists(outputPath).Should().BeTrue();
        var bytes = File.ReadAllBytes(outputPath);
        bytes.Length.Should().BeGreaterThan(32);
        bytes[0].Should().Be((byte)'%');
        bytes[1].Should().Be((byte)'P');
        bytes[2].Should().Be((byte)'D');
        bytes[3].Should().Be((byte)'F');
    }

    private static FontInfo CreateFont(string family, string subfamily, string sourcePath)
        => new(
            SourcePath: sourcePath,
            FaceIndex: 0,
            Family: family,
            Subfamily: subfamily,
            Weight: 400,
            Width: FontWidthClass.Normal,
            IsItalic: false,
            Format: FontContainerFormat.TrueType,
            Coverage: new GlyphCoverageSummary(
                GlyphCount: 96,
                MappedCodePointCount: 96,
                SupportsBasicLatin: true,
                SupportsLatin1Supplement: false,
                SupportsLatinExtendedA: false,
                SupportsGreekAndCoptic: false,
                SupportsCyrillic: false,
                SupportsCjkUnifiedIdeographs: false,
                CoveredCodePointRanges: new[] { new CodePointRange(0x20, 0x7E) }));
}
