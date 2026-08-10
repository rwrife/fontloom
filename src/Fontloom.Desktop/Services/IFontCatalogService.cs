using Fontloom.Core.Fonts;

namespace Fontloom.Desktop.Services;

public interface IFontCatalogService
{
    FontIndex BuildIndex(CancellationToken cancellationToken = default);
}
