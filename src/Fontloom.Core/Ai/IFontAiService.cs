using Fontloom.Core.Fonts;

namespace Fontloom.Core.Ai;

public interface IFontAiService
{
    Task<bool> ProbeAsync(string endpoint, CancellationToken cancellationToken = default);

    Task<FontAiSuggestionResult> SuggestPairingsAsync(
        FontInfo baseFont,
        IReadOnlyList<FontInfo> libraryFonts,
        bool enableLocalAi,
        string endpoint,
        CancellationToken cancellationToken = default);
}
