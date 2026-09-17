using System.Text.Json;
using System.Text.Json.Serialization;
using FindThatBook.Api.Models;

namespace FindThatBook.Api.Clients;

// Fetching only - no matching or ranking logic belongs in here.
public class OpenLibraryClient : IOpenLibraryClient
{
    private readonly HttpClient _httpClient;

    public OpenLibraryClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<OpenLibraryWork>> SearchAsync(string? title, string? author, CancellationToken ct)
    {
        // Open Library takes title/author as separate query params rather than
        // one free-text field, so we build the query string ourselves.
        var queryParams = new List<string>();

        if (!string.IsNullOrWhiteSpace(title))
        {
            queryParams.Add($"title={Uri.EscapeDataString(title)}");
        }

        if (!string.IsNullOrWhiteSpace(author))
        {
            queryParams.Add($"author={Uri.EscapeDataString(author)}");
        }

        queryParams.Add("fields=key,title,author_name,first_publish_year,cover_i,edition_count");
        queryParams.Add("limit=20");

        var requestUri = $"https://openlibrary.org/search.json?{string.Join("&", queryParams)}";

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
