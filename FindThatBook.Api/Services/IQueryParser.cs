using FindThatBook.Api.Models;

namespace FindThatBook.Api.Services;

public interface IQueryParser
{
    // Never throws. Falls back to the raw query if extraction fails.
    Task<QueryInterpretation> ParseAsync(string rawQuery, CancellationToken ct);
}
