namespace FindThatBook.Api.Models;

public enum TitleMatchKind
{
    // None is 0 so an unset MatchEvidence defaults to "no match", not "exact".
    None = 0,
    Exact = 1,
    Prefix = 2,
    Contains = 3
}

// Concrete facts behind a candidate's tier, so explanations can cite them.
public class MatchEvidence
{
    public TitleMatchKind TitleMatchKind { get; set; }

    // 0 = primary author, higher = contributor, null = no author match.
    public int? AuthorPosition { get; set; }

    public string? MatchedAuthorName { get; set; }

    public List<string> OtherAuthorNames { get; set; } = new();

    public List<string> MatchedKeywords { get; set; } = new();

    public bool YearMatched { get; set; }
}
