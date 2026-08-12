namespace Fontloom.Core.Organization;

public static class FontloomStoragePaths
{
    public static string ResolveDataDirectory()
    {
        string appDataRoot;
        if (OperatingSystem.IsWindows())
        {
            appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else if (OperatingSystem.IsMacOS())
        {
            appDataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support");
        }
        else
        {
            appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        return Path.Combine(appDataRoot, "fontloom");
    }

    public static string ResolveOrganizationStorePath()
        => Path.Combine(ResolveDataDirectory(), "font-organization.json");

    public static string ResolveLooseFontIndexCachePath()
        => Path.Combine(ResolveDataDirectory(), "loose-font-index-cache.json");
}
