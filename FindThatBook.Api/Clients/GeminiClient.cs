using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace FindThatBook.Api.Clients;

public class GeminiClient : IGeminiClient
{
    private const string Model = "gemini-2.5-flash";
    private const string Endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public GeminiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;

        // Fail at startup rather than on the first search.
        _apiKey = configuration["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Gemini:ApiKey is not configured.");
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken ct)
    {
        var request = new GeminiRequest
        {
            Contents = [new Content { Parts = [new Part { Text = prompt }] }]
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(request)
        };

        // Header auth keeps the key out of the URL
        message.Headers.Add("x-goog-api-key", _apiKey);

        var response = await _httpClient.SendAsync(message, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<GeminiResponse>(ct);

        // A safety-blocked or empty reply has no candidates. Returning empty text lets callers fall back instead of handling an exception.
        return body?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;
    }

    // Gemini uses the same contents/parts shape in both directions, so Content and Part are shared by the request and response DTOs
    private class GeminiRequest
    {
        [JsonPropertyName("contents")]
        public List<Content> Contents { get; set; } = new();
    }

    private class Content
    {
        [JsonPropertyName("parts")]
        public List<Part> Parts { get; set; } = new();
    }

    private class Part
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<Candidate>? Candidates { get; set; }
    }

    private class Candidate
    {
        [JsonPropertyName("content")]
        public Content? Content { get; set; }
    }
}
