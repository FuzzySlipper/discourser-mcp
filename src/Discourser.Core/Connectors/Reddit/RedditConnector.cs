using Discourser.Core.Filters;
using Discourser.Core.Models;
using Microsoft.Extensions.Logging;

namespace Discourser.Core.Connectors.Reddit;

/// <summary>
/// Orchestrates Reddit search and thread retrieval.
/// Selects backend (official API vs Arctic Shift) based on query date range.
/// Returns typed data — no JSON/MCP concerns.
/// </summary>
public sealed class RedditConnector : IConnector
{
    private readonly RedditApiClient _redditApi;
    private readonly ArcticShiftClient _arcticShift;
    private readonly RedditThreadStitcher _stitcher;
    private readonly int _historicalFallbackDays;
    private readonly int _defaultMaxResults;
    private readonly int _absoluteMaxResults;
    private readonly int _defaultMaxComments;
    private readonly ILogger<RedditConnector> _logger;

    public RedditConnector(
        RedditApiClient redditApi,
        ArcticShiftClient arcticShift,
        RedditThreadStitcher stitcher,
        int historicalFallbackDays,
        int defaultMaxResults,
        int absoluteMaxResults,
        int defaultMaxComments,
        ILogger<RedditConnector> logger)
    {
        _redditApi = redditApi;
        _arcticShift = arcticShift;
        _stitcher = stitcher;
        _historicalFallbackDays = historicalFallbackDays;
        _defaultMaxResults = defaultMaxResults;
        _absoluteMaxResults = absoluteMaxResults;
        _defaultMaxComments = defaultMaxComments;
        _logger = logger;
    }

    public string SourceName => "reddit";

    public async Task<SearchResult> SearchAsync(
        SearchQuery query, CancellationToken ct = default)
    {
        var maxResults = Math.Clamp(
            query.MaxResults ?? _defaultMaxResults, 1, _absoluteMaxResults);

        List<RedditPostData> posts;

        if (ShouldUseArcticShift(query))
        {
            _logger.LogDebug("Using Arctic Shift for historical query");
            posts = await _arcticShift.SearchAsync(
                query.Text,
                query.SiteFilters.GetValueOrDefault("subreddit"),
                query.DateFrom,
                query.DateTo,
                maxResults,
                ct);
        }
        else
        {
            var timeWindow = RedditApiClient.MapToTimeWindow(query.DateFrom, query.DateTo);
            posts = await _redditApi.SearchAsync(
                query.Text,
                query.SiteFilters.GetValueOrDefault("subreddit"),
                timeWindow,
                maxResults,
                ct);
        }

        // Normalize to Documents (DocType "post" — search hits, not full threads)
        IReadOnlyList<Document> documents = posts.Select(p => new Document
        {
            Url = p.FullUrl,
            Source = "reddit",
            DocType = "post",
            Title = p.Title,
            Body = p.SelfText,
            Score = p.Score,
            FetchedAt = DateTime.UtcNow,
            PublishedAt = p.PublishedAt,
            ParentUrl = null,
            Metadata = null
        }).ToList();

        // Apply filters
        if (query.MinScore.HasValue)
            documents = new MinScoreFilter(query.MinScore.Value).Apply(documents);

        if (query.MinWords.HasValue)
            documents = new MinWordsFilter(query.MinWords.Value).Apply(documents);

        documents = new DeduplicateFilter().Apply(documents);

        // Apply limit after filtering
        if (documents.Count > maxResults)
            documents = documents.Take(maxResults).ToList();

        return new SearchResult
        {
            Documents = documents,
            Source = "reddit",
            Query = query.Text,
            RetrievedAt = DateTime.UtcNow,
            FromCache = false
        };
    }

    public async Task<Document> FetchThreadAsync(
        string url, CancellationToken ct = default)
    {
        var (subreddit, threadId) = RedditUrlParser.Parse(url);

        var threadData = await _redditApi.FetchThreadAsync(subreddit, threadId, ct);

        return _stitcher.Stitch(threadData,
            minCommentScore: 1,
            minCommentWords: 1,
            maxComments: _defaultMaxComments);
    }

    private bool ShouldUseArcticShift(SearchQuery query)
    {
        if (query.DateFrom is null)
            return false;

        var age = DateTime.UtcNow - query.DateFrom.Value;
        return age.TotalDays > _historicalFallbackDays;
    }
}
