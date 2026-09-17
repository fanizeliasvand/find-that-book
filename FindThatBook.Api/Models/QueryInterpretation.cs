namespace FindThatBook.Api.Models;

public class QueryInterpretation
{
    public string? Title { get; set; }

    public string? Author { get; set; }

    public List<string> Keywords { get; set; } = new();
}
