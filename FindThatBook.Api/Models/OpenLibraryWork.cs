namespace FindThatBook.Api.Models;

// A single "work" (book) as returned by the Open Library search API,
// trimmed down to the fields we actually use.
public class OpenLibraryWork
{
    // Open Library's stable identifier for this book, e.g. "/works/OL262758W".
    public required string Key { get; set; }

    public required string Title { get; set; }

    // Order matters here - index 0 is the primary/first-listed author.
    public List<string> AuthorNames { get; set; } = new();

    public int? FirstPublishYear { get; set; }

    // Id used to build a cover image URL later; null if no cover is known.
    public int? CoverId { get; set; }

    public int EditionCount { get; set; }
}
