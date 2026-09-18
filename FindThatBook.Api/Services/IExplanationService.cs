using FindThatBook.Api.Models;

namespace FindThatBook.Api.Services;

public interface IExplanationService
{
    // Fills each candidate's Explanation in place. Never throws, never
    // leaves an explanation empty.
    Task ApplyExplanationsAsync(
        List<BookCandidate> candidates,
        QueryInterpretation interpretation,
        CancellationToken ct);
}
