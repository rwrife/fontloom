namespace Fontloom.Core.Fonts;

public sealed record FontInfo(
    string SourcePath,
    int FaceIndex,
    string Family,
    string Subfamily,
    int Weight,
    FontWidthClass Width,
    bool IsItalic,
    FontContainerFormat Format,
    GlyphCoverageSummary Coverage
);
