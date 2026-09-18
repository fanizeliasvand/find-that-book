using FindThatBook.Api.Models;
using FindThatBook.Api.Services;

namespace FindThatBook.Tests.Fakes;

public class FakeExplanationService : IExplanationService
{
    public int CallCount { get; private set; }

    // Snapshot, so a later mutation of the caller's list cannot mask what arrived.
    public List<BookCandidate> ReceivedCandidates { get; private set; } = [];

    public Task ApplyExplanationsAsync(
        List<BookCandidate> candidates,
        QueryInterpretation interpretation,
        CancellationToken ct)
    {
        CallCount++;
        ReceivedCandidates = [.. candidates];

        return Task.CompletedTask;
    }
}
