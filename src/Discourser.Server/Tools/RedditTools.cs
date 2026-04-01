using System.ComponentModel;
using System.Text.Json;
using Discourser.Core.Connectors;
using Discourser.Core.Connectors.Reddit;
using Discourser.Core.Data;
using Discourser.Core.Models;
using ModelContextProtocol.Server;

namespace Discourser.Server.Tools;

[McpServerToolType]
public sealed class RedditTools
{
    [McpServerTool(Name = "search_reddit")]
    [Description("Search Reddit posts. Returns structured search results with source URLs. " +
                 "Results are post metadata (title, score, URL) — use get_reddit_thread to fetch full thread content.")]
    public static async Task<string> SearchReddit(
        IConnector connector,
        ICacheRepository cache,
        DiscourserOptions options,
        [Description("Search query text.")] string query,
        [Description("Limit to a specific subreddit. Omit to search all of Reddit.")] string? subreddit = null,
        [Description("Earliest post date, ISO 8601 (e.g. 2025-01-01).")] string? date_from = null,
        [Description("Latest post date, ISO 8601.")] string? date_to = null,
        [Description("Minimum post score.")] int? min_score = null,
        [Description("Minimum body word count.")] int? min_words = null,
        [Description("Maximum number of results to return.")] int? max_results = null,
        [Description("Bypass cache and fetch fresh data.")] bool force_refresh = false,
        CancellationToken ct = default)
    {
        try
        {
            var searchQuery = new SearchQuery
            {
                Text = query,
                DateFrom = date_from is not null ? DateTime.Parse(date_from) : null,
                DateTo = date_to is not null ? DateTime.Parse(date_to) : null,
                MinScore = min_score,
                MinWords = min_words,
                MaxResults = max_results.HasValue
                    ? Math.Clamp(max_results.Value, 1, options.AbsoluteMaxResults)
                    : null,
                SiteFilters = subreddit is not null
                    ? new Dictionary<string, string> { ["subreddit"] = subreddit }
                    : new()
            };

            if (!force_refresh)
            {
                var cached = await cache.GetSearchResultAsync(connector.SourceName, searchQuery, ct);
                if (cached is not null)
                {
                    var withCache = cached with { FromCache = true };
                    return JsonSerializer.Serialize(withCache, JsonOpts.Default);
                }
            }

            var result = await connector.SearchAsync(searchQuery, ct);

            var ttl = TimeSpan.FromHours(options.CacheTtlSearchHours);
            await cache.SetSearchResultAsync(result, searchQuery, ttl, ct);

            return JsonSerializer.Serialize(result, JsonOpts.Default);
        }
        catch (ConnectorException ex)
        {
            return SerializeError(ex.Message);
        }
    }

    [McpServerTool(Name = "get_reddit_thread")]
    [Description("Fetch a Reddit thread by URL. Stitches the post and top comments into a single document. " +
                 "Comment URLs are preserved in metadata for citation.")]
    public static async Task<string> GetRedditThread(
        RedditApiClient redditApiClient,
        RedditThreadStitcher stitcher,
        ICacheRepository cache,
        DiscourserOptions options,
        [Description("Full reddit.com URL to the thread.")] string url,
        [Description("Minimum comment score to include.")] int? min_comment_score = null,
        [Description("Minimum comment word count to include.")] int? min_comment_words = null,
        [Description("Bypass cache and fetch fresh data.")] bool force_refresh = false,
        CancellationToken ct = default)
    {
        try
        {
            var effectiveMinScore = min_comment_score ?? 1;
            var effectiveMinWords = min_comment_words ?? 1;
            var maxComments = options.DefaultMaxComments;

            // Cache raw thread data by normalized URL, then re-stitch per request
            // with the caller's thresholds. This avoids the bug where different
            // threshold values return the same cached stitched output.
            var normalizedUrl = RedditUrlParser.Normalize(url);

            if (!force_refresh)
            {
                var cached = await cache.GetDocumentAsync(normalizedUrl, ct);
                if (cached is not null && cached.DocType == "raw_thread")
                {
                    // Re-stitch from cached raw data
                    var rawThread = DeserializeRawThread(cached);
                    var restitched = stitcher.Stitch(rawThread,
                        effectiveMinScore, effectiveMinWords, maxComments);
                    return JsonSerializer.Serialize(restitched, JsonOpts.Default);
                }
            }

            var (subreddit, threadId) = RedditUrlParser.Parse(url);
            var threadData = await redditApiClient.FetchThreadAsync(subreddit, threadId, ct);

            // Cache the raw thread data
            var rawDoc = SerializeRawThread(threadData, normalizedUrl);
            var ttl = TimeSpan.FromHours(options.CacheTtlThreadHours);
            await cache.SetDocumentAsync(rawDoc, ttl, ct);

            // Stitch with caller's thresholds
            var stitched = stitcher.Stitch(threadData,
                effectiveMinScore, effectiveMinWords, maxComments);

            return JsonSerializer.Serialize(stitched, JsonOpts.Default);
        }
        catch (ConnectorException ex)
        {
            return SerializeError(ex.Message);
        }
    }

    private static string SerializeError(string message) =>
        JsonSerializer.Serialize(new { error = message }, JsonOpts.Default);

    /// <summary>
    /// Stores raw thread data as a Document with DocType "raw_thread".
    /// The body holds serialized RedditThreadData for later re-stitching.
    /// </summary>
    private static Document SerializeRawThread(RedditThreadData threadData, string normalizedUrl)
    {
        var rawJson = JsonSerializer.Serialize(threadData, JsonOpts.Default);
        return new Document
        {
            Url = normalizedUrl,
            Source = "reddit",
            DocType = "raw_thread",
            Title = threadData.Post.Title,
            Body = rawJson,
            Score = threadData.Post.Score,
            FetchedAt = DateTime.UtcNow,
            PublishedAt = threadData.Post.PublishedAt
        };
    }

    private static RedditThreadData DeserializeRawThread(Document cached)
    {
        return JsonSerializer.Deserialize<RedditThreadData>(cached.Body, JsonOpts.Default)
               ?? throw new ConnectorException("Failed to deserialize cached thread data");
    }
}
