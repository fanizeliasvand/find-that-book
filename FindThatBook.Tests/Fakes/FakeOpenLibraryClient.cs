using FindThatBook.Api.Clients;
using FindThatBook.Api.Models;

namespace FindThatBook.Tests.Fakes;

// Returns the same canned works from either method, and records which one
// was called so tests can assert on the search strategy.
public class FakeOpenLibraryClient : IOpenLibraryClient
{
    private readonly List<OpenLibraryWork> _works;

    public FakeOpenLibraryClient(params OpenLibraryWork[] works) => _works = [.. works];

    public string? LastMethod { get; private set; }

    public string? LastTitle { get; private set; }

    public string? LastAuthor { get; private set; }

    public string? LastRawQuery { get; private set; }

    public Task<List<OpenLibraryWork>> SearchAsync(string? title, string? author, CancellationToken ct)
    {
        LastMethod = nameof(SearchAsync);
        LastTitle = title;
        LastAuthor = author;

        return Task.FromResult(_works);
    }

    public Task<List<OpenLibraryWork>> SearchRawAsync(string query, CancellationToken ct)
    {
        LastMethod = nameof(SearchRawAsync);
        LastRawQuery = query;

        return Task.FromResult(_works);
    }
}
