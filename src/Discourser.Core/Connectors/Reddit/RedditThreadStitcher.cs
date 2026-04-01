using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Discourser.Core.Models;

namespace Discourser.Core.Connectors.Reddit;

/// <summary>
/// Assembles raw Reddit thread data (post + comments) into a single stitched Document.
/// Top-level comments only, sorted by score descending, capped and filtered.
/// </summary>
public sealed class RedditThreadStitcher
{
    public Document Stitch(
        RedditThreadData threadData,
        int minCommentScore,
        int minCommentWords,
        int maxComments)
    {
        var post = threadData.Post;

        var filteredComments = threadData.Comments
            .Where(c => c.Score >= minCommentScore)
            .Where(c => CountWords(c.Body) >= minCommentWords)
            .OrderByDescending(c => c.Score)
            .Take(maxComments)
            .ToList();

        var body = BuildBody(post, filteredComments);
        var metadata = BuildMetadata(post, filteredComments);

        return new Document
        {
            Url = post.FullUrl,
            Source = "reddit",
            DocType = "thread",
            Title = post.Title,
            Body = body,
            Score = post.Score,
            FetchedAt = DateTime.UtcNow,
            PublishedAt = post.PublishedAt,
            ParentUrl = null,
            Metadata = metadata
        };
    }

    private static string BuildBody(RedditPostData post, List<RedditCommentData> comments)
    {
        var sb = new StringBuilder();
        sb.AppendLine(post.Title);
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(post.SelfText))
        {
            sb.AppendLine(post.SelfText);
            sb.AppendLine();
        }

        foreach (var comment in comments)
        {
            sb.AppendLine("---");
            sb.AppendLine($"{comment.Author} (score: {comment.Score})");
            sb.AppendLine(comment.Body);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static JsonElement BuildMetadata(
        RedditPostData post, List<RedditCommentData> comments)
    {
        var meta = new JsonObject
        {
            ["subreddit"] = post.Subreddit,
            ["author"] = post.Author,
            ["comment_count"] = comments.Count,
            ["comment_urls"] = new JsonArray(
                comments.Select(c => JsonValue.Create(c.FullUrl)).ToArray<JsonNode>())
        };

        return JsonSerializer.Deserialize<JsonElement>(meta.ToJsonString());
    }

    private static int CountWords(string text) =>
        string.IsNullOrWhiteSpace(text) ? 0 : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}
