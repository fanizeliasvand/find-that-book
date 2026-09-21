namespace FindThatBook.Api.Models;

public class QueryInterpretation
{
    // Always the user's original text, on both the parsed and fallback paths.
    public string RawQuery { get; set; } = string.Empty;

    public string? Title { get; set; }

    public string? Author { get; set; }

    public List<string> Keywords { get; set; } = new();

    // True when the search fell back to full-text, from failed extraction or an empty fielded search.
    public bool IsFallback { get; set; }
}
