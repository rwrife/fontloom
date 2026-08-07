namespace Fontloom.Core.Fonts;

public enum FontReadErrorCode
{
    FileNotFound = 1,
    UnsupportedFormat = 2,
    CorruptOrUnsupportedFont = 3,
    IoError = 4
}
