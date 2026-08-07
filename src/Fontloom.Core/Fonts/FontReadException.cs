namespace Fontloom.Core.Fonts;

public sealed class FontReadException : Exception
{
    public FontReadException(FontReadErrorCode code, string path, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        Path = path;
    }

    public FontReadErrorCode Code { get; }

    public string Path { get; }
}
