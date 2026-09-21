using System.Text.Json;
using System.Text.Json.Serialization;
using FindThatBook.Api.Models;

namespace FindThatBook.Api.Clients;

// Fetching only: no matching or ranking logic belongs here.
public class OpenLibraryClient : IOpenLibraryClient
{
    private static readonly string[] CommonParams =
    [
        "fields=key,title,author_name,first_publish_year,cover_i,edition_count",
        "limit=20"
    ];

    private readonly HttpClient _httpClient;

    public OpenLibraryClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<List<OpenLibraryWork>> SearchAsync(string? title, string? author, CancellationToken ct)
    {
        // Open Library takes title and author as separate params, not one free-text field.
        var queryParams = new List<string>();

        if (!string.IsNullOrWhiteSpace(title))
        {
            queryParams.Add($"title={Uri.EscapeDataString(title)}");
        }

        if (!string.IsNullOrWhiteSpace(author))
        {
            queryParams.Add($"author={Uri.EscapeDataString(author)}");
        }

        return FetchAsync(queryParams, ct);
    }

    public Task<List<OpenLibraryWork>> SearchRawAsync(string query, CancellationToken ct) =>
        FetchAsync([$"q={Uri.EscapeDataString(query)}"], ct);

    private async Task<List<OpenLibraryWork>> FetchAsync(IEnumerable<string> queryParams, CancellationToken ct)
    {
        var requestUri = $"https://openlibrary.org/search.json?{string.Join("&", queryParams.Concat(CommonParams))}";

        var response = await _httpClient.GetAsync(requestUri, ct);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(ct);
        var searchResponse = await JsonSerializer.DeserializeAsync<OpenLibrarySearchResponse>(stream, cancellationToken: ct);

        if (searchResponse?.Docs is null)
        {
            return new List<OpenLibraryWork>();
        }

        // Mapping here keeps Open Library's field names from leaking past this class.
        return searchResponse.Docs
            .Select(doc => new OpenLibraryWork
            {
                Key = doc.Key ?? string.Empty,
                Title = doc.Title ?? string.Empty,
                AuthorNames = doc.AuthorName ?? new List<string>(),
                FirstPublishYear = doc.FirstPublishYear,
                CoverId = doc.CoverI,
                EditionCount = doc.EditionCount ?? 0
            })
            .ToList();
    }

    // Mirrors Open Library's raw JSON shape. Never leaves this class.
    private class OpenLibrarySearchResponse
    {
        [JsonPropertyName("docs")]
        public List<OpenLibraryDoc>? Docs { get; set; }
    }

    private class OpenLibraryDoc
    {
        [JsonPropertyName("key")]
        public string? Key { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("author_name")]
        public List<string>? AuthorName { get; set; }

        [JsonPropertyName("first_publish_year")]
        public int? FirstPublishYear { get; set; }

        [JsonPropertyName("cover_i")]
        public int? CoverI { get; set; }

        [JsonPropertyName("edition_count")]
        public int? EditionCount { get; set; }
    }
}
