namespace FindThatBook.Api.Models;

// Trimmed to the fields we actually use.
public class OpenLibraryWork
{
    // Open Library's stable identifier for this book, e.g. "/works/OL262758W".
    public required string Key { get; set; }

    public required string Title { get; set; }

    // Order matters: index 0 is the primary author.
    public List<string> AuthorNames { get; set; } = new();

    public int? FirstPublishYear { get; set; }

    // Id used to build a cover image URL.
    public int? CoverId { get; set; }

    public int EditionCount { get; set; }
}
