using FindThatBook.Api.Services;

namespace FindThatBook.Tests;

public class BookMatcherNormalizeTests
{
    [Fact]
    public void Normalize_Lowercases()
    {
        var input = "MOBY DICK";

        var result = BookMatcher.Normalize(input);

        Assert.Equal("moby dick", result);
    }

    [Fact]
    public void Normalize_StripsLeadingThe()
    {
        var input = "The Hobbit";

        var result = BookMatcher.Normalize(input);

        Assert.Equal("hobbit", result);
    }

    [Fact]
    public void Normalize_StripsPunctuation()
    {
        var input = "The Hobbit, or There and Back Again";

        var result = BookMatcher.Normalize(input);

        Assert.Equal("hobbit or there and back again", result);
    }

    [Fact]
    public void Normalize_StripsDiacritics()
    {
        var input = "Café";

        var result = BookMatcher.Normalize(input);

        Assert.Equal("cafe", result);
    }

    [Fact]
    public void Normalize_RemovesApostrophesWithoutSplittingTheWord()
    {
        var input = "Hitchhiker's";

        var result = BookMatcher.Normalize(input);

        Assert.Equal("hitchhikers", result);
    }

    [Fact]
    public void Normalize_CollapsesRepeatedSpaces()
    {
        var input = "Moby    Dick";

        var result = BookMatcher.Normalize(input);

        Assert.Equal("moby dick", result);
    }

    [Fact]
    public void Normalize_ReturnsEmptyForNull()
    {
        string? input = null;

        var result = BookMatcher.Normalize(input);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Normalize_ReturnsEmptyForWhitespace()
    {
        var input = "   ";

        var result = BookMatcher.Normalize(input);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void NormalizeAuthor_KeepsLeadingArticle()
    {
        var input = "A. A. Milne";

        var result = BookMatcher.NormalizeAuthor(input);

        Assert.Equal("a a milne", result);
    }

    [Fact]
    public void NormalizeAuthor_DiffersFromNormalizeOnALeadingArticle()
    {
        var input = "A. A. Milne";

        var author = BookMatcher.NormalizeAuthor(input);
        var title = BookMatcher.Normalize(input);

        Assert.Equal("a a milne", author);
        Assert.Equal("a milne", title);
    }
}
