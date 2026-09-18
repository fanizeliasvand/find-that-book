using System.Text.Json;
using FindThatBook.Api.Clients;
using FindThatBook.Api.Models;

namespace FindThatBook.Api.Services;

public class QueryParser : IQueryParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IGeminiClient _gemini;
    private readonly ILogger<QueryParser> _logger;

    public QueryParser(IGeminiClient gemini, ILogger<QueryParser> logger)
    {
        _gemini = gemini;
        _logger = logger;
    }

    public async Task<QueryInterpretation> ParseAsync(string rawQuery, CancellationToken ct)
    {
        try
        {
            var response = await _gemini.GenerateAsync(BuildPrompt(rawQuery), ct);

            if (string.IsNullOrWhiteSpace(response))
            {
                return Fallback(rawQuery, "Gemini returned no text");
            }

            var json = ExtractJsonObject(response);

            if (json.Length == 0)
            {
                return Fallback(rawQuery, "no JSON object in the Gemini response");
            }

            var parsed = JsonSerializer.Deserialize<QueryInterpretation>(json, JsonOptions);

            if (parsed is null ||
                (string.IsNullOrWhiteSpace(parsed.Title) && string.IsNullOrWhiteSpace(parsed.Author)))
            {
                return Fallback(rawQuery, "Gemini returned neither a title nor an author");
            }

            // An explicit "keywords": null in the JSON overwrites the property initializer.
            parsed.Keywords ??= [];
            parsed.RawQuery = rawQuery;

            return parsed;
        }
        // Only a caller cancel propagates. GeminiClient's own timeout is also an
        // OperationCanceledException, and that is just another failure to fall back from.
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Fallback(rawQuery, "the Gemini call or its parsing failed", exception);
        }
    }

    private static string BuildPrompt(string rawQuery) =>
        $$"""
        You extract structured search fields from a messy book query.

        Return only JSON in this exact shape. No markdown, no code fences, no prose:
        {"title": string|null, "author": string|null, "keywords": string[]}

        Rules:
        - title: the book title alone, without edition, format or year words.
        - author: the author's name if the query names one, otherwise null.
        - keywords: remaining meaningful terms, such as format, edition or a 4-digit year.

        Query: tolkien hobbit illustrated deluxe 1937
        {"title":"The Hobbit","author":"J. R. R. Tolkien","keywords":["illustrated","deluxe","1937"]}

        Query: that dune book
        {"title":"Dune","author":null,"keywords":[]}

        Query: {{rawQuery}}
        """;

    // Slicing between the outermost braces also strips ```json fences,
    // so they need no separate handling.
    private static string ExtractJsonObject(string response)
    {
        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');

        return start >= 0 && end > start
            ? response[start..(end + 1)]
            : string.Empty;
    }

    private QueryInterpretation Fallback(string rawQuery, string reason, Exception? exception = null)
    {
        _logger.LogWarning(exception, "Falling back to the raw book query: {Reason}", reason);

        // Title/Author stay null: the raw string only finds anything through
        // SearchRawAsync, which reads RawQuery instead.
        return new QueryInterpretation
        {
            RawQuery = rawQuery,
            Title = null,
            Author = null,
            Keywords = [],
            IsFallback = true
        };
    }
}
