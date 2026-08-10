namespace Fontloom.Core.Fonts;

public sealed record FontIndexQuery(
    string? FamilyNameContains = null,
    IReadOnlyCollection<FontClassification>? Classifications = null,
    int? MinimumWeight = null,
    int? MaximumWeight = null,
    bool? IsItalic = null,
    bool? IsMonospace = null,
    string? SupportsText = null);
