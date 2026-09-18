using System.Net;
using System.Text;

namespace FindThatBook.Tests.Fakes;

// Returns a queued sequence of responses, repeating the last one once the
// queue runs dry, and records what was sent.
public class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Status, string Body)> _responses;
    private readonly (HttpStatusCode Status, string Body) _lastResponse;

    public StubHttpMessageHandler(params (HttpStatusCode Status, string Body)[] responses)
    {
        _responses = new Queue<(HttpStatusCode, string)>(responses);
        _lastResponse = responses[^1];
    }

    public int CallCount { get; private set; }

    public List<CapturedRequest> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CallCount++;

        // Captured eagerly: HttpClient disposes the request once it is sent.
        Requests.Add(new CapturedRequest(
            request.Headers.TryGetValues("x-goog-api-key", out var values) ? values.Single() : null,
            request.RequestUri?.ToString() ?? string.Empty));

        var next = _responses.Count > 0 ? _responses.Dequeue() : _lastResponse;

        // A fresh message per call, because the client disposes each response.
        return Task.FromResult(new HttpResponseMessage(next.Status)
        {
            Content = new StringContent(next.Body, Encoding.UTF8, "application/json")
        });
    }

    public record CapturedRequest(string? ApiKeyHeader, string Url);
}
