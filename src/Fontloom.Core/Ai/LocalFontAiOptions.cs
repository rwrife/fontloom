namespace Fontloom.Core.Ai;

public sealed class LocalFontAiOptions
{
    public const string DefaultEndpoint = "http://localhost:11434";

    public string Model { get; init; } = "llama3.2:3b-instruct";

    public int MaxSuggestions { get; init; } = 3;

    public TimeSpan ProbeTimeout { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(8);
}
