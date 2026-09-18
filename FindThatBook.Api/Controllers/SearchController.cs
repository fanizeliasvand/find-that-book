using FindThatBook.Api.Models;
using FindThatBook.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FindThatBook.Api.Controllers;

[ApiController]
[Route("api/search")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpPost]
    public async Task<ActionResult<SearchResponse>> Search(SearchRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest("Query must not be empty.");
        }

        return await _searchService.SearchAsync(request.Query, ct);
    }
}
