using System.Net;
using System.Text;
using FluentAssertions;
using Fontloom.Core.Ai;
using Fontloom.Core.Fonts;

namespace Fontloom.Core.Tests;

public class LocalFontAiServiceTests
{
    [Fact]
    public async Task SuggestPairingsAsync_UsesLocalEndpoint_WhenProbeAndResponseSucceed()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/v1/models")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
            }

            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/v1/chat/completions")
            {
                const string response =
                    """
                    {
                      "choices": [
                        {
                          "message": {
                            "content": "{\"description\":\"Elegant high-contrast serif suited for editorial headers.\",\"pairings\":[2,1],\"rationales\":{\"2\":\"Clean sans-serif for body text contrast.\",\"1\":\"Neutral sans-serif fallback option.\"}}"
                          }
                        }
                      ]
                    }
                    """;

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(response, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(handler);
        var service = new LocalFontAiService(httpClient);

        var baseFont = CreateFont("Playfair Serif", "Regular", 700, false, "/fonts/playfair.ttf");
        var library = new[]
        {
            baseFont,
            CreateFont("Inter Sans", "Regular", 400, false, "/fonts/inter.ttf"),
            CreateFont("Source Sans", "Regular", 400, false, "/fonts/source-sans.ttf"),
            CreateFont("JetBrains Mono", "Regular", 400, false, "/fonts/jetbrains-mono.ttf")
        };

        var result = await service.SuggestPairingsAsync(
            baseFont,
            library,
            enableLocalAi: true,
            endpoint: LocalFontAiOptions.DefaultEndpoint);

        result.EndpointReachable.Should().BeTrue();
        result.UsedFallback.Should().BeFalse();
        result.Pairings.Should().HaveCount(2);
        result.Pairings.Select(pair => pair.Font.Family)
            .Should()
            .ContainInOrder("Source Sans", "Inter Sans");
        result.Description.Should().ContainEquivalentOf("editorial");
    }

    [Fact]
    public async Task SuggestPairingsAsync_FallsBackToHeuristics_WhenProbeFails()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var httpClient = new HttpClient(handler);
        var service = new LocalFontAiService(httpClient);

        var baseFont = CreateFont("Playfair Serif", "Regular", 700, false, "/fonts/playfair.ttf");
        var library = new[]
        {
            baseFont,
            CreateFont("Inter Sans", "Regular", 400, false, "/fonts/inter.ttf"),
            CreateFont("JetBrains Mono", "Regular", 400, false, "/fonts/jetbrains-mono.ttf"),
            CreateFont("Merriweather Serif", "Regular", 400, false, "/fonts/merriweather.ttf")
        };

        var result = await service.SuggestPairingsAsync(
            baseFont,
            library,
            enableLocalAi: true,
            endpoint: LocalFontAiOptions.DefaultEndpoint);

        result.EndpointReachable.Should().BeFalse();
        result.UsedFallback.Should().BeTrue();
        result.Pairings.Should().NotBeEmpty();
        result.Pairings.Count.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public void HeuristicPairingEngine_ReturnsOneToThreeSuggestions_FromLibrary()
    {
        var baseFont = CreateFont("Playfair Serif", "Regular", 700, false, "/fonts/playfair.ttf");
        var library = new[]
        {
            baseFont,
            CreateFont("Inter Sans", "Regular", 400, false, "/fonts/inter.ttf"),
            CreateFont("Merriweather Serif", "Regular", 400, false, "/fonts/merriweather.ttf"),
            CreateFont("JetBrains Mono", "Regular", 400, false, "/fonts/jetbrains-mono.ttf")
        };

        var result = HeuristicFontPairingEngine.BuildFallback(
            baseFont,
            library,
            localAiEnabled: true,
            endpointReachable: false,
            maxSuggestions: 3);

        result.UsedFallback.Should().BeTrue();
        result.Pairings.Count.Should().BeGreaterThanOrEqualTo(1);
        result.Pairings.Count.Should().BeLessThanOrEqualTo(3);
        result.Pairings.Select(pair => pair.Font.SourcePath)
            .Should()
            .OnlyContain(path => !StringComparer.OrdinalIgnoreCase.Equals(path, baseFont.SourcePath));
    }

    private static FontInfo CreateFont(
        string family,
        string subfamily,
        int weight,
        bool isItalic,
        string sourcePath)
        => new(
            SourcePath: sourcePath,
            FaceIndex: 0,
            Family: family,
            Subfamily: subfamily,
            Weight: weight,
            Width: FontWidthClass.Normal,
            IsItalic: isItalic,
            Format: FontContainerFormat.TrueType,
            Coverage: CreateCoverage(new CodePointRange(0x20, 0x7E)));

    private static GlyphCoverageSummary CreateCoverage(params CodePointRange[] ranges)
    {
        var normalizedRanges = ranges
            .OrderBy(range => range.Start)
            .ToArray();

        var mappedCodePointCount = normalizedRanges
            .Sum(range => checked((int)(range.End - range.Start + 1)));

        return new GlyphCoverageSummary(
            GlyphCount: mappedCodePointCount,
            MappedCodePointCount: mappedCodePointCount,
            SupportsBasicLatin: HasIntersectingRange(normalizedRanges, 0x0020, 0x007E),
            SupportsLatin1Supplement: HasIntersectingRange(normalizedRanges, 0x00A0, 0x00FF),
            SupportsLatinExtendedA: HasIntersectingRange(normalizedRanges, 0x0100, 0x017F),
            SupportsGreekAndCoptic: HasIntersectingRange(normalizedRanges, 0x0370, 0x03FF),
            SupportsCyrillic: HasIntersectingRange(normalizedRanges, 0x0400, 0x04FF),
            SupportsCjkUnifiedIdeographs: HasIntersectingRange(normalizedRanges, 0x4E00, 0x9FFF),
            CoveredCodePointRanges: normalizedRanges);
    }

    private static bool HasIntersectingRange(IEnumerable<CodePointRange> ranges, uint startInclusive, uint endInclusive)
        => ranges.Any(range => range.End >= startInclusive && range.Start <= endInclusive);

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }
}
