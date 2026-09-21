using System.Text;
using System.Text.Json;
using FindThatBook.Api.Clients;
using FindThatBook.Api.Models;

namespace FindThatBook.Api.Services;

public class ExplanationService : IExplanationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IGeminiClient _gemini;
    private readonly ILogger<ExplanationService> _logger;

    public ExplanationService(IGeminiClient gemini, ILogger<ExplanationService> logger)
    {
        _gemini = gemini;
        _logger = logger;
    }

    public async Task ApplyExplanationsAsync(
        List<BookCandidate> candidates,
        QueryInterpretation interpretation,
        CancellationToken ct)
    {
        var needsModel = new List<BookCandidate>();

        foreach (var candidate in candidates)
        {
            if (IsUnambiguous(candidate))
            {
                candidate.Explanation =
                    $"Exact title match; {candidate.Evidence.MatchedAuthorName} is the primary author.";
            }
            else
            {
                needsModel.Add(candidate);
            }
        }

        if (needsModel.Count == 0)
        {
            return;
        }

        try
        {
            var response = await _gemini.GenerateAsync(BuildPrompt(needsModel, interpretation), ct);
            ApplyModelExplanations(response, needsModel);
        }
        // Only a caller cancel propagates; a GeminiClient timeout falls back like any failure.
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Explanation generation failed; falling back to evidence text");
        }

        // Covers a failed call and any index the model skipped.
        foreach (var candidate in needsModel.Where(c => string.IsNullOrWhiteSpace(c.Explanation)))
        {
            candidate.Explanation = BuildFallbackExplanation(candidate);
        }
    }

    // A top-tier match with a single author has nothing to disambiguate, so skip the API call.
    private static bool IsUnambiguous(BookCandidate candidate) =>
        candidate.Tier == MatchTier.ExactTitlePrimaryAuthor &&
        candidate.Evidence.TitleMatchKind == TitleMatchKind.Exact &&
        candidate.Contributors.Count == 0 &&
        !string.IsNullOrWhiteSpace(candidate.Evidence.MatchedAuthorName);

    private void ApplyModelExplanations(string response, List<BookCandidate> candidates)
    {
        var json = ExtractJsonArray(response);

        if (json.Length == 0)
        {
            _logger.LogWarning("No JSON array in the explanation response");
            return;
        }

        var items = JsonSerializer.Deserialize<List<ExplanationItem>>(json, JsonOptions);

        if (items is null)
        {
            return;
        }

        foreach (var item in items)
        {
            if (item.Index >= 0 &&
                item.Index < candidates.Count &&
                !string.IsNullOrWhiteSpace(item.Explanation))
            {
                candidates[item.Index].Explanation = item.Explanation.Trim();
            }
        }
    }

    // Slicing between the outermost brackets drops fences and prose.
    // It also reaches the array if the model wraps it in an object.
    private static string ExtractJsonArray(string response)
    {
        var start = response.IndexOf('[');
        var end = response.LastIndexOf(']');

        return start >= 0 && end > start
            ? response[start..(end + 1)]
            : string.Empty;
    }

    private static string BuildPrompt(List<BookCandidate> candidates, QueryInterpretation interpretation)
    {
        var facts = new StringBuilder();

        for (var index = 0; index < candidates.Count; index++)
        {
            facts.Append(DescribeCandidate(index, candidates[index]));
        }

        return $$"""
            You explain why each book below matched a user's book search.

            The user searched for: {{interpretation.RawQuery}}

            Write exactly one sentence per book, addressed to the user, and keep
            it to about 20 words. Lead with the strongest signal for that book,
            then add supporting detail. Do not mention the publication year
            unless the year from the query matched.

            Use only the facts listed under that book. Do not invent anything
            you were not given: no plot, genre, awards or publication details.

            Omit facts that add no information. Never state that there are no
            other authors, no matched keywords or no year match. Mention those
            only when they are actually present and meaningful.

            Match this tone:
            "Exact title match; Tolkien is primary author, Alan Lee listed as illustrator."

            Return only a JSON array in this shape. No markdown, no prose:
            [{"index": 0, "explanation": "..."}]

            Books:
            {{facts}}
            """;
    }

    private static string DescribeCandidate(int index, BookCandidate candidate)
    {
        var evidence = candidate.Evidence;
        var builder = new StringBuilder();

        builder.AppendLine($"[{index}]");
        builder.AppendLine($"  Title: {candidate.Title}");
        builder.AppendLine($"  First published: {DescribeYear(candidate.FirstPublishYear)}");
        builder.AppendLine($"  Title match: {evidence.TitleMatchKind}");
        builder.AppendLine($"  Author match: {DescribeAuthorMatch(evidence)}");
        builder.AppendLine($"  Other authors listed: {DescribeList(evidence.OtherAuthorNames)}");
        builder.AppendLine($"  Matched keywords: {DescribeList(evidence.MatchedKeywords)}");
        builder.AppendLine($"  Year from query matched: {(evidence.YearMatched ? "yes" : "no")}");
        builder.AppendLine($"  Tier: {candidate.Tier}");

        return builder.ToString();
    }

    private static string DescribeAuthorMatch(MatchEvidence evidence) => evidence.AuthorPosition switch
    {
        null => "no author matched the query",
        0 => $"{evidence.MatchedAuthorName} is the primary author",
        _ => $"{evidence.MatchedAuthorName} is a listed contributor, not the primary author"
    };

    private static string DescribeYear(int? year) => year?.ToString() ?? "unknown";

    private static string DescribeList(List<string> values) =>
        values.Count > 0 ? string.Join(", ", values) : "none";

    private static string BuildFallbackExplanation(BookCandidate candidate)
    {
        var evidence = candidate.Evidence;

        var parts = new List<string>
        {
            evidence.TitleMatchKind switch
            {
                TitleMatchKind.Exact => "The title matches your search exactly",
                TitleMatchKind.Prefix => "The title starts with your search",
                TitleMatchKind.Contains => "The title contains your search",
                _ => "This is a broader match on your search"
            }
        };

        if (evidence.AuthorPosition == 0)
        {
            parts.Add($"{evidence.MatchedAuthorName} is the primary author");
        }
        else if (evidence.AuthorPosition > 0)
        {
            parts.Add($"{evidence.MatchedAuthorName} is a listed contributor");
        }

        if (evidence.YearMatched)
        {
            parts.Add($"it was first published in {candidate.FirstPublishYear}");
        }

        if (evidence.MatchedKeywords.Count > 0)
        {
            parts.Add($"it matches {string.Join(" and ", evidence.MatchedKeywords)}");
        }

        return string.Join("; ", parts) + ".";
    }

    private class ExplanationItem
    {
        public int Index { get; set; }

        public string? Explanation { get; set; }
    }
}
