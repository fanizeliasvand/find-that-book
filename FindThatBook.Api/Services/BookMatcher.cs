using System.Globalization;
using System.Text;
using FindThatBook.Api.Models;

namespace FindThatBook.Api.Services;

public class BookMatcher : IBookMatcher
{
    private static readonly string[] LeadingArticles = ["the ", "a ", "an "];

    public List<BookCandidate> Rank(List<OpenLibraryWork> works, QueryInterpretation interpretation)
    {
        var queryTitle = Normalize(interpretation.Title);
        var queryAuthor = NormalizeAuthor(interpretation.Author);

        return DeduplicateByTitleAndAuthor(DeduplicateByKey(works))
            .Select(work => BuildCandidate(work, interpretation, queryTitle, queryAuthor))
            .OrderBy(candidate => candidate.Tier)
            .ThenByDescending(candidate => candidate.Evidence.YearMatched)
            .ThenByDescending(candidate => candidate.Evidence.MatchedKeywords.Count)
            .ThenByDescending(candidate => candidate.EditionCount)
            .ToList();
    }

    public static string Normalize(string? value) => DropLeadingArticle(NormalizeCore(value));

    public static string NormalizeAuthor(string? value) => NormalizeCore(value);

    private static string NormalizeCore(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // FormD splits "é" into "e" plus a combining mark, so dropping the marks strips the accent.
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else if (ch is '\'' or '’')
            {
                // Apostrophes vanish rather than split: "hitchhiker's" -> "hitchhikers".
                continue;
            }
            else if (builder.Length > 0 && builder[^1] != ' ')
            {
                // Any other punctuation becomes a separator, collapsed as we go.
                builder.Append(' ');
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string DropLeadingArticle(string normalized)
    {
        foreach (var article in LeadingArticles)
        {
            if (normalized.StartsWith(article, StringComparison.Ordinal))
            {
                return normalized[article.Length..];
            }
        }

        return normalized;
    }

    // Pass one: the same work key returned more than once in a single response.
    private static List<OpenLibraryWork> DeduplicateByKey(List<OpenLibraryWork> works)
    {
        return works
            .Where(work => !string.IsNullOrWhiteSpace(work.Key))
            .GroupBy(work => work.Key, StringComparer.Ordinal)
            .Select(group => new OpenLibraryWork
            {
                Key = group.Key,
                Title = group.First().Title,
                AuthorNames = group.First().AuthorNames,
                FirstPublishYear = group.Min(work => work.FirstPublishYear),
                CoverId = group.Select(work => work.CoverId).FirstOrDefault(id => id is not null),
                EditionCount = group.Max(work => work.EditionCount)
            })
            .ToList();
    }

    // Pass two: genuinely distinct work keys that describe the same book, which
    // Open Library does carry. The widest edition count wins as the canonical record.
    private static List<OpenLibraryWork> DeduplicateByTitleAndAuthor(List<OpenLibraryWork> works)
    {
        return works
            .GroupBy(work => (
                Title: Normalize(work.Title),
                // No authors groups on title alone rather than throwing.
                Author: NormalizeAuthor(work.AuthorNames.FirstOrDefault())))
            .Select(group =>
            {
                var canonical = group.MaxBy(work => work.EditionCount)!;

                return new OpenLibraryWork
                {
                    Key = canonical.Key,
                    Title = canonical.Title,
                    AuthorNames = canonical.AuthorNames,
                    EditionCount = canonical.EditionCount,
                    FirstPublishYear = group.Min(work => work.FirstPublishYear),
                    CoverId = group.Select(work => work.CoverId).FirstOrDefault(id => id is not null)
                };
            })
            .ToList();
    }

    private static BookCandidate BuildCandidate(
        OpenLibraryWork work,
        QueryInterpretation interpretation,
        string queryTitle,
        string queryAuthor)
    {
        var normalizedTitle = Normalize(work.Title);
        var titleMatch = MatchTitle(normalizedTitle, queryTitle);
        var authorIndex = FindAuthorIndex(work.AuthorNames, queryAuthor);

        var evidence = new MatchEvidence
        {
            TitleMatchKind = titleMatch,
            AuthorPosition = authorIndex,
            MatchedAuthorName = authorIndex is not null ? work.AuthorNames[authorIndex.Value] : null,
            OtherAuthorNames = work.AuthorNames.Where((_, index) => index != authorIndex).ToList()
        };

        ApplyKeywords(interpretation.Keywords, work.FirstPublishYear, normalizedTitle, evidence);

        return new BookCandidate
        {
            Title = work.Title,
            PrimaryAuthor = work.AuthorNames.FirstOrDefault(),
            Contributors = work.AuthorNames.Skip(1).ToList(),
            FirstPublishYear = work.FirstPublishYear,
            EditionCount = work.EditionCount,
            OpenLibraryUrl = $"https://openlibrary.org{work.Key}",
            CoverUrl = work.CoverId is null
                ? null
                : $"https://covers.openlibrary.org/b/id/{work.CoverId}-M.jpg",
            Tier = DetermineTier(titleMatch, authorIndex, queryTitle.Length > 0, queryAuthor.Length > 0),
            Evidence = evidence
        };
    }

    private static TitleMatchKind MatchTitle(string candidateTitle, string queryTitle)
    {
        if (queryTitle.Length == 0 || candidateTitle.Length == 0)
        {
            return TitleMatchKind.None;
        }

        if (candidateTitle == queryTitle)
        {
            return TitleMatchKind.Exact;
        }

        // Prefix covers subtitles: "the hobbit an illustrated edition" for "the hobbit".
        if (candidateTitle.StartsWith(queryTitle, StringComparison.Ordinal))
        {
            return TitleMatchKind.Prefix;
        }

        return candidateTitle.Contains(queryTitle, StringComparison.Ordinal)
            ? TitleMatchKind.Contains
            : TitleMatchKind.None;
    }

    private static int? FindAuthorIndex(List<string> authorNames, string queryAuthor)
    {
        if (queryAuthor.Length == 0)
        {
            return null;
        }

        for (var index = 0; index < authorNames.Count; index++)
        {
            var candidate = NormalizeAuthor(authorNames[index]);
            if (candidate.Length == 0)
            {
                continue;
            }

            // Substring either way so a surname-only query still matches "J. R. R. Tolkien".
            if (candidate.Contains(queryAuthor, StringComparison.Ordinal) ||
                queryAuthor.Contains(candidate, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return null;
    }

    private static void ApplyKeywords(
        List<string> keywords,
        int? firstPublishYear,
        string normalizedTitle,
        MatchEvidence evidence)
    {
        foreach (var keyword in keywords)
        {
            var normalizedKeyword = Normalize(keyword);
            if (normalizedKeyword.Length == 0)
            {
                continue;
            }

            if (normalizedKeyword.Length == 4 && normalizedKeyword.All(char.IsAsciiDigit))
            {
                if (firstPublishYear is not null &&
                    int.Parse(normalizedKeyword, CultureInfo.InvariantCulture) == firstPublishYear)
                {
                    evidence.YearMatched = true;
                }

                // A year is never also a title keyword.
                continue;
            }

            if (normalizedTitle.Contains(normalizedKeyword, StringComparison.Ordinal))
            {
                evidence.MatchedKeywords.Add(keyword);
            }
        }
    }

    private static MatchTier DetermineTier(
        TitleMatchKind titleMatch,
        int? authorPosition,
        bool titleGiven,
        bool authorGiven)
    {
        if (titleMatch == TitleMatchKind.Exact && authorPosition == 0)
        {
            return MatchTier.ExactTitlePrimaryAuthor;
        }

        if (titleMatch == TitleMatchKind.Exact && authorPosition > 0)
        {
            return MatchTier.ExactTitleContributorAuthor;
        }

        // Prefix counts here because a subtitle is still the book they asked for.
        if (!authorGiven && titleMatch is TitleMatchKind.Exact or TitleMatchKind.Prefix)
        {
            return MatchTier.TitleOnlyNoAuthorGiven;
        }

        if (authorPosition is not null && titleMatch is TitleMatchKind.Prefix or TitleMatchKind.Contains)
        {
            return MatchTier.NearTitleWithAuthor;
        }

        if (!titleGiven && authorPosition is not null)
        {
            return MatchTier.AuthorOnlyNoTitleGiven;
        }

        return MatchTier.Weak;
    }
}
