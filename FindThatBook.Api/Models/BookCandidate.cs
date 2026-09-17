namespace FindThatBook.Api.Models;

public class BookCandidate
{
    public required string Title { get; set; }

    public string? PrimaryAuthor { get; set; }

    public List<string> Contributors { get; set; } = new();

    public int? FirstPublishYear { get; set; }

    public int EditionCount { get; set; }

    public required string OpenLibraryUrl { get; set; }

    public string? CoverUrl { get; set; }

    public MatchTier Tier { get; set; }

    public MatchEvidence Evidence { get; set; } = new();

    // Filled in later by ExplanationService.
    public string Explanation { get; set; } = string.Empty;
}
