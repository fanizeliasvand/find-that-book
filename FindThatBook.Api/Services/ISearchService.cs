using FindThatBook.Api.Models;

namespace FindThatBook.Api.Services;

public interface ISearchService
{
    Task<SearchResponse> SearchAsync(string query, CancellationToken ct);
}
