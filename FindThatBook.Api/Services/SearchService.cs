using FindThatBook.Api.Clients;
using FindThatBook.Api.Models;

namespace FindThatBook.Api.Services;

public class SearchService : ISearchService
{
    private const int MaxResults = 5;

    private readonly IQueryParser _queryParser;
    private readonly IOpenLibraryClient _openLibrary;
    private readonly IBookMatcher _matcher;
    private readonly IExplanationService _explanationService;
    private readonly ILogger<SearchService> _logger;

    public SearchService(
        IQueryParser queryParser,
        IOpenLibraryClient openLibrary,
        IBookMatcher matcher,
        IExplanationService explanationService,
        ILogger<SearchService> logger)
    {
        _queryParser = queryParser;
        _openLibrary = openLibrary;
        _matcher = matcher;
        _explanationService = explanationService;
        _logger = logger;
    }

    public async Task<SearchResponse> SearchAsync(string query, CancellationToken ct)
    {
        var parsed = await _queryParser.ParseAsync(query, ct);

        var (works, interpretation) = await FindWorksAsync(parsed, ct);

        var ranked = _matcher.Rank(works, interpretation);
        var confident = ranked.Where(candidate => candidate.Tier != MatchTier.Weak).Take(MaxResults).ToList();

        // An uncertain match beats an empty page.
        var results = confident.Count > 0
            ? confident
            : ranked.Take(MaxResults).ToList();

        // After the trim, so we only pay for explanations we will show.
        await _explanationService.ApplyExplanationsAsync(results, interpretation, ct);

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

    // The fielded search ANDs title and author, so one wrong field finds nothing even when the other is right.
    // Returns the interpretation that produced the works, so the tiers and the UI describe the search that ran.
    private async Task<(List<OpenLibraryWork> Works, QueryInterpretation Interpretation)> FindWorksAsync(
        QueryInterpretation interpretation,
        CancellationToken ct)
    {
        // A fallback interpretation has no usable title or author, so only full-text search finds anything.
        if (interpretation.IsFallback)
        {
            return (await _openLibrary.SearchRawAsync(interpretation.RawQuery, ct), interpretation);
        }

        var works = await _openLibrary.SearchAsync(interpretation.Title, interpretation.Author, ct);

        if (works.Count > 0)
        {
            return (works, interpretation);
        }

        if (!string.IsNullOrWhiteSpace(interpretation.Author))
        {
            _logger.LogInformation("No match for the title and author together; retrying with the author alone");

            // Dropping the title drops it from the tiering, so these rank as "by this author".
            var authorOnly = WithoutTitle(interpretation, isFallback: false);
            works = await _openLibrary.SearchAsync(null, authorOnly.Author, ct);

            if (works.Count > 0)
            {
                return (works, authorOnly);
            }
        }

        _logger.LogInformation("No match for the extracted fields; retrying as a full-text search");

        // Same full-text fallback the parser uses, flagged so the page shows broader results.
        return (
            await _openLibrary.SearchRawAsync(interpretation.RawQuery, ct),
            WithoutTitle(interpretation, isFallback: true));
    }

    // The author survives: it still earns a tier on whatever the wider search returns.
    private static QueryInterpretation WithoutTitle(QueryInterpretation interpretation, bool isFallback) => new()
    {
        RawQuery = interpretation.RawQuery,
        Title = null,
        Author = interpretation.Author,
        Keywords = interpretation.Keywords,
        IsFallback = isFallback
    };
}
