namespace FindThatBook.Api.Models;

public class SearchResponse
{
    // Returned so the UI can show how the query was understood.
    public required QueryInterpretation Interpretation { get; set; }

    public required List<BookCandidate> Results { get; set; }
}
