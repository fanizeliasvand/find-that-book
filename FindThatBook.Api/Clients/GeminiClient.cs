using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace FindThatBook.Api.Clients;

public class GeminiClient : IGeminiClient
{
    private const string Model = "gemini-3.6-flash";
    private const string Endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent";

    private const int MaxAttempts = 3;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiClient> _logger;
    private readonly string _apiKey;

    public GeminiClient(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

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

        for (var attempt = 1; ; attempt++)
        {
            var lastAttempt = attempt == MaxAttempts;

            // Linked so the per-request timeout can never outlive the caller's token.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(RequestTimeout);

            HttpResponseMessage response;

            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, Endpoint)
                {
                    Content = JsonContent.Create(request)
                };

                // Header auth keeps the key out of the URL
                message.Headers.Add("x-goog-api-key", _apiKey);

                response = await _httpClient.SendAsync(message, timeout.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
            {
                if (lastAttempt)
                {
                    throw;
                }

                _logger.LogWarning(
                    exception,
                    "Gemini attempt {Attempt} of {MaxAttempts} failed; retrying",
                    attempt,
                    MaxAttempts);

                await Task.Delay(BackoffFor(attempt), ct);
                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadFromJsonAsync<GeminiResponse>(timeout.Token);

                    // A safety-blocked or empty reply has no candidates. Returning empty text lets callers fall back instead of handling an exception.
                    // Take the first part that actually carries text: thinking models can
                    // put a reasoning part ahead of the answer.
                    return body?.Candidates?.FirstOrDefault()?.Content?.Parts?
                        .FirstOrDefault(part => !string.IsNullOrWhiteSpace(part.Text))?.Text ?? string.Empty;
                }

                // A 4xx fails identically on a second attempt, so only 5xx is worth repeating.
                if ((int)response.StatusCode < 500 || lastAttempt)
                {
                    // EnsureSuccessStatusCode throws the body away, and the body is
                    // where Google explains what is actually wrong.
                    var errorBody = await response.Content.ReadAsStringAsync(ct);

                    _logger.LogError(
                        "Gemini returned {StatusCode}: {ErrorBody}",
                        (int)response.StatusCode,
                        errorBody);

                    response.EnsureSuccessStatusCode();
                }

                _logger.LogWarning(
                    "Gemini attempt {Attempt} of {MaxAttempts} returned {StatusCode}; retrying",
                    attempt,
                    MaxAttempts,
                    (int)response.StatusCode);
            }

            await Task.Delay(BackoffFor(attempt), ct);
        }
    }

    // 200ms then 400ms, plus jitter so concurrent callers do not retry in lockstep.
    private static TimeSpan BackoffFor(int attempt) =>
        TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt) + Random.Shared.Next(0, 100));

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
