using FindThatBook.Api.Models;

namespace FindThatBook.Api.Services;

public interface IBookMatcher
{
    List<BookCandidate> Rank(List<OpenLibraryWork> works, QueryInterpretation interpretation);
}
