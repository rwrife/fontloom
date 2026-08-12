using Fontloom.Core.Fonts;

namespace Fontloom.Desktop.ViewModels;

public sealed class FontTileViewModel
{
    private FontTileViewModel(FontInfo font, bool isFavorite, IReadOnlyCollection<string> tags)
    {
        Font = font;
        IsFavorite = isFavorite;
        Tags = tags;
    }

    public FontInfo Font { get; }

    public bool IsFavorite { get; }

    public IReadOnlyCollection<string> Tags { get; }

    public string Family => Font.Family;

    public string Subfamily => Font.Subfamily;

    public int Weight => Font.Weight;

    public bool IsItalic => Font.IsItalic;

    public string FavoriteMarker => IsFavorite ? "★" : "☆";

    public string StyleSummary =>
        $"{Subfamily} · {Weight}{(IsItalic ? " · Italic" : string.Empty)}";

    public string TagSummary =>
        Tags.Count == 0
            ? "No tags"
            : string.Join(", ", Tags.OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase));

    public string SourcePath => Font.SourcePath;

    public string Format => Font.Format.ToString();

    public string Width => Font.Width.ToString();

    public int GlyphCount => Font.Coverage.GlyphCount;

    public int MappedCodePointCount => Font.Coverage.MappedCodePointCount;

    public static FontTileViewModel FromFontInfo(
        FontInfo font,
        bool isFavorite = false,
        IReadOnlyCollection<string>? tags = null)
        => new(font, isFavorite, tags ?? Array.Empty<string>());
}
