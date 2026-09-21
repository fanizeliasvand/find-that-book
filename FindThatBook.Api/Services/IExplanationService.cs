using FindThatBook.Api.Models;

namespace FindThatBook.Api.Services;

public interface IExplanationService
{
    // Fills each candidate's Explanation in place. Never leaves one empty; only throws if the caller cancels.
    Task ApplyExplanationsAsync(
        List<BookCandidate> candidates,
        QueryInterpretation interpretation,
        CancellationToken ct);
}
