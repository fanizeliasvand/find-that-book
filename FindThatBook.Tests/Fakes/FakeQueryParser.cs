using FindThatBook.Api.Models;
using FindThatBook.Api.Services;

namespace FindThatBook.Tests.Fakes;

public class FakeQueryParser : IQueryParser
{
    private readonly QueryInterpretation _interpretation;

    public FakeQueryParser(QueryInterpretation interpretation) => _interpretation = interpretation;

    public Task<QueryInterpretation> ParseAsync(string rawQuery, CancellationToken ct) =>
        Task.FromResult(_interpretation);
}
