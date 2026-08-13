using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Fontloom.Core.Fonts;

namespace Fontloom.Core.Ai;

public sealed class LocalFontAiService : IFontAiService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly LocalFontAiOptions _options;

    public LocalFontAiService(HttpClient? httpClient = null, LocalFontAiOptions? options = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _options = options ?? new LocalFontAiOptions();
    }

    public async Task<bool> ProbeAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        if (!TryResolveEndpoint(endpoint, out var endpointUri))
        {
            return false;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.ProbeTimeout);

        if (await ProbePathAsync(endpointUri, "/v1/models", timeoutCts.Token).ConfigureAwait(false))
        {
            return true;
        }

        return await ProbePathAsync(endpointUri, "/api/tags", timeoutCts.Token).ConfigureAwait(false);
    }

    public async Task<FontAiSuggestionResult> SuggestPairingsAsync(
        FontInfo baseFont,
        IReadOnlyList<FontInfo> libraryFonts,
        bool enableLocalAi,
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseFont);
        ArgumentNullException.ThrowIfNull(libraryFonts);

        if (!enableLocalAi)
        {
            return new FontAiSuggestionResult(
                LocalAiEnabled: false,
                EndpointReachable: false,
                UsedFallback: false,
                Pairings: Array.Empty<FontPairingSuggestion>(),
                Description: "Local AI is disabled.");
        }

        if (!TryResolveEndpoint(endpoint, out var endpointUri))
        {
            return HeuristicFontPairingEngine.BuildFallback(
                baseFont,
                libraryFonts,
                localAiEnabled: true,
                endpointReachable: false,
                maxSuggestions: _options.MaxSuggestions);
        }

        var probeSucceeded = await ProbeAsync(endpoint, cancellationToken).ConfigureAwait(false);
        if (!probeSucceeded)
        {
            return HeuristicFontPairingEngine.BuildFallback(
                baseFont,
                libraryFonts,
                localAiEnabled: true,
                endpointReachable: false,
                maxSuggestions: _options.MaxSuggestions);
        }

        try
        {
            var aiResult = await RequestModelPairingsAsync(baseFont, libraryFonts, endpointUri, cancellationToken)
                .ConfigureAwait(false);

            if (aiResult.Pairings.Count == 0)
            {
                return HeuristicFontPairingEngine.BuildFallback(
                    baseFont,
                    libraryFonts,
                    localAiEnabled: true,
                    endpointReachable: true,
                    maxSuggestions: _options.MaxSuggestions);
            }

            return aiResult;
        }
        catch
        {
            return HeuristicFontPairingEngine.BuildFallback(
                baseFont,
                libraryFonts,
                localAiEnabled: true,
                endpointReachable: true,
                maxSuggestions: _options.MaxSuggestions);
        }
    }

    private async Task<FontAiSuggestionResult> RequestModelPairingsAsync(
        FontInfo baseFont,
        IReadOnlyList<FontInfo> libraryFonts,
        Uri endpointUri,
        CancellationToken cancellationToken)
    {
        var candidates = libraryFonts
            .Where(font => !IsSameFace(baseFont, font))
            .DistinctBy(font => (Normalize(font.SourcePath), font.FaceIndex))
            .Take(48)
            .Select((font, index) => new PromptCandidate(
                Id: index + 1,
                Family: font.Family,
                Subfamily: font.Subfamily,
                Classification: FontClassificationRules.Classify(font).ToString(),
                Weight: font.Weight,
                Width: font.Width.ToString(),
                IsItalic: font.IsItalic,
                Font: font))
            .ToArray();

        if (candidates.Length == 0)
        {
            return new FontAiSuggestionResult(
                LocalAiEnabled: true,
                EndpointReachable: true,
                UsedFallback: false,
                Pairings: Array.Empty<FontPairingSuggestion>(),
                Description: HeuristicFontPairingEngine.BuildAutoDescription(baseFont));
        }

        var promptBody = BuildPrompt(baseFont, candidates);

        var payload = new
        {
            model = _options.Model,
            temperature = 0.2,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You are a typography assistant. Return strict JSON only. Pick 1-3 best pairing candidates from the provided candidate IDs."
                },
                new
                {
                    role = "user",
                    content = promptBody
                }
            }
        };

        var requestJson = JsonSerializer.Serialize(payload, JsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(endpointUri, "/v1/chat/completions"))
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.RequestTimeout);

        using var response = await _httpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
        var completion = JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson, JsonOptions);
        var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;

        if (!TryExtractJsonPayload(content, out var modelPayloadJson))
        {
            return new FontAiSuggestionResult(
                LocalAiEnabled: true,
                EndpointReachable: true,
                UsedFallback: false,
                Pairings: Array.Empty<FontPairingSuggestion>(),
                Description: HeuristicFontPairingEngine.BuildAutoDescription(baseFont));
        }

        var modelPayload = JsonSerializer.Deserialize<ModelSuggestionPayload>(modelPayloadJson, JsonOptions);
        var requestedIds = modelPayload?.Pairings ?? Array.Empty<int>();

        var candidateMap = candidates.ToDictionary(candidate => candidate.Id, candidate => candidate);
        var pairings = new List<FontPairingSuggestion>();

        foreach (var id in requestedIds)
        {
            if (!candidateMap.TryGetValue(id, out var candidate))
            {
                continue;
            }

            var rationale = modelPayload?.Rationales?.TryGetValue(id.ToString(), out var value) == true &&
                            !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : $"Suggested by local model as a complement to {baseFont.Family}.";

            pairings.Add(new FontPairingSuggestion(candidate.Font, rationale));

            if (pairings.Count >= Math.Clamp(_options.MaxSuggestions, 1, 3))
            {
                break;
            }
        }

        var distinctPairings = pairings
            .DistinctBy(pairing => (Normalize(pairing.Font.SourcePath), pairing.Font.FaceIndex))
            .Take(Math.Clamp(_options.MaxSuggestions, 1, 3))
            .ToArray();

        var description = string.IsNullOrWhiteSpace(modelPayload?.Description)
            ? HeuristicFontPairingEngine.BuildAutoDescription(baseFont)
            : modelPayload.Description.Trim();

        return new FontAiSuggestionResult(
            LocalAiEnabled: true,
            EndpointReachable: true,
            UsedFallback: false,
            Pairings: distinctPairings,
            Description: description);
    }

    private static string BuildPrompt(FontInfo baseFont, IReadOnlyList<PromptCandidate> candidates)
    {
        var baseMetadata = new
        {
            family = baseFont.Family,
            subfamily = baseFont.Subfamily,
            classification = FontClassificationRules.Classify(baseFont).ToString(),
            weight = baseFont.Weight,
            width = baseFont.Width.ToString(),
            italic = baseFont.IsItalic
        };

        var candidateMetadata = candidates.Select(candidate => new
        {
            id = candidate.Id,
            family = candidate.Family,
            subfamily = candidate.Subfamily,
            classification = candidate.Classification,
            weight = candidate.Weight,
            width = candidate.Width,
            italic = candidate.IsItalic
        });

        var metadataJson = JsonSerializer.Serialize(new
        {
            target_font = baseMetadata,
            candidate_fonts = candidateMetadata
        }, JsonOptions);

        return "Use only the metadata below. Never ask for font files. Return JSON with this exact shape:\n" +
               "{\n" +
               "  \"description\": \"short one-sentence description of the target font\",\n" +
               "  \"pairings\": [<candidate-id-1>, <candidate-id-2>, <candidate-id-3>],\n" +
               "  \"rationales\": {\n" +
               "    \"<candidate-id>\": \"short rationale\"\n" +
               "  }\n" +
               "}\n\n" +
               "Rules:\n" +
               "- Pick 1 to 3 candidates from candidate_fonts.\n" +
               "- Prefer contrast and readability for heading/body pairings.\n" +
               "- Include only IDs that exist in candidate_fonts.\n\n" +
               "Metadata:\n" +
               metadataJson;
    }

    private async Task<bool> ProbePathAsync(Uri endpointUri, string path, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(endpointUri, path));
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryResolveEndpoint(string endpoint, out Uri endpointUri)
    {
        endpointUri = default!;

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return false;
        }

        if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var parsedUri) || parsedUri is null)
        {
            return false;
        }

        endpointUri = parsedUri;
        return endpointUri.Scheme is "http" or "https";
    }

    private static bool TryExtractJsonPayload(string? content, out string jsonPayload)
    {
        jsonPayload = string.Empty;

        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var trimmed = content.Trim();

        if (trimmed.StartsWith("```") && trimmed.Contains('{') && trimmed.Contains('}'))
        {
            var firstBrace = trimmed.IndexOf('{');
            var lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                jsonPayload = trimmed[firstBrace..(lastBrace + 1)];
                return true;
            }
        }

        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            jsonPayload = trimmed;
            return true;
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            jsonPayload = trimmed[start..(end + 1)];
            return true;
        }

        return false;
    }

    private static bool IsSameFace(FontInfo a, FontInfo b)
        => StringComparer.OrdinalIgnoreCase.Equals(Normalize(a.SourcePath), Normalize(b.SourcePath)) &&
           a.FaceIndex == b.FaceIndex;

    private static string Normalize(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path.Trim();
        }
    }

    private sealed record PromptCandidate(
        int Id,
        string Family,
        string Subfamily,
        string Classification,
        int Weight,
        string Width,
        bool IsItalic,
        FontInfo Font);

    private sealed class ChatCompletionResponse
    {
        public IReadOnlyList<ChatChoice>? Choices { get; set; }
    }

    private sealed class ChatChoice
    {
        public ChatMessage? Message { get; set; }
    }

    private sealed class ChatMessage
    {
        public string? Content { get; set; }
    }

    private sealed class ModelSuggestionPayload
    {
        public string? Description { get; set; }

        public IReadOnlyList<int>? Pairings { get; set; }

        public IReadOnlyDictionary<string, string>? Rationales { get; set; }
    }
}
