using FindThatBook.Api.Models;

namespace FindThatBook.Api.Clients;

public interface IOpenLibraryClient
{
    // Searches Open Library by title and/or author. Either may be null,
    // but at least one is expected to be provided by the caller.
    Task<List<OpenLibraryWork>> SearchAsync(string? title, string? author, CancellationToken ct);
}
