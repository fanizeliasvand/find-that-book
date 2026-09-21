namespace FindThatBook.Api.Clients;

public interface IGeminiClient
{
    // Callers own parsing of the returned text.
    Task<string> GenerateAsync(string prompt, CancellationToken ct);
}
