namespace Fontloom.Core.Fonts;

public sealed record SystemFontEnumerationProgress(
    int ProcessedFileCount,
    int DiscoveredFaceCount,
    string? CurrentPath);
