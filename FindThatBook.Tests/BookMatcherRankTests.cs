using FindThatBook.Api.Models;
using FindThatBook.Api.Services;

namespace FindThatBook.Tests;

public class BookMatcherRankTests
{
    [Fact]
    public void Rank_ExactTitleAndPrimaryAuthor_IsTierOne()
    {
        var works = new List<OpenLibraryWork> { Work("/works/OL1", "The Hobbit", ["J.R.R. Tolkien"]) };
        var interpretation = Query("The Hobbit", "J.R.R. Tolkien");

        var results = new BookMatcher().Rank(works, interpretation);

        Assert.Equal(MatchTier.ExactTitlePrimaryAuthor, results.Single().Tier);
    }

    [Fact]
    public void Rank_ExactTitleWithAuthorAsContributor_IsTierTwo()
    {
        var works = new List<OpenLibraryWork>
        {
            Work("/works/OL1", "The Hobbit", ["Charles Dixon", "J.R.R. Tolkien"])
        };
        var interpretation = Query("The Hobbit", "J.R.R. Tolkien");

        var result = new BookMatcher().Rank(works, interpretation).Single();

        Assert.Equal(MatchTier.ExactTitleContributorAuthor, result.Tier);
        Assert.Equal(1, result.Evidence.AuthorPosition);
    }

    [Fact]
    public void Rank_PrimaryAuthorMatchSortsAboveContributorMatch()
    {
        var works = new List<OpenLibraryWork>
        {
            Work("/works/OL1", "The Hobbit", ["Charles Dixon", "J.R.R. Tolkien"]),
            Work("/works/OL2", "The Hobbit", ["J.R.R. Tolkien"])
        };
        var interpretation = Query("The Hobbit", "J.R.R. Tolkien");

        var results = new BookMatcher().Rank(works, interpretation);

        Assert.Equal(MatchTier.ExactTitlePrimaryAuthor, results[0].Tier);
        Assert.Equal(MatchTier.ExactTitleContributorAuthor, results[1].Tier);
    }

    [Fact]
    public void Rank_TitleMatchesAndNoAuthorSupplied_IsTierThree()
    {
        var works = new List<OpenLibraryWork> { Work("/works/OL1", "The Hobbit", ["J.R.R. Tolkien"]) };
        var interpretation = Query("The Hobbit", null);

        var results = new BookMatcher().Rank(works, interpretation);

        Assert.Equal(MatchTier.TitleOnlyNoAuthorGiven, results.Single().Tier);
    }

    [Fact]
    public void Rank_TitleMatchesOnlyAsSubstringWithAuthorMatch_IsTierFour()
    {
        var works = new List<OpenLibraryWork>
        {
            Work("/works/OL1", "The Annotated Hobbit", ["Douglas A. Anderson"])
        };
        var interpretation = Query("Hobbit", "Douglas A. Anderson");

        var result = new BookMatcher().Rank(works, interpretation).Single();

        Assert.Equal(MatchTier.NearTitleWithAuthor, result.Tier);
        Assert.Equal(TitleMatchKind.Contains, result.Evidence.TitleMatchKind);
    }

    [Fact]
    public void Rank_AuthorSuppliedWithNoTitle_IsTierFive()
    {
        var works = new List<OpenLibraryWork>
        {
            Work("/works/OL1", "The Left Hand of Darkness", ["Ursula K. Le Guin"])
        };
        var interpretation = Query(null, "Ursula K. Le Guin");

        var results = new BookMatcher().Rank(works, interpretation);

        Assert.Equal(MatchTier.AuthorOnlyNoTitleGiven, results.Single().Tier);
    }

    [Fact]
    public void Rank_TitleFollowedBySubtitle_MatchesAsPrefix()
    {
        var works = new List<OpenLibraryWork>
        {
            Work("/works/OL1", "The Hobbit, or There and Back Again", ["J.R.R. Tolkien"])
        };
        var interpretation = Query("hobbit", null);

        var result = new BookMatcher().Rank(works, interpretation).Single();

        Assert.Equal(TitleMatchKind.Prefix, result.Evidence.TitleMatchKind);
    }

    [Fact]
    public void Rank_AuthorQueryMissingMiddleInitial_StillMatches()
    {
        var works = new List<OpenLibraryWork>
        {
            Work("/works/OL1", "The Left Hand of Darkness", ["Ursula K. Le Guin"])
        };
        var interpretation = Query(null, "ursula le guin");

        var result = new BookMatcher().Rank(works, interpretation).Single();

        Assert.Equal(0, result.Evidence.AuthorPosition);
        Assert.Equal("Ursula K. Le Guin", result.Evidence.MatchedAuthorName);
    }

    [Fact]
    public void Rank_SameKeyTwice_CollapsesToEarliestYearAndHighestEditionCount()
    {
        var works = new List<OpenLibraryWork>
        {
            Work("/works/OL1", "The Hobbit", ["J.R.R. Tolkien"], firstPublishYear: 1937, editionCount: 400),
            Work("/works/OL1", "The Hobbit", ["J.R.R. Tolkien"], firstPublishYear: 2020, editionCount: 12)
        };
        var interpretation = Query("The Hobbit", "J.R.R. Tolkien");

        var result = new BookMatcher().Rank(works, interpretation).Single();

        Assert.Equal(1937, result.FirstPublishYear);
        Assert.Equal(400, result.EditionCount);
    }

    [Fact]
    public void Rank_SameTitleAndPrimaryAuthorUnderDifferentKeys_CollapsesKeepingTheWidestEdition()
    {
        var works = new List<OpenLibraryWork>
        {
            Work("/works/OL1", "The Hobbit", ["J.R.R. Tolkien"], firstPublishYear: 1937, editionCount: 481),
            Work("/works/OL2", "The Hobbit", ["J.R.R. Tolkien"], firstPublishYear: 2026, coverId: 999, editionCount: 2)
        };
        var interpretation = Query("The Hobbit", "J.R.R. Tolkien");

        var result = new BookMatcher().Rank(works, interpretation).Single();

        Assert.Equal(481, result.EditionCount);
        Assert.Equal("https://openlibrary.org/works/OL1", result.OpenLibraryUrl);
        Assert.Equal(1937, result.FirstPublishYear);
        Assert.Equal("https://covers.openlibrary.org/b/id/999-M.jpg", result.CoverUrl);
    }

    [Fact]
    public void Rank_SameTitleButDifferentPrimaryAuthor_DoesNotCollapse()
    {
        var works = new List<OpenLibraryWork>
        {
            Work("/works/OL1", "The Hobbit", ["J.R.R. Tolkien"]),
            Work("/works/OL2", "The Hobbit", ["Charles Dixon"])
        };
        var interpretation = Query("The Hobbit", null);

        var results = new BookMatcher().Rank(works, interpretation);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Rank_WithinATier_MatchingYearSortsAboveNonMatchingYear()
    {
        var works = new List<OpenLibraryWork>
        {
            Work("/works/OL1", "The Hobbit", ["Author One"], firstPublishYear: 2000, editionCount: 500),
            Work("/works/OL2", "The Hobbit", ["Author Two"], firstPublishYear: 1937, editionCount: 1)
        };
        var interpretation = Query("The Hobbit", null, "1937");

        var results = new BookMatcher().Rank(works, interpretation);

        Assert.Equal("https://openlibrary.org/works/OL2", results[0].OpenLibraryUrl);
        Assert.True(results[0].Evidence.YearMatched);
        Assert.False(results[1].Evidence.YearMatched);
    }

    private static OpenLibraryWork Work(
        string key,
        string title,
        List<string>? authorNames = null,
        int? firstPublishYear = 2000,
        int? coverId = null,
        int editionCount = 1) => new()
        {
            Key = key,
            Title = title,
            AuthorNames = authorNames ?? [],
            FirstPublishYear = firstPublishYear,
            CoverId = coverId,
            EditionCount = editionCount
        };

    private static QueryInterpretation Query(string? title, string? author, params string[] keywords) => new()
    {
        Title = title,
        Author = author,
        Keywords = [.. keywords]
    };
}
