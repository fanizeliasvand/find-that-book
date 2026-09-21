using FindThatBook.Api.Clients;

namespace FindThatBook.Tests.Fakes;

// Returns a canned response or fails with a supplied exception.
// Records the prompt so tests can assert on what was sent.
public class FakeGeminiClient : IGeminiClient
{
    private readonly string? _response;
    private readonly Exception? _exception;

    private FakeGeminiClient(string? response, Exception? exception)
    {
        _response = response;
        _exception = exception;
    }

    public string? LastPrompt { get; private set; }

    public int CallCount { get; private set; }

    public static FakeGeminiClient Returning(string response) => new(response, null);

    public static FakeGeminiClient Throwing(Exception exception) => new(null, exception);

    public Task<string> GenerateAsync(string prompt, CancellationToken ct)
    {
        LastPrompt = prompt;
        CallCount++;

        return _exception is not null
            ? Task.FromException<string>(_exception)
            : Task.FromResult(_response!);
    }
}
