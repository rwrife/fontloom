using Fontloom.Core.Fonts;

namespace Fontloom.Core.Ai;

public sealed record FontPairingSuggestion(FontInfo Font, string Rationale);

public sealed record FontAiSuggestionResult(
    bool LocalAiEnabled,
    bool EndpointReachable,
    bool UsedFallback,
    IReadOnlyList<FontPairingSuggestion> Pairings,
    string Description);
