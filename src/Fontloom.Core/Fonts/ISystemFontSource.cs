namespace Fontloom.Core.Fonts;

public interface ISystemFontSource
{
    IReadOnlyList<FontInfo> EnumerateFonts(
        IProgress<SystemFontEnumerationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
