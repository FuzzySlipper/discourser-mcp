using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Discourser.Core.Connectors.Reddit;

/// <summary>
/// Typed client for the official Reddit OAuth API.
/// Handles token lifecycle, search, and thread fetching.
/// Returns typed intermediate data — no JSON/MCP error payloads.
/// </summary>
public sealed class RedditApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _userAgent;
    private readonly int _maxRetries;
    private readonly ILogger<RedditApiClient> _logger;

    private string? _accessToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public RedditApiClient(
        HttpClient httpClient,
        string clientId,
        string clientSecret,
        string userAgent,
        int maxRetries,
        ILogger<RedditApiClient> logger)
    {
        _httpClient = httpClient;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _userAgent = userAgent;
        _maxRetries = maxRetries;
        _logger = logger;
    }

    public async Task<List<RedditPostData>> SearchAsync(
        string query,
        string? subreddit,
        string? timeWindow,
        int limit,
        CancellationToken ct = default)
    {
        await EnsureTokenAsync(ct);

        var path = subreddit is not null
            ? $"r/{subreddit}/search"
            : "search";

        var qs = $"q={Uri.EscapeDataString(query)}&sort=relevance&limit={limit}&raw_json=1&type=link";
        if (timeWindow is not null)
            qs += $"&t={timeWindow}";
        if (subreddit is not null)
            qs += "&restrict_sr=on";

        var url = $"https://oauth.reddit.com/{path}?{qs}";
        var json = await GetWithRetriesAsync(url, ct);

        return ParseSearchListing(json);
    }

    public async Task<RedditThreadData> FetchThreadAsync(
        string subreddit, string threadId, CancellationToken ct = default)
    {
        await EnsureTokenAsync(ct);

        var url = $"https://oauth.reddit.com/r/{subreddit}/comments/{threadId}.json?sort=top&raw_json=1";
        var json = await GetWithRetriesAsync(url, ct);

        return ParseThreadResponse(json);
    }

    private async Task<JsonElement> GetWithRetriesAsync(string url, CancellationToken ct)
    {
        for (var attempt = 0; attempt <= _maxRetries; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Headers.UserAgent.ParseAdd(_userAgent);

            using var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && attempt == 0)
            {
                _logger.LogDebug("Reddit returned 401, refreshing token");
                await RefreshTokenAsync(ct);
                continue;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                if (attempt == _maxRetries)
                    throw new RateLimitExceededException("reddit");

                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
                _logger.LogDebug("Reddit rate limited, waiting {Delay}", retryAfter);
                await Task.Delay(retryAfter, ct);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new UpstreamApiException("reddit", (int)response.StatusCode, body);
            }

            var doc = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            return doc;
        }

        throw new RateLimitExceededException("reddit");
    }

    private async Task EnsureTokenAsync(CancellationToken ct)
    {
        if (_accessToken is not null && DateTime.UtcNow < _tokenExpiresAt)
            return;

        await RefreshTokenAsync(ct);
    }

    private async Task RefreshTokenAsync(CancellationToken ct)
    {
        await _tokenLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_accessToken is not null && DateTime.UtcNow < _tokenExpiresAt)
                return;

            using var request = new HttpRequestMessage(HttpMethod.Post,
                "https://www.reddit.com/api/v1/access_token");

            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);
            request.Headers.UserAgent.ParseAdd(_userAgent);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials"
            });

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new UpstreamApiException("reddit-auth", (int)response.StatusCode, body);
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            _accessToken = json.GetProperty("access_token").GetString()!;
            var expiresIn = json.GetProperty("expires_in").GetInt32();
            // Refresh 5 minutes early to avoid edge cases
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 300);

            _logger.LogDebug("Reddit OAuth token acquired, expires at {ExpiresAt}", _tokenExpiresAt);
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>
    /// Maps a date range to Reddit's preset time windows.
    /// Returns the smallest window that contains the range, or null for "all".
    /// </summary>
    public static string? MapToTimeWindow(DateTime? dateFrom, DateTime? dateTo)
    {
        if (dateFrom is null)
            return null; // "all" time

        var age = DateTime.UtcNow - dateFrom.Value;

        return age.TotalHours switch
        {
            <= 1 => "hour",
            <= 24 => "day",
            <= 168 => "week",     // 7 days
            <= 730 => "month",    // ~30 days
            <= 8766 => "year",    // ~365 days
            _ => null             // "all"
        };
    }

    private static List<RedditPostData> ParseSearchListing(JsonElement json)
    {
        var posts = new List<RedditPostData>();

        if (json.TryGetProperty("data", out var data) &&
            data.TryGetProperty("children", out var children))
        {
            foreach (var child in children.EnumerateArray())
            {
                if (child.TryGetProperty("data", out var postData))
                    posts.Add(ParsePostData(postData));
            }
        }

        return posts;
    }

    private static RedditThreadData ParseThreadResponse(JsonElement json)
    {
        // Reddit returns an array: [post_listing, comments_listing]
        if (json.ValueKind != JsonValueKind.Array)
            throw new ConnectorException("Unexpected Reddit thread response format");

        var items = json.EnumerateArray().ToList();
        if (items.Count < 2)
            throw new ConnectorException("Reddit thread response missing comment listing");

        var postListing = items[0];
        var commentListing = items[1];

        // Extract the post
        var postChildren = postListing.GetProperty("data").GetProperty("children");
        var postData = postChildren.EnumerateArray().First().GetProperty("data");
        var post = ParsePostData(postData);

        // Extract comments
        var comments = new List<RedditCommentData>();
        if (commentListing.TryGetProperty("data", out var cData) &&
            cData.TryGetProperty("children", out var cChildren))
        {
            foreach (var child in cChildren.EnumerateArray())
            {
                if (child.TryGetProperty("kind", out var kind) && kind.GetString() == "t1" &&
                    child.TryGetProperty("data", out var commentData))
                {
                    comments.Add(ParseCommentData(commentData));
                }
            }
        }

        return new RedditThreadData(post, comments);
    }

    private static RedditPostData ParsePostData(JsonElement data)
    {
        return new RedditPostData
        {
            Title = data.GetProperty("title").GetString() ?? "",
            SelfText = data.TryGetProperty("selftext", out var st) ? st.GetString() ?? "" : "",
            Score = data.TryGetProperty("score", out var s) ? s.GetInt32() : 0,
            Author = data.TryGetProperty("author", out var a) ? a.GetString() ?? "[deleted]" : "[deleted]",
            Permalink = data.TryGetProperty("permalink", out var p) ? p.GetString() ?? "" : "",
            CreatedUtc = data.TryGetProperty("created_utc", out var c) ? c.GetDouble() : 0,
            NumComments = data.TryGetProperty("num_comments", out var nc) ? nc.GetInt32() : 0,
            Subreddit = data.TryGetProperty("subreddit", out var sub) ? sub.GetString() ?? "" : ""
        };
    }

    private static RedditCommentData ParseCommentData(JsonElement data)
    {
        return new RedditCommentData
        {
            Body = data.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "",
            Score = data.TryGetProperty("score", out var s) ? s.GetInt32() : 0,
            Author = data.TryGetProperty("author", out var a) ? a.GetString() ?? "[deleted]" : "[deleted]",
            Permalink = data.TryGetProperty("permalink", out var p) ? p.GetString() ?? "" : "",
            CreatedUtc = data.TryGetProperty("created_utc", out var c) ? c.GetDouble() : 0
        };
    }
}

// Typed intermediate models — internal to the connector layer

public sealed class RedditPostData
{
    public required string Title { get; init; }
    public required string SelfText { get; init; }
    public required int Score { get; init; }
    public required string Author { get; init; }
    public required string Permalink { get; init; }
    public required double CreatedUtc { get; init; }
    public required int NumComments { get; init; }
    public required string Subreddit { get; init; }

    public DateTime PublishedAt =>
        DateTimeOffset.FromUnixTimeSeconds((long)CreatedUtc).UtcDateTime;

    public string FullUrl =>
        $"https://www.reddit.com{Permalink}";
}

public sealed class RedditCommentData
{
    public required string Body { get; init; }
    public required int Score { get; init; }
    public required string Author { get; init; }
    public required string Permalink { get; init; }
    public required double CreatedUtc { get; init; }

    public string FullUrl =>
        $"https://www.reddit.com{Permalink}";
}

public sealed record RedditThreadData(
    RedditPostData Post,
    List<RedditCommentData> Comments);
