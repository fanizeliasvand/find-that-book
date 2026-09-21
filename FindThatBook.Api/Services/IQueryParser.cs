using FindThatBook.Api.Models;

namespace FindThatBook.Api.Services;

public interface IQueryParser
{
    // Falls back to the raw query if extraction fails; only throws if the caller cancels.
    Task<QueryInterpretation> ParseAsync(string rawQuery, CancellationToken ct);
}
