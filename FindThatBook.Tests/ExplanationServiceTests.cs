using FindThatBook.Api.Clients;
using FindThatBook.Api.Models;
using FindThatBook.Api.Services;
using FindThatBook.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FindThatBook.Tests;

public class ExplanationServiceTests
{
    [Fact]
    public async Task ApplyExplanationsAsync_UnambiguousTopMatch_IsExplainedWithoutCallingTheModel()
    {
        var candidate = Unambiguous("The Hobbit");
        var gemini = FakeGeminiClient.Returning("[]");

        await CreateService(gemini).ApplyExplanationsAsync([candidate], Interpretation(), CancellationToken.None);

        Assert.Equal("Exact title match; J.R.R. Tolkien is the primary author.", candidate.Explanation);
        Assert.Null(gemini.LastPrompt);
        Assert.Equal(0, gemini.CallCount);
    }

    [Fact]
    public async Task ApplyExplanationsAsync_MixedBatch_SendsOnlyTheAmbiguousCandidate()
    {
        var unambiguous = Unambiguous("The Hobbit");
        var ambiguous = Ambiguous("Dune");
        var gemini = FakeGeminiClient.Returning("""[{"index":0,"explanation":"from the model"}]""");

        await CreateService(gemini).ApplyExplanationsAsync(
            [unambiguous, ambiguous],
            Interpretation(),
            CancellationToken.None);

        Assert.Equal("from the model", ambiguous.Explanation);
        Assert.Equal("Exact title match; J.R.R. Tolkien is the primary author.", unambiguous.Explanation);
        Assert.DoesNotContain("The Hobbit", gemini.LastPrompt);
    }

    [Fact]
    public async Task ApplyExplanationsAsync_ManyAmbiguousCandidates_MakesASingleCall()
    {
        var candidates = new List<BookCandidate>
        {
            Ambiguous("Dune"),
            Ambiguous("Dune Messiah"),
            Ambiguous("Children of Dune"),
            Ambiguous("God Emperor of Dune")
        };
        var gemini = FakeGeminiClient.Returning(
            """[{"index":0,"explanation":"a"},{"index":1,"explanation":"b"},{"index":2,"explanation":"c"},{"index":3,"explanation":"d"}]""");

        await CreateService(gemini).ApplyExplanationsAsync(candidates, Interpretation(), CancellationToken.None);

        Assert.Equal(1, gemini.CallCount);
    }

    [Fact]
    public async Task ApplyExplanationsAsync_PromptCarriesTheAuthorEvidence()
    {
        var candidate = Ambiguous("The Hobbit", MatchTier.ExactTitleContributorAuthor, authorPosition: 1);
        var gemini = FakeGeminiClient.Returning("[]");

        await CreateService(gemini).ApplyExplanationsAsync([candidate], Interpretation(), CancellationToken.None);

        Assert.Contains("J.R.R. Tolkien", gemini.LastPrompt);
        Assert.Contains("contributor", gemini.LastPrompt);
    }

    [Fact]
    public async Task ApplyExplanationsAsync_ValidJsonArray_AssignsExplanationsByIndex()
    {
        var first = Ambiguous("Dune");
        var second = Ambiguous("Dune Messiah");

        // Deliberately out of order, so a positional assignment would fail this.
        var gemini = FakeGeminiClient.Returning(
            """[{"index":1,"explanation":"second"},{"index":0,"explanation":"first"}]""");

        await CreateService(gemini).ApplyExplanationsAsync(
            [first, second],
            Interpretation(),
            CancellationToken.None);

        Assert.Equal("first", first.Explanation);
        Assert.Equal("second", second.Explanation);
    }

    [Fact]
    public async Task ApplyExplanationsAsync_ModelThrows_EveryCandidateStillGetsAnExplanation()
    {
        var candidates = new List<BookCandidate> { Ambiguous("Dune"), Ambiguous("Dune Messiah") };
        var gemini = FakeGeminiClient.Throwing(new HttpRequestException("503"));

        await CreateService(gemini).ApplyExplanationsAsync(candidates, Interpretation(), CancellationToken.None);

        Assert.All(candidates, candidate => Assert.False(string.IsNullOrWhiteSpace(candidate.Explanation)));
        Assert.All(candidates, candidate => Assert.Contains("title matches your search exactly", candidate.Explanation));
    }

    [Fact]
    public async Task ApplyExplanationsAsync_MalformedResponse_FallsBackToEvidenceText()
    {
        var candidate = Ambiguous("Dune");
        var gemini = FakeGeminiClient.Returning("I could not produce that.");

        await CreateService(gemini).ApplyExplanationsAsync([candidate], Interpretation(), CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(candidate.Explanation));
        Assert.Contains("title matches your search exactly", candidate.Explanation);
    }

    [Fact]
    public async Task ApplyExplanationsAsync_ModelSkipsAnIndex_ThatCandidateStillGetsAFallback()
    {
        var answered = Ambiguous("Dune");
        var skipped = Ambiguous("Dune Messiah");
        var gemini = FakeGeminiClient.Returning("""[{"index":0,"explanation":"only the first"}]""");

        await CreateService(gemini).ApplyExplanationsAsync(
            [answered, skipped],
            Interpretation(),
            CancellationToken.None);

        Assert.Equal("only the first", answered.Explanation);
        Assert.False(string.IsNullOrWhiteSpace(skipped.Explanation));
    }

    [Fact]
    public async Task ApplyExplanationsAsync_OperationCanceled_IsRethrownRatherThanSwallowed()
    {
        var gemini = FakeGeminiClient.Throwing(new OperationCanceledException());
        var service = CreateService(gemini);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.ApplyExplanationsAsync([Ambiguous("Dune")], Interpretation(), CancellationToken.None));
    }

    private static ExplanationService CreateService(IGeminiClient gemini) =>
        new(gemini, NullLogger<ExplanationService>.Instance);

    private static QueryInterpretation Interpretation() => new() { RawQuery = "a search phrase" };

    // Tier 1, exact title, single author: nothing to disambiguate.
    private static BookCandidate Unambiguous(string title) =>
        Build(title, MatchTier.ExactTitlePrimaryAuthor, TitleMatchKind.Exact, 0, []);

    private static BookCandidate Ambiguous(
        string title,
        MatchTier tier = MatchTier.TitleOnlyNoAuthorGiven,
        int? authorPosition = 0) =>
        Build(title, tier, TitleMatchKind.Exact, authorPosition, ["Someone Else"]);

    private static BookCandidate Build(
        string title,
        MatchTier tier,
        TitleMatchKind titleMatch,
        int? authorPosition,
        List<string> contributors) => new()
        {
            Title = title,
            PrimaryAuthor = "J.R.R. Tolkien",
            Contributors = contributors,
            FirstPublishYear = 1937,
            EditionCount = 10,
            OpenLibraryUrl = "https://openlibrary.org/works/OL1",
            Tier = tier,
            Evidence = new MatchEvidence
            {
                TitleMatchKind = titleMatch,
                AuthorPosition = authorPosition,
                MatchedAuthorName = "J.R.R. Tolkien",
                OtherAuthorNames = [],
                MatchedKeywords = [],
                YearMatched = false
            }
        };
}
