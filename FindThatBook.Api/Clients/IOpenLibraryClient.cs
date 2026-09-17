using FindThatBook.Api.Models;

namespace FindThatBook.Api.Clients;

public interface IOpenLibraryClient
{
    // Searches Open Library by title and/or author. Either may be null.
    Task<List<OpenLibraryWork>> SearchAsync(string? title, string? author, CancellationToken ct);

    // Full-text search. Unlike SearchAsync, a messy unstructured string still
    // returns hits here, so this is the fallback path.
    Task<List<OpenLibraryWork>> SearchRawAsync(string query, CancellationToken ct);
}
