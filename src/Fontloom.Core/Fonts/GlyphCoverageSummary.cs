using System.Text;

namespace Fontloom.Core.Fonts;

public sealed record GlyphCoverageSummary(
    int GlyphCount,
    int MappedCodePointCount,
    bool SupportsBasicLatin,
    bool SupportsLatin1Supplement,
    bool SupportsLatinExtendedA,
    bool SupportsGreekAndCoptic,
    bool SupportsCyrillic,
    bool SupportsCjkUnifiedIdeographs,
    IReadOnlyList<CodePointRange>? CoveredCodePointRanges = null)
{
    public bool SupportsText(string? sampleText)
    {
        if (string.IsNullOrEmpty(sampleText))
        {
            return true;
        }

        var ranges = CoveredCodePointRanges;
        if (ranges is { Count: > 0 })
        {
            foreach (var rune in sampleText.EnumerateRunes())
            {
                if (Rune.IsControl(rune))
                {
                    continue;
                }

                if (!ContainsCodePoint((uint)rune.Value, ranges))
                {
                    return false;
                }
            }

            return true;
        }

        return SupportsTextByUnicodeBlocks(sampleText);
    }

    private static bool ContainsCodePoint(uint codePoint, IReadOnlyList<CodePointRange> ranges)
    {
        var low = 0;
        var high = ranges.Count - 1;

        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var range = ranges[mid];

            if (codePoint < range.Start)
            {
                high = mid - 1;
                continue;
            }

            if (codePoint > range.End)
            {
                low = mid + 1;
                continue;
            }

            return true;
        }

        return false;
    }

    private bool SupportsTextByUnicodeBlocks(string sampleText)
    {
        foreach (var rune in sampleText.EnumerateRunes())
        {
            if (Rune.IsControl(rune))
            {
                continue;
            }

            var codePoint = (uint)rune.Value;
            if (!IsLikelyCoveredByBlockFlags(codePoint))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsLikelyCoveredByBlockFlags(uint codePoint)
    {
        if (codePoint >= 0x0020 && codePoint <= 0x007E)
        {
            return SupportsBasicLatin;
        }

        if (codePoint >= 0x00A0 && codePoint <= 0x00FF)
        {
            return SupportsLatin1Supplement;
        }

        if (codePoint >= 0x0100 && codePoint <= 0x017F)
        {
            return SupportsLatinExtendedA;
        }

        if (codePoint >= 0x0370 && codePoint <= 0x03FF)
        {
            return SupportsGreekAndCoptic;
        }

        if (codePoint >= 0x0400 && codePoint <= 0x04FF)
        {
            return SupportsCyrillic;
        }

        if (codePoint >= 0x4E00 && codePoint <= 0x9FFF)
        {
            return SupportsCjkUnifiedIdeographs;
        }

        return false;
    }
}
