namespace Fontloom.Core.Specimens;

public sealed record SpecimenExportOptions(
    string SampleText,
    float PointSize = 42,
    string? CollectionLabel = null)
{
    public static SpecimenExportOptions Default { get; } =
        new("The quick brown fox jumps over the lazy dog 0123456789", 42);
}
