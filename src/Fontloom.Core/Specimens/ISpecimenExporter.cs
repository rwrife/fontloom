using Fontloom.Core.Fonts;

namespace Fontloom.Core.Specimens;

public interface ISpecimenExporter
{
    void ExportFontPng(FontInfo font, string outputPath, SpecimenExportOptions options);

    void ExportCollectionPdf(IReadOnlyList<FontInfo> fonts, string outputPath, SpecimenExportOptions options);
}
