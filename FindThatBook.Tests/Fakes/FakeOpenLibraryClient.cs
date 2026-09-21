using FindThatBook.Api.Clients;
using FindThatBook.Api.Models;

namespace FindThatBook.Tests.Fakes;

// Answers each call from a queued sequence, repeating the last entry once it runs dry.
// Records every call so tests can assert on the search strategy, not just its outcome.
public class FakeOpenLibraryClient : IOpenLibraryClient
{
    private readonly Queue<List<OpenLibraryWork>> _responses;
    private readonly List<OpenLibraryWork> _lastResponse;

    // The common case: the same works from any call, however many are made.
    public FakeOpenLibraryClient(params OpenLibraryWork[] works) => (_responses, _lastResponse) =
        (new Queue<List<OpenLibraryWork>>(), [.. works]);

    private FakeOpenLibraryClient(List<List<OpenLibraryWork>> responses) => (_responses, _lastResponse) =
        (new Queue<List<OpenLibraryWork>>(responses), responses[^1]);

    // One entry per call, in order, for tests that drive the widening search.
    public static FakeOpenLibraryClient Sequence(params OpenLibraryWork[][] responses) =>
        new([.. responses.Select(response => response.ToList())]);

    public string? LastMethod { get; private set; }

    public string? LastTitle { get; private set; }

    public string? LastAuthor { get; private set; }

    public string? LastRawQuery { get; private set; }

    // Every fielded call in order, so a retry is visible and not just implied.
    public List<(string? Title, string? Author)> FieldedCalls { get; } = [];

    public Task<List<OpenLibraryWork>> SearchAsync(string? title, string? author, CancellationToken ct)
    {
        LastMethod = nameof(SearchAsync);
        LastTitle = title;
        LastAuthor = author;
        FieldedCalls.Add((title, author));

        return Task.FromResult(Next());
    }

    public Task<List<OpenLibraryWork>> SearchRawAsync(string query, CancellationToken ct)
    {
        LastMethod = nameof(SearchRawAsync);
        LastRawQuery = query;

        return Task.FromResult(Next());
    }

    private List<OpenLibraryWork> Next()
    {
        return _responses.Count > 0 ? _responses.Dequeue() : _lastResponse;
    }
}
