using FindThatBook.Api.Models;
using FindThatBook.Api.Services;
using FindThatBook.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FindThatBook.Tests;

public class SearchServiceTests
{
    [Fact]
    public async Task SearchAsync_NormalInterpretation_SearchesByTitleAndAuthor()
    {
        var openLibrary = new FakeOpenLibraryClient(Work("/works/OL1", "The Hobbit", ["J.R.R. Tolkien"]));
        var service = CreateService(Parsed("The Hobbit", "J.R.R. Tolkien"), openLibrary);

        await service.SearchAsync("tolkien hobbit", CancellationToken.None);

        Assert.Equal("SearchAsync", openLibrary.LastMethod);
        Assert.Equal("The Hobbit", openLibrary.LastTitle);
        Assert.Equal("J.R.R. Tolkien", openLibrary.LastAuthor);
    }

    [Fact]
    public async Task SearchAsync_FallbackInterpretation_SearchesByRawQueryInstead()
    {
        var openLibrary = new FakeOpenLibraryClient(Work("/works/OL1", "The Hobbit", ["J.R.R. Tolkien"]));
        var service = CreateService(FallbackFor("some messy query"), openLibrary);

        await service.SearchAsync("some messy query", CancellationToken.None);

        Assert.Equal("SearchRawAsync", openLibrary.LastMethod);
        Assert.Equal("some messy query", openLibrary.LastRawQuery);
    }

    [Fact]
    public async Task SearchAsync_TitleAndAuthorFindNothing_RetriesWithTheAuthorAlone()
    {
        var openLibrary = FakeOpenLibraryClient.Sequence(
            [],
            [Work("/works/OL1", "The Great Gatsby", ["F. Scott Fitzgerald"])]);
        var service = CreateService(
            Parsed("The Grate Gatsbee", "F. Scott Fitzgerald", "the grate gatsbee fitzgerald"),
            openLibrary);

        var response = await service.SearchAsync("the grate gatsbee fitzgerald", CancellationToken.None);

        Assert.Equal(
            [("The Grate Gatsbee", "F. Scott Fitzgerald"), (null, "F. Scott Fitzgerald")],
            openLibrary.FieldedCalls);

        // The title must leave the interpretation too, or the tier and the page disagree.
        Assert.Null(response.Interpretation.Title);
        Assert.False(response.Interpretation.IsFallback);
        Assert.Equal(MatchTier.AuthorOnlyNoTitleGiven, response.Results.Single().Tier);
    }

    [Fact]
    public async Task SearchAsync_AuthorAloneAlsoFindsNothing_FallsBackToFullTextSearch()
    {
        var openLibrary = FakeOpenLibraryClient.Sequence(
            [],
            [],
            [Work("/works/OL1", "The Great Gatsby", ["F. Scott Fitzgerald"])]);
        var service = CreateService(
            Parsed("The Grate Gatsbee", "F. Scott Fitzgerald", "the grate gatsbee fitzgerald"),
            openLibrary);

        var response = await service.SearchAsync("the grate gatsbee fitzgerald", CancellationToken.None);

        Assert.Equal("SearchRawAsync", openLibrary.LastMethod);
        Assert.Equal("the grate gatsbee fitzgerald", openLibrary.LastRawQuery);
        Assert.True(response.Interpretation.IsFallback);
        Assert.Single(response.Results);
    }

    [Fact]
    public async Task SearchAsync_TitleFindsNothingAndNoAuthorWasExtracted_SkipsStraightToFullTextSearch()
    {
        var openLibrary = FakeOpenLibraryClient.Sequence(
            [],
            [Work("/works/OL1", "Dune", ["Frank Herbert"])]);
        var service = CreateService(Parsed("Dunes", null, "that dunes book"), openLibrary);

        await service.SearchAsync("that dunes book", CancellationToken.None);

        // One fielded attempt only: there is no author to retry with.
        Assert.Equal([("Dunes", null)], openLibrary.FieldedCalls);
        Assert.Equal("SearchRawAsync", openLibrary.LastMethod);
    }

    [Fact]
    public async Task SearchAsync_WeakResultsAreDroppedWhenStrongerOnesExist()
    {
        var openLibrary = new FakeOpenLibraryClient(
            Work("/works/OL1", "The Hobbit", ["J.R.R. Tolkien"]),
            Work("/works/OL2", "Something Else Entirely", ["Another Person"]));
        var service = CreateService(Parsed("The Hobbit", "J.R.R. Tolkien"), openLibrary);

        var response = await service.SearchAsync("tolkien hobbit", CancellationToken.None);

        Assert.Equal(MatchTier.ExactTitlePrimaryAuthor, response.Results.Single().Tier);
    }

    [Fact]
    public async Task SearchAsync_WhenEveryResultIsWeak_ReturnsThemRatherThanNothing()
    {
        var openLibrary = new FakeOpenLibraryClient(
            Work("/works/OL1", "Something Else Entirely", ["Another Person"]),
            Work("/works/OL2", "A Different Book", ["Someone Unrelated"]));
        var service = CreateService(Parsed("The Hobbit", "J.R.R. Tolkien"), openLibrary);

        var response = await service.SearchAsync("tolkien hobbit", CancellationToken.None);

        Assert.Equal(2, response.Results.Count);
        Assert.All(response.Results, result => Assert.Equal(MatchTier.Weak, result.Tier));
    }

    [Fact]
    public async Task SearchAsync_NeverReturnsMoreThanFiveResults()
    {
        var openLibrary = new FakeOpenLibraryClient(ManyMatchingWorks(7));
        var service = CreateService(Parsed("Dune", null), openLibrary);

        var response = await service.SearchAsync("dune", CancellationToken.None);

        Assert.Equal(5, response.Results.Count);
    }

    [Fact]
    public async Task SearchAsync_ReturnsTheInterpretationToTheCaller()
    {
        var interpretation = Parsed("The Hobbit", "J.R.R. Tolkien");
        var openLibrary = new FakeOpenLibraryClient(Work("/works/OL1", "The Hobbit", ["J.R.R. Tolkien"]));
        var service = CreateService(interpretation, openLibrary);

        var response = await service.SearchAsync("tolkien hobbit", CancellationToken.None);

        Assert.Same(interpretation, response.Interpretation);
    }

    [Fact]
    public async Task SearchAsync_ExplainsOnlyTheTrimmedResultSet()
    {
        var openLibrary = new FakeOpenLibraryClient(ManyMatchingWorks(7));
        var explanations = new FakeExplanationService();
        var service = CreateService(Parsed("Dune", null), openLibrary, explanations);

        await service.SearchAsync("dune", CancellationToken.None);

        Assert.Equal(1, explanations.CallCount);
        Assert.Equal(5, explanations.ReceivedCandidates.Count);
    }

    // Distinct primary authors keep these out of the dedup pass, so the count supplied is the count trimmed.
    private static OpenLibraryWork[] ManyMatchingWorks(int count) =>
        Enumerable.Range(1, count)
            .Select(index => Work($"/works/OL{index}", "Dune", [$"Author Number {index}"]))
            .ToArray();

    private static SearchService CreateService(
        QueryInterpretation interpretation,
        FakeOpenLibraryClient openLibrary,
        FakeExplanationService? explanations = null) =>
        new(
            new FakeQueryParser(interpretation),
            openLibrary,
            new BookMatcher(),
            explanations ?? new FakeExplanationService(),
            NullLogger<SearchService>.Instance);

    private static QueryInterpretation Parsed(string? title, string? author, string rawQuery = "raw query") => new()
    {
        RawQuery = rawQuery,
        Title = title,
        Author = author,
        IsFallback = false
    };

    private static QueryInterpretation FallbackFor(string rawQuery) => new()
    {
        RawQuery = rawQuery,
        IsFallback = true
    };

    private static OpenLibraryWork Work(string key, string title, List<string> authorNames) => new()
    {
        Key = key,
        Title = title,
        AuthorNames = authorNames,
        FirstPublishYear = 2000,
        EditionCount = 1
    };
}
