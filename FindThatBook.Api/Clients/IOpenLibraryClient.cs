using FindThatBook.Api.Models;

namespace FindThatBook.Api.Clients;

public interface IOpenLibraryClient
{
    Task<List<OpenLibraryWork>> SearchAsync(string? title, string? author, CancellationToken ct);

    // A messy unstructured string still returns hits here, unlike the fielded SearchAsync.
    Task<List<OpenLibraryWork>> SearchRawAsync(string query, CancellationToken ct);
}
