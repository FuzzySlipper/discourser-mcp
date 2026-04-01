using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Discourser.Core.Connectors.Reddit;

/// <summary>
/// Client for the Arctic Shift community API — historical Reddit search fallback.
/// Returns the same typed intermediate data as RedditApiClient.
/// </summary>
public sealed class ArcticShiftClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<ArcticShiftClient> _logger;

    public ArcticShiftClient(
        HttpClient httpClient,
        string baseUrl,
        ILogger<ArcticShiftClient> logger)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _logger = logger;
    }

    public async Task<List<RedditPostData>> SearchAsync(
        string query,
        string? subreddit,
        DateTime? dateFrom,
        DateTime? dateTo,
        int limit,
        CancellationToken ct = default)
    {
        var qs = $"q={Uri.EscapeDataString(query)}&limit={limit}";
        if (subreddit is not null)
            qs += $"&subreddit={Uri.EscapeDataString(subreddit)}";
        if (dateFrom.HasValue)
            qs += $"&after={new DateTimeOffset(dateFrom.Value).ToUnixTimeSeconds()}";
        if (dateTo.HasValue)
            qs += $"&before={new DateTimeOffset(dateTo.Value).ToUnixTimeSeconds()}";

        var url = $"{_baseUrl}/api/posts/search?{qs}";

        _logger.LogDebug("Arctic Shift search: {Url}", url);

        try
        {
            var json = await _httpClient.GetFromJsonAsync<JsonElement>(url, ct);
            return ParseResponse(json);
        }
        catch (HttpRequestException ex)
        {
            throw new UpstreamApiException("arctic-shift", ex.Message, ex);
        }
    }

    private static List<RedditPostData> ParseResponse(JsonElement json)
    {
        var posts = new List<RedditPostData>();

        // Arctic Shift returns { "data": [ ... ] }
        var items = json.TryGetProperty("data", out var data)
            ? data
            : json;

        if (items.ValueKind != JsonValueKind.Array)
            return posts;

        foreach (var item in items.EnumerateArray())
        {
            posts.Add(new RedditPostData
            {
                Title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                SelfText = item.TryGetProperty("selftext", out var st) ? st.GetString() ?? "" : "",
                Score = item.TryGetProperty("score", out var s) ? s.GetInt32() : 0,
                Author = item.TryGetProperty("author", out var a) ? a.GetString() ?? "[deleted]" : "[deleted]",
                Permalink = item.TryGetProperty("permalink", out var p) ? p.GetString() ?? "" : "",
                CreatedUtc = item.TryGetProperty("created_utc", out var c) ? c.GetDouble() : 0,
                NumComments = item.TryGetProperty("num_comments", out var nc) ? nc.GetInt32() : 0,
                Subreddit = item.TryGetProperty("subreddit", out var sub) ? sub.GetString() ?? "" : ""
            });
        }

        return posts;
    }
}
