namespace Fontloom.Core.Fonts;

public sealed class NullSystemFontSource : ISystemFontSource
{
    public static NullSystemFontSource Instance { get; } = new();

    private NullSystemFontSource()
    {
    }

    public IReadOnlyList<FontInfo> EnumerateFonts(
        IProgress<SystemFontEnumerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return [];
    }
}
