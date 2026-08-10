using Fontloom.Core.Fonts;

namespace Fontloom.Desktop.ViewModels;

public sealed class FontTileViewModel
{
    private FontTileViewModel(FontInfo font)
    {
        Font = font;
    }

    public FontInfo Font { get; }

    public string Family => Font.Family;

    public string Subfamily => Font.Subfamily;

    public int Weight => Font.Weight;

    public bool IsItalic => Font.IsItalic;

    public string StyleSummary =>
        $"{Subfamily} · {Weight}{(IsItalic ? " · Italic" : string.Empty)}";

    public string SourcePath => Font.SourcePath;

    public string Format => Font.Format.ToString();

    public string Width => Font.Width.ToString();

    public int GlyphCount => Font.Coverage.GlyphCount;

    public int MappedCodePointCount => Font.Coverage.MappedCodePointCount;

    public static FontTileViewModel FromFontInfo(FontInfo font)
        => new(font);
}
