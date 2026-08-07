namespace Fontloom.Core.Fonts;

public sealed record GlyphCoverageSummary(
    int GlyphCount,
    int MappedCodePointCount,
    bool SupportsBasicLatin,
    bool SupportsLatin1Supplement,
    bool SupportsLatinExtendedA,
    bool SupportsGreekAndCoptic,
    bool SupportsCyrillic,
    bool SupportsCjkUnifiedIdeographs
);
