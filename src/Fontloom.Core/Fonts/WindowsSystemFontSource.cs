namespace Fontloom.Core.Fonts;

public sealed class WindowsSystemFontSource : DirectorySystemFontSource
{
    public WindowsSystemFontSource(IFontFileReader fontFileReader)
        : base(fontFileReader, ResolveKnownFontDirectories())
    {
    }

    private static IEnumerable<string> ResolveKnownFontDirectories()
    {
        var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(windowsDirectory))
        {
            yield return Path.Combine(windowsDirectory, "Fonts");
        }

        var localAppDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppDataDirectory))
        {
            yield return Path.Combine(localAppDataDirectory, "Microsoft", "Windows", "Fonts");
        }
    }
}
