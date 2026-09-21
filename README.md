# Find That Book

## Live demo

**https://findthatbook-faniz.vercel.app**

The API runs on Render's free tier, which sleeps after about 15 minutes idle. The page pings `/health` on load to wake it while you type, so the first search is usually fast; if it's been idle, allow up to 50 seconds. The front end is on Vercel; the API is deployed from `FindThatBook.Api/Dockerfile`, with the key and allowed origin set as environment variables (`Gemini__ApiKey`, `Cors__AllowedOrigins__0`).

## What it does

Find That Book takes a messy plain-text book description, like `tolkien hobbit illustrated deluxe 1937`, and returns a short ranked list of matches. Gemini extracts a title, author and keywords; those drive an Open Library search; and our own matching hierarchy re-ranks the results. Gemini then writes a one-sentence explanation for each, using only match facts our code supplies.

## Where I went deep

A working end-to-end app is the baseline, so I picked three areas to take further and deliberately left others thin.

**Matching and normalization.** The ranking is the part a user actually feels, and it's the part with no API to lean on. It's pure functions over plain data, which is why it carries the most tests: normalization, six tiers, two de-duplication passes, tiebreaks.

**LLM integration.** The model parses and writes prose; code decides every tier and sort order, so the model can only influence the outcome through what it extracts. Everything around it assumes it will misbehave — fences, prose, malformed JSON, nulls, skipped indexes, timeouts — and every one of those paths has a fallback and a test. The latency work (a 22s search down to ~2.5s) came out of the same scrutiny.

**Testing.** 62 tests, no mocking library, and the subtle ones were mutation-checked by breaking the code on purpose to confirm the right test failed. That's how I found the cancellation bug described below.

**Left thin on purpose:** no auth, no caching, no persistence, no rate limiting, and no tests for the controller, the Open Library client or the front end. Those are listed under [next steps](#next-steps) rather than half-built.

## Setup and running

Prerequisites:

- .NET 10 SDK
- Node.js 20.19+ or 22.12+ (required by Vite 8)
- A Gemini API key from Google AI Studio

Set the key with user secrets (the project already has a `UserSecretsId`, so there's no `init` step):

```bash
dotnet user-secrets set "Gemini:ApiKey" "<your-key>" --project FindThatBook.Api
```

User secrets load only in Development, which the launch profile uses. `appsettings.Development.json` is gitignored and also works, but user secrets keep the key out of the project directory.

API, on **http://localhost:5216**:

```bash
dotnet run --project FindThatBook.Api --launch-profile http
```

Front end, on **http://localhost:5173**:

```bash
cd findthatbook-web
npm install
npm run dev
```

The front end calls `http://localhost:5216` by default (override with `VITE_API_BASE_URL`). CORS allows only the origins in `Cors:AllowedOrigins`, which defaults to `http://localhost:5173` in `appsettings.json`, so if 5173 is taken and Vite picks another port, the browser blocks requests. An `https` profile runs on https://localhost:7259, but the front end and CORS expect http.

Tests, from the repo root:

```bash
dotnet test
```

## How I approached it

I built the Open Library and Gemini clients first, since their response shapes set the data model, then the matching logic, query parser, orchestration and explanation services, and the React page.

Next time I'd start with a thin end-to-end slice through every layer. Two problems only appeared once everything was connected: the Gemini model I'd targeted wasn't available to new keys, and my first fallback returned nothing. A skeleton would've caught both early. Tests also came after the code rather than alongside it.

## The matching hierarchy

Each candidate gets one tier; lower is better.

| Tier | Label | Meaning |
|---|---|---|
| 1 | Exact match (`ExactTitlePrimaryAuthor`) | Exact title; author listed first |
| 2 | Title match · credited author (`ExactTitleContributorAuthor`) | Exact title; author listed, not first |
| 3 | Title match (`TitleOnlyNoAuthorGiven`) | No author given; exact or prefix title |
| 4 | Close match (`NearTitleWithAuthor`) | Author at any position; prefix or substring title |
| 5 | By this author (`AuthorOnlyNoTitleGiven`) | No title given; author matches |
| 6 | Broad match (`Weak`) | Anything else, including an exact title without the requested author |

Tiers 3 and 5 only occur when the query lacks an author or title, so they never compete with 1, 2 and 4 in one list; their number matters more for the badge than for sorting.

**Primary vs contributor.** `author_name` is ordered: index 0 is the primary author, later entries are illustrators, editors, translators and adapters. Searching Tolkien's *The Hobbit* also returns the 1990 graphic-novel adaptation, which lists Charles Dixon first and Tolkien second: same title, correct author, not the book most people mean. Position is the only signal separating them, so it splits tier 1 from tier 2 and feeds the explanation evidence.

**Where the tier order costs us.** For `mark huckleberry`, Open Library has a 2007 *Huckleberry Finn* with Henry Brook as primary author and Twain as contributor (1 edition). Exact title, so tier 2. Twain's own *Adventures of Huckleberry Finn* (2,623 editions) only matches as a substring, so tier 4, and the adaptation wins. The tier order is the brief's, and it's right far more often than not — it's the same rule that puts the real *The Hobbit* above the Dixon graphic novel. The fix is a different signal, not a different order: an edition count three orders of magnitude larger is a strong hint of the canonical work.

**Normalization.** Titles and authors are lowercased; stripped of diacritics by decomposing to Unicode FormD and dropping combining marks (`Café` → `cafe`); stripped of apostrophes without splitting words (`Hitchhiker's` → `hitchhikers`); and have other punctuation turned into word breaks, with whitespace collapsed (`The Hobbit, or There and Back Again` → `the hobbit or there and back again`). Titles also lose a leading `the`, `a` or `an`. Authors keep it, or `A. A. Milne` would become `a milne` instead of `a a milne`.

**Exact, then prefix, then contains.** Exact means equal after normalization. A prefix match is almost always the same book with a subtitle or edition suffix (`hobbit` against *The Hobbit, or There and Back Again*). A substring match catches related but usually different books, like companions and commentaries such as *The Annotated Hobbit*. With no author given, a prefix still counts as tier 3, since a subtitle doesn't change the book.

**Keywords.** Open Library's fielded search has no parameter for words like `illustrated` or `deluxe`, and putting them in the title breaks it: the full messy string as a title returns zero results. So on the normal path they never reach Open Library. Afterwards, a four-digit keyword is compared with `first_publish_year` to set a year-match flag, and other keywords count if they appear in the normalized title. Within a tier, results sort by year match, then keyword count, then edition count. In practice the year does the work; edition words rarely appear in work titles.

## Assumptions and trade-offs

**Open Library as a candidate pool.** Its relevance order is tuned for general search and knows nothing about author position or our tiers, so we take its top 20 and discard the order. A correct book outside the top 20 can't be found; the cap keeps responses small and fast.

**One request, not N+1.** The brief points at `/works/{id}.json`, `/authors/{id}.json` and `/authors/{id}/works.json`, but `search.json` with `fields=key,title,author_name,first_publish_year,cover_i,edition_count` already returns everything the tiers and the cards need, so a search is one HTTP call instead of one plus a detail call per candidate. The detail endpoints would be worth it for data those fields don't carry, like subjects or an author's canonical name for disambiguation.

**Two de-duplication passes.** The first, by work key, is what the brief asks for: repeated keys merge, keeping the earliest `first_publish_year`, highest edition count and first cover. The second catches distinct work records for one book, like a 1937 *The Hobbit* with 481 editions and a 2026 one with 2, both by J.R.R. Tolkien. Records sharing a normalized title and primary author merge; the most-editioned one supplies the link, and the group keeps its earliest year and any cover. I confirmed this was the preferred behavior. Same-titled books by different authors stay apart, and records with no work key are dropped, having nothing to link to.

**Loose author matching.** A candidate matches when every word of the normalized query author is in its name, so `ursula le guin` matches `Ursula K. Le Guin` and `tolkien` matches `J.R.R. Tolkien` (`j r r tolkien`). It replaced a substring test that broke whenever Gemini dropped a middle initial, which happened intermittently. It's loose on purpose: a wrong match still gets ranked and explained; a missed one disappears. The costs: it's one-directional, so `j r r tolkien` won't match a record listing only `Tolkien` (records usually carry the fuller name), and a common surname matches everyone sharing it.

**A tier for title-only queries.** No author is absence of evidence, not evidence against. Scoring it like a wrong author would push every correct result to `Weak`; tier 3 keeps them real results.

**Private DTOs at both client boundaries.** Each client deserializes into private classes mirroring the external JSON (`author_name` and `cover_i`; Gemini's nested `candidates`/`content`/`parts`) and maps them into our models, settling nullability there: a missing author list becomes empty, a missing edition count zero. An API field rename changes one file.

**The LLM parses and explains; code decides.** Gemini extracts query fields and writes explanations; tiers and sorting are code, so the model only affects the outcome through what it extracts. The explanation prompt carries only each candidate's match evidence and forbids adding plot, genre, awards or publication details. That held: Open Library dates *The Great Gatsby* to 1920 (it was 1925), and the explanation repeated 1920 rather than correcting it from the model's own knowledge. That's intended: it matches the year on the card, and data errors surface instead of being papered over.

**Batched and skipped explanations.** All displayed results are explained in one call returning a JSON array, assigned by index rather than array position. A tier-1 match with an exact title and no contributors skips the model and gets a fixed sentence from code. Explanations run only after trimming to five, so nothing's spent on hidden results.

**The API key.** It's read from configuration (`Gemini:ApiKey`), never source (see setup), and sent in the `x-goog-api-key` header rather than a `?key=` parameter, so it stays out of URL logs. A missing or empty key stops the app at startup, before it starts listening, with a message saying how to set it.

## Handling failures and unclear results

**Query parsing.** The parser falls back when Gemini returns nothing, no JSON object, malformed JSON, or a null title and author, or when the call throws or times out. The fallback runs Open Library's general `q=` full-text search on the raw query, not the fielded search. Testing drove that: the first version put the raw string in both title and author, which Open Library ANDs, and got zero results for the example query while `q=` found *The Hobbit*. The response flags the fallback, and the page shows "Showing broader results".

Parsing is defensive regardless: models often wrap the requested bare JSON in markdown fences or a sentence, so the parser keeps only the text between the outermost braces.

**Explanations.** If the call fails, times out or returns something unusable, or the model skips an index, that result gets a sentence built from its match evidence, e.g. "The title matches your search exactly; J.R.R. Tolkien is a listed contributor." No result goes unexplained.

**Retries.** Gemini calls retry on 5xx, connection failures and our own timeout: up to three attempts, with exponential backoff (about 200ms, then 400ms) plus up to 100ms of jitter so concurrent requests don't retry in lockstep, each with a 10-second timeout. 4xx is never retried, since a bad key or request fails the same way every time; that includes 429 (see next steps).

**Cancellation.** A caller cancellation stops the request; our own timeout falls back like any failure. Both raise the same exception type, so the code checks the caller's token.

**Error bodies are logged.** On a final failure the client logs the response body before throwing, since `EnsureSuccessStatusCode` discards it and that's where Google explains the problem. That's how a retired-model error hid: the log said only "404", and it took replaying the request by hand to see "this model is no longer available to new users".

**A search that finds nothing widens instead of stopping.** The fielded search ANDs title and author, so one wrong field returns zero rows even when the other is right: `the grate gatsbee fitzgerald` used to return an empty page, because Gemini passed the typos straight through as the title and no record matches it. When the fielded search comes back empty, the search retries with the author alone, and if that's also empty it falls back to the same full-text `q=` search the parser's own fallback uses.

The title is dropped from the interpretation on that retry, not just from the request. If it weren't, the results would be tiered against a title that matched nothing and land in `Weak`, while the page still displayed the title as if it had been searched. Dropping it puts them in `By this author` honestly, and the page reports what actually ran. This is the brief's author-only fallback; it previously only fired when the parser returned no title at all, never when the title was simply wrong.

**Weak results.** Results are trimmed to five with `Weak` matches removed; if nothing's left, the weak ones are shown, because an uncertain result beats an empty page.

**Not handled.** Open Library failures aren't caught: the search returns a 500 and the page shows the HTTP status. If the API is unreachable, the page says so instead of crashing.

## Performance

A search originally took 22 seconds, mostly Gemini 3.6 Flash reasoning before answering, which neither prompt needs: one extracts three fields, the other writes a sentence from given facts. Setting `generationConfig.thinkingConfig.thinkingLevel` to `minimal` cut a warm search to about 2.5s, with parsing down from 3.88s to 0.83–1.08s and explanations from 15.67s to 1.15–1.37s (22.06s total before). Open Library varies from 0.3s to 6.6s; the first run after startup took 9.1s, 6.6s of it Open Library.

The baseline ran on the free tier and the new numbers on paid, since billing was enabled partway through, so I isolated thinking with the same explanation prompt and key, three runs per setting:

| `thinkingLevel` | Average latency | Thought tokens |
|---|---|---|
| not set (default `medium`) | 7.66s | ~1,318 |
| `low` | 4.17s | ~609 |
| `minimal` | 1.57s | 0 |

Thought tokens falling from 1,318 to 609 to zero with latency show the time went on reasoning, not network variance. `thinkingBudget: 0`, the obvious thing to try, is the Gemini 2.5 control, not Gemini 3's. Google's docs say Gemini 3 Flash can't fully disable thinking, yet `minimal` reported zero thought tokens here. The output was still valid JSON with one grounded sentence per book.

## Testing strategy

There are 62 tests.

| File | Tests | Covers |
|---|---|---|
| `BookMatcherNormalizeTests` | 10 | Title and author normalization |
| `BookMatcherRankTests` | 12 | Every tier, subtitle prefixes, author matching, both de-dup passes, tiebreaks |
| `QueryParserTests` | 13 | Extraction, defensive parsing, every fallback trigger, cancellation |
| `GeminiClientTests` | 7 | Retry and no-retry by status, multi-part responses, key placement |
| `SearchServiceTests` | 10 | Search strategy, the widening retries, trimming, weak results, explanation scope |
| `ExplanationServiceTests` | 10 | Skipping the model, batching, index mapping, fallbacks, cancellation |

The matching logic has no I/O, so it's tested directly with plain data. Everything else takes dependencies through interfaces, which tests fill with hand-written fakes; there's no mocking library. That covers LLM behavior you can't reliably trigger live: markdown fences, prose around JSON, malformed JSON, explicit `null`s, missing indexes, exceptions and timeouts. `GeminiClient` uses a stub `HttpMessageHandler` returning queued status codes. The 5xx retry branch never ran live (the only real failures were 429s and a 404), so that stub test is its only evidence.

Subtle tests were mutation-checked: I broke the code on purpose and confirmed the right test failed, and only that one. That covered choosing the highest-edition record in de-duplication, the retry condition, the explicit-null keywords guard, dropping weak results unless nothing else matched, explaining only the trimmed list, skipping the model for unambiguous results, and batching. The batching check was telling: one call per book also broke index-based assignment, because they're the same design decision.

I found a cancellation bug while reviewing that handling. `GeminiClient`'s 10-second timeout surfaces as `TaskCanceledException`, a subclass of `OperationCanceledException` and the same type a caller cancellation raises. `QueryParser` and `ExplanationService` rethrew every one, so a slow Gemini response became a 500 instead of a fallback. The fakes made it reproducible: I wrote tests for both cases first, and the timeout ones failed against the unfixed code. The fix rethrows only when the caller's token was cancelled.

`OpenLibraryClient`, `SearchController` and the React front end have no automated tests. The suite runs in about a second, mostly real retry backoff in the `GeminiClient` tests.

## Next steps

- **Caching**, in memory for one instance, Redis across several. Repeat queries are common, and caching both Gemini calls would cut their latency to near zero and reduce API spend.
- **Return results before explanations.** Explanations are the slowest remaining step; books could render as soon as ranking finishes, with explanations filling in after.
- **Honor the retry delay on 429** instead of never retrying. Gemini's 429 says how long to wait, which separates a per-minute limit that clears in seconds from an exhausted daily quota that doesn't. Today both are treated as permanent.
- **Controller-level tests.** `SearchController`'s 400 on an empty query, model binding and DI wiring are untested; a `WebApplicationFactory` test with fakes swapped in would cover the HTTP contract the front end relies on.
- **Rate limiting and a query length cap.** The demo endpoint is public and every search spends two Gemini calls against a billed key, so anyone can run up the bill. `AddRateLimiter` with a fixed window per IP, plus a `[MaxLength]` on the query, would bound both the cost and the size of what reaches the prompt.
