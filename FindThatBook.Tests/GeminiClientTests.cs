using System.Net;
using FindThatBook.Api.Clients;
using FindThatBook.Tests.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FindThatBook.Tests;

public class GeminiClientTests
{
    private const string ApiKey = "test-key-123";

    private const string SuccessBody =
        """{"candidates":[{"content":{"parts":[{"text":"hello"}],"role":"model"}}]}""";

    [Fact]
    public async Task GenerateAsync_ServerErrorThenSuccess_RetriesAndReturnsText()
    {
        var handler = new StubHttpMessageHandler(
            (HttpStatusCode.ServiceUnavailable, "{}"),
            (HttpStatusCode.OK, SuccessBody));

        var result = await CreateClient(handler).GenerateAsync("prompt", CancellationToken.None);

        Assert.Equal("hello", result);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_ServerErrorOnEveryAttempt_ThrowsAfterThreeAttempts()
    {
        var handler = new StubHttpMessageHandler((HttpStatusCode.ServiceUnavailable, "{}"));

        var client = CreateClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GenerateAsync("prompt", CancellationToken.None));
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_BadRequest_ThrowsWithoutRetrying()
    {
        var handler = new StubHttpMessageHandler(
            (HttpStatusCode.BadRequest, """{"error":{"message":"API key not valid"}}"""));

        var client = CreateClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GenerateAsync("prompt", CancellationToken.None));
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_TooManyRequests_ThrowsWithoutRetrying()
    {
        var handler = new StubHttpMessageHandler(
            (HttpStatusCode.TooManyRequests, """{"error":{"message":"quota exceeded"}}"""));

        var client = CreateClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GenerateAsync("prompt", CancellationToken.None));
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_ResponseWithNoCandidates_ReturnsEmptyString()
    {
        var handler = new StubHttpMessageHandler((HttpStatusCode.OK, """{"candidates":[]}"""));

        var result = await CreateClient(handler).GenerateAsync("prompt", CancellationToken.None);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GenerateAsync_FirstPartCarriesNoText_ReturnsTextFromTheLaterPart()
    {
        var handler = new StubHttpMessageHandler((
            HttpStatusCode.OK,
            """{"candidates":[{"content":{"parts":[{"thoughtSignature":"abc"},{"text":"the answer"}]}}]}"""));

        var result = await CreateClient(handler).GenerateAsync("prompt", CancellationToken.None);

        Assert.Equal("the answer", result);
    }

    [Fact]
    public async Task GenerateAsync_SendsApiKeyAsHeaderAndKeepsItOutOfTheUrl()
    {
        var handler = new StubHttpMessageHandler((HttpStatusCode.OK, SuccessBody));

        await CreateClient(handler).GenerateAsync("prompt", CancellationToken.None);

        var request = handler.Requests.Single();
        Assert.Equal(ApiKey, request.ApiKeyHeader);
        Assert.DoesNotContain(ApiKey, request.Url);
    }

    private static GeminiClient CreateClient(StubHttpMessageHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Gemini:ApiKey"] = ApiKey })
            .Build();

        return new GeminiClient(
            new HttpClient(handler),
            configuration,
            NullLogger<GeminiClient>.Instance);
    }
}
