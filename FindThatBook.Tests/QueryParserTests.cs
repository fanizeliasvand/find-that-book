using FindThatBook.Api.Clients;
using FindThatBook.Api.Models;
using FindThatBook.Api.Services;
using FindThatBook.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FindThatBook.Tests;

public class QueryParserTests
{
    [Fact]
    public async Task ParseAsync_CleanJson_PopulatesTitleAuthorAndKeywords()
    {
        var gemini = FakeGeminiClient.Returning(
            """{"title":"The Hobbit","author":"J. R. R. Tolkien","keywords":["illustrated","1937"]}""");

        var result = await CreateParser(gemini).ParseAsync("tolkien hobbit", CancellationToken.None);

        Assert.False(result.IsFallback);
        Assert.Equal("The Hobbit", result.Title);
        Assert.Equal("J. R. R. Tolkien", result.Author);
        Assert.Equal(["illustrated", "1937"], result.Keywords);
    }

    [Fact]
    public async Task ParseAsync_JsonWrappedInMarkdownFences_StillParses()
    {
        var gemini = FakeGeminiClient.Returning(
            """
            ```json
            {"title":"Dune","author":"Frank Herbert","keywords":[]}
            ```
            """);

        var result = await CreateParser(gemini).ParseAsync("dune", CancellationToken.None);

        Assert.False(result.IsFallback);
        Assert.Equal("Dune", result.Title);
    }

    [Fact]
    public async Task ParseAsync_JsonSurroundedByProse_StillParses()
    {
        var gemini = FakeGeminiClient.Returning(
            """
            Here is the JSON you asked for:
            {"title":"Dune","author":"Frank Herbert","keywords":[]}
            Let me know if you need anything else.
            """);

        var result = await CreateParser(gemini).ParseAsync("dune", CancellationToken.None);

        Assert.False(result.IsFallback);
        Assert.Equal("Dune", result.Title);
        Assert.Equal("Frank Herbert", result.Author);
    }

    [Fact]
    public async Task ParseAsync_PropertyNamesInDifferentCasing_StillParses()
    {
        var gemini = FakeGeminiClient.Returning(
            """{"Title":"Dune","AUTHOR":"Frank Herbert","Keywords":["epic"]}""");

        var result = await CreateParser(gemini).ParseAsync("dune", CancellationToken.None);

        Assert.False(result.IsFallback);
        Assert.Equal("Dune", result.Title);
        Assert.Equal("Frank Herbert", result.Author);
        Assert.Equal(["epic"], result.Keywords);
    }

    [Fact]
    public async Task ParseAsync_ExplicitNullKeywords_YieldsEmptyListNotNull()
    {
        var gemini = FakeGeminiClient.Returning(
            """{"title":"Dune","author":"Frank Herbert","keywords":null}""");

        var result = await CreateParser(gemini).ParseAsync("dune", CancellationToken.None);

        Assert.NotNull(result.Keywords);
        Assert.Empty(result.Keywords);
    }

    [Fact]
    public async Task ParseAsync_SendsTheRawQueryInThePrompt()
    {
        var gemini = FakeGeminiClient.Returning("""{"title":"Dune","author":null,"keywords":[]}""");

        await CreateParser(gemini).ParseAsync("that dune book", CancellationToken.None);

        Assert.Contains("that dune book", gemini.LastPrompt);
    }

    [Fact]
    public async Task ParseAsync_EmptyResponse_FallsBack()
    {
        var gemini = FakeGeminiClient.Returning(string.Empty);

        var result = await CreateParser(gemini).ParseAsync("dune", CancellationToken.None);

        AssertFallback(result, "dune");
    }

    [Fact]
    public async Task ParseAsync_ResponseWithNoJson_FallsBack()
    {
        var gemini = FakeGeminiClient.Returning("I am not able to answer that.");

        var result = await CreateParser(gemini).ParseAsync("dune", CancellationToken.None);

        AssertFallback(result, "dune");
    }

    [Fact]
    public async Task ParseAsync_MalformedJson_FallsBack()
    {
        var gemini = FakeGeminiClient.Returning("""{"title":"Dune","author":}""");

        var result = await CreateParser(gemini).ParseAsync("dune", CancellationToken.None);

        AssertFallback(result, "dune");
    }

    [Fact]
    public async Task ParseAsync_JsonWithNullTitleAndNullAuthor_FallsBack()
    {
        var gemini = FakeGeminiClient.Returning("""{"title":null,"author":null,"keywords":["dune"]}""");

        var result = await CreateParser(gemini).ParseAsync("dune", CancellationToken.None);

        AssertFallback(result, "dune");
    }

    [Fact]
    public async Task ParseAsync_ClientThrowsHttpRequestException_FallsBack()
    {
        var gemini = FakeGeminiClient.Throwing(new HttpRequestException("503"));

        var result = await CreateParser(gemini).ParseAsync("dune", CancellationToken.None);

        AssertFallback(result, "dune");
    }

    [Fact]
    public async Task ParseAsync_ClientThrowsOperationCanceled_RethrowsInsteadOfFallingBack()
    {
        var gemini = FakeGeminiClient.Throwing(new OperationCanceledException());

        var parser = CreateParser(gemini);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => parser.ParseAsync("dune", CancellationToken.None));
    }

    private static QueryParser CreateParser(IGeminiClient gemini) =>
        new(gemini, NullLogger<QueryParser>.Instance);

    private static void AssertFallback(QueryInterpretation result, string rawQuery)
    {
        Assert.True(result.IsFallback);
        Assert.Null(result.Title);
        Assert.Null(result.Author);
        Assert.Equal(rawQuery, result.RawQuery);
    }
}
