namespace Fontloom.Core.Fonts;

public interface IFontFileReader
{
    IReadOnlyList<FontInfo> Read(string path);
}
