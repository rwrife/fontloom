namespace Fontloom.Core.Organization;

public sealed record FontOrganizationSnapshot(
    IReadOnlyCollection<string> FavoriteFontPaths,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> TagsByFontPath,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> Collections,
    IReadOnlyCollection<string> LooseFontFolders)
{
    public static FontOrganizationSnapshot Empty { get; } = new(
        FavoriteFontPaths: Array.Empty<string>(),
        TagsByFontPath: new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase),
        Collections: new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase),
        LooseFontFolders: Array.Empty<string>());
}
