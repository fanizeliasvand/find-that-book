using FindThatBook.Api.Clients;
using FindThatBook.Api.Models;

namespace FindThatBook.Api.Services;

public class SearchService : ISearchService
{
    private const int MaxResults = 5;

    private readonly IQueryParser _queryParser;
    private readonly IOpenLibraryClient _openLibrary;
    private readonly IBookMatcher _matcher;
    private readonly ILogger<SearchService> _logger;

    public SearchService(
        IQueryParser queryParser,
        IOpenLibraryClient openLibrary,
        IBookMatcher matcher,
        ILogger<SearchService> logger)
    {
        _queryParser = queryParser;
        _openLibrary = openLibrary;
        _matcher = matcher;
        _logger = logger;
    }

    public async Task<SearchResponse> SearchAsync(string query, CancellationToken ct)
    {
        var interpretation = await _queryParser.ParseAsync(query, ct);

        // A fallback interpretation has no usable title or author, so only
        // full-text search can find anything for it.
        var works = interpretation.IsFallback
            ? await _openLibrary.SearchRawAsync(interpretation.RawQuery, ct)
            : await _openLibrary.SearchAsync(interpretation.Title, interpretation.Author, ct);

        var ranked = _matcher.Rank(works, interpretation);
        var confident = ranked.Where(candidate => candidate.Tier != MatchTier.Weak).Take(MaxResults).ToList();

        // An uncertain match beats an empty page.
        var results = confident.Count > 0
            ? confident
            : ranked.Take(MaxResults).ToList();

        _logger.LogInformation(
            "Search returned {ResultCount} of {RankedCount} candidates (fallback: {IsFallback})",
            results.Count,
            ranked.Count,
            interpretation.IsFallback);

        return new SearchResponse
        {
            Interpretation = interpretation,
            Results = results
        };
    }
}
