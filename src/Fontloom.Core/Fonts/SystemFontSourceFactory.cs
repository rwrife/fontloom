namespace Fontloom.Core.Fonts;

public static class SystemFontSourceFactory
{
    public static ISystemFontSource CreateDefault(IFontFileReader fontFileReader)
    {
        ArgumentNullException.ThrowIfNull(fontFileReader);

        if (OperatingSystem.IsWindows())
        {
            return new WindowsSystemFontSource(fontFileReader);
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacOsSystemFontSource(fontFileReader);
        }

        return NullSystemFontSource.Instance;
    }
}
