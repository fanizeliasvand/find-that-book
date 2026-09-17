namespace FindThatBook.Api.Clients;

public interface IGeminiClient
{
    // Return raw text output. Callers own parsing of it.
    Task<string> GenerateAsync(string prompt, CancellationToken ct);
}
