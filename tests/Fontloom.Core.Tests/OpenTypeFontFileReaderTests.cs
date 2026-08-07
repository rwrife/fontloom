using FluentAssertions;
using Fontloom.Core.Fonts;

namespace Fontloom.Core.Tests;

public class OpenTypeFontFileReaderTests
{
    private readonly OpenTypeFontFileReader _reader = new();

    [Fact]
    public void Read_Ttf_ReturnsPopulatedFontInfo()
    {
        var path = FixturePath("NotoSans-Variable.ttf");

        var fonts = _reader.Read(path);

        fonts.Should().HaveCount(1);
        var font = fonts.Single();

        font.Family.Should().NotBeNullOrWhiteSpace();
        font.Subfamily.Should().NotBeNullOrWhiteSpace();
        font.Weight.Should().BeGreaterThan(0);
        font.Width.Should().NotBe(FontWidthClass.Unknown);
        font.IsItalic.Should().BeFalse();
        font.Format.Should().Be(FontContainerFormat.TrueType);
        font.Coverage.GlyphCount.Should().BeGreaterThan(0);
        font.Coverage.MappedCodePointCount.Should().BeGreaterThan(0);
        font.Coverage.SupportsBasicLatin.Should().BeTrue();
    }

    [Fact]
    public void Read_Otf_ReturnsPopulatedFontInfo()
    {
        var path = FixturePath("SourceSerif4-Regular.otf");

        var fonts = _reader.Read(path);

        fonts.Should().HaveCount(1);
        var font = fonts.Single();

        font.Family.Should().NotBeNullOrWhiteSpace();
        font.Subfamily.Should().NotBeNullOrWhiteSpace();
        font.Weight.Should().BeGreaterThan(0);
        font.Format.Should().Be(FontContainerFormat.OpenType);
        font.Coverage.GlyphCount.Should().BeGreaterThan(0);
        font.Coverage.SupportsBasicLatin.Should().BeTrue();
    }

    [Fact]
    public void Read_Ttc_ReturnsOneEntryPerFace()
    {
        var path = FixturePath("TestTTC.ttc");

        var fonts = _reader.Read(path);

        fonts.Should().HaveCount(2);
        fonts.Should().OnlyContain(f => f.Format == FontContainerFormat.TrueTypeCollection);
        fonts.Select(f => f.FaceIndex).Should().BeEquivalentTo([0, 1]);
        fonts.Should().OnlyContain(f => f.Family == "Test TTF");
    }

    [Fact]
    public void Read_Woff_ReturnsPopulatedFontInfo()
    {
        var path = FixturePath("fa-regular-400.woff");

        var fonts = _reader.Read(path);

        fonts.Should().HaveCount(1);
        var font = fonts.Single();
        font.Format.Should().Be(FontContainerFormat.WebOpenFont);
        font.Family.Should().NotBeNullOrWhiteSpace();
        font.Coverage.GlyphCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Read_Woff2_ReturnsPopulatedFontInfo()
    {
        var path = FixturePath("Inter-Regular.woff2");

        var fonts = _reader.Read(path);

        fonts.Should().HaveCount(1);
        var font = fonts.Single();
        font.Format.Should().Be(FontContainerFormat.WebOpenFont2);
        font.Family.Should().Contain("Inter");
        font.Coverage.GlyphCount.Should().BeGreaterThan(0);
        font.Coverage.MappedCodePointCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Read_MissingFile_ThrowsTypedError()
    {
        var act = () => _reader.Read(FixturePath("does-not-exist.ttf"));

        var ex = act.Should().Throw<FontReadException>().Which;
        ex.Code.Should().Be(FontReadErrorCode.FileNotFound);
    }

    [Fact]
    public void Read_CorruptFile_ThrowsTypedError()
    {
        var act = () => _reader.Read(FixturePath("corrupt.ttf"));

        var ex = act.Should().Throw<FontReadException>().Which;
        ex.Code.Should().Be(FontReadErrorCode.CorruptOrUnsupportedFont);
    }

    [Fact]
    public void Read_UnsupportedExtension_ThrowsTypedError()
    {
        var path = FixturePath("not-a-font.txt");
        File.WriteAllText(path, "hello");

        var act = () => _reader.Read(path);

        var ex = act.Should().Throw<FontReadException>().Which;
        ex.Code.Should().Be(FontReadErrorCode.UnsupportedFormat);
    }

    private static string FixturePath(string fileName)
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
}
