using Fontloom.Core.Fonts;

namespace Fontloom.Desktop.Services;

public sealed class SystemFontCatalogService : IFontCatalogService
{
    public FontIndex BuildIndex(CancellationToken cancellationToken = default)
    {
        var fontFileReader = new OpenTypeFontFileReader();
        var systemFontSource = SystemFontSourceFactory.CreateDefault(fontFileReader);
        var inventoryBuilder = new FontInventoryBuilder(systemFontSource, fontFileReader);

        var fonts = inventoryBuilder.EnumerateFonts(cancellationToken: cancellationToken);
        return FontIndex.Create(fonts);
    }
}
