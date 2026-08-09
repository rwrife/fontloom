namespace Fontloom.Core.Fonts;

public sealed class MacOsSystemFontSource : DirectorySystemFontSource
{
    public MacOsSystemFontSource(IFontFileReader fontFileReader)
        : base(fontFileReader, ResolveKnownFontDirectories())
    {
    }

    private static IEnumerable<string> ResolveKnownFontDirectories()
    {
        var userProfileDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfileDirectory))
        {
            yield return Path.Combine(userProfileDirectory, "Library", "Fonts");
        }

        yield return "/Library/Fonts";
        yield return "/System/Library/Fonts";
    }
}
