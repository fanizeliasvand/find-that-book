namespace FindThatBook.Api.Models;

// Lower value = better match. Sorting relies on these numbers.
public enum MatchTier
{
    ExactTitlePrimaryAuthor = 1,
    ExactTitleContributorAuthor = 2,
    TitleOnlyNoAuthorGiven = 3,
    NearTitleWithAuthor = 4,
    AuthorOnlyNoTitleGiven = 5,
    Weak = 6
}
