using Discourser.Core.Connectors.Reddit;

namespace Discourser.Core.Tests.Connectors;

public class RedditThreadStitcherTests
{
    private static readonly RedditPostData TestPost = new()
    {
        Title = "Test Post Title",
        SelfText = "This is the post body text.",
        Score = 100,
        Author = "testauthor",
        Permalink = "/r/csharp/comments/abc123/test_post_title/",
        CreatedUtc = 1700000000,
        NumComments = 5,
        Subreddit = "csharp"
    };

    private static RedditCommentData MakeComment(int score, string body, string author = "commenter") =>
        new()
        {
            Body = body,
            Score = score,
            Author = author,
            Permalink = $"/r/csharp/comments/abc123/test_post_title/{Guid.NewGuid():N}/",
            CreatedUtc = 1700000100
        };

    private readonly RedditThreadStitcher _stitcher = new();

    [Fact]
    public void Stitch_IncludesPostTitleAndBody()
    {
        var thread = new RedditThreadData(TestPost, new List<RedditCommentData>());
        var doc = _stitcher.Stitch(thread, minCommentScore: 1, minCommentWords: 1, maxComments: 50);

        Assert.Contains("Test Post Title", doc.Body);
        Assert.Contains("This is the post body text.", doc.Body);
    }

    [Fact]
    public void Stitch_SortsCommentsByScoreDescending()
    {
        var comments = new List<RedditCommentData>
        {
            MakeComment(5, "low score comment"),
            MakeComment(50, "high score comment"),
            MakeComment(25, "mid score comment")
        };
        var thread = new RedditThreadData(TestPost, comments);
        var doc = _stitcher.Stitch(thread, minCommentScore: 1, minCommentWords: 1, maxComments: 50);

        var highIdx = doc.Body.IndexOf("high score comment", StringComparison.Ordinal);
        var midIdx = doc.Body.IndexOf("mid score comment", StringComparison.Ordinal);
        var lowIdx = doc.Body.IndexOf("low score comment", StringComparison.Ordinal);

        Assert.True(highIdx < midIdx);
        Assert.True(midIdx < lowIdx);
    }

    [Fact]
    public void Stitch_FiltersCommentsByMinScore()
    {
        var comments = new List<RedditCommentData>
        {
            MakeComment(1, "low score comment"),
            MakeComment(10, "high score comment")
        };
        var thread = new RedditThreadData(TestPost, comments);
        var doc = _stitcher.Stitch(thread, minCommentScore: 5, minCommentWords: 1, maxComments: 50);

        Assert.DoesNotContain("low score comment", doc.Body);
        Assert.Contains("high score comment", doc.Body);
    }

    [Fact]
    public void Stitch_FiltersCommentsByMinWords()
    {
        var comments = new List<RedditCommentData>
        {
            MakeComment(10, "short"),
            MakeComment(10, "this comment has enough words to pass the filter")
        };
        var thread = new RedditThreadData(TestPost, comments);
        var doc = _stitcher.Stitch(thread, minCommentScore: 1, minCommentWords: 5, maxComments: 50);

        Assert.DoesNotContain("short", doc.Body);
        Assert.Contains("this comment has enough words", doc.Body);
    }

    [Fact]
    public void Stitch_CapsAtMaxComments()
    {
        var comments = Enumerable.Range(1, 10)
            .Select(i => MakeComment(100 - i, $"comment number {i}"))
            .ToList();

        var thread = new RedditThreadData(TestPost, comments);
        var doc = _stitcher.Stitch(thread, minCommentScore: 1, minCommentWords: 1, maxComments: 3);

        // Should only contain top 3 by score
        Assert.Contains("comment number 1", doc.Body);
        Assert.Contains("comment number 2", doc.Body);
        Assert.Contains("comment number 3", doc.Body);
        Assert.DoesNotContain("comment number 4", doc.Body);
    }

    [Fact]
    public void Stitch_PreservesCommentUrlsInMetadata()
    {
        var comments = new List<RedditCommentData>
        {
            MakeComment(10, "a good comment with enough words")
        };
        var thread = new RedditThreadData(TestPost, comments);
        var doc = _stitcher.Stitch(thread, minCommentScore: 1, minCommentWords: 1, maxComments: 50);

        Assert.True(doc.Metadata.HasValue);
        var urls = doc.Metadata.Value.GetProperty("comment_urls");
        Assert.Equal(1, urls.GetArrayLength());
    }

    [Fact]
    public void Stitch_EmptyComments_ProducesValidDocument()
    {
        var thread = new RedditThreadData(TestPost, new List<RedditCommentData>());
        var doc = _stitcher.Stitch(thread, minCommentScore: 1, minCommentWords: 1, maxComments: 50);

        Assert.Equal("thread", doc.DocType);
        Assert.Equal("reddit", doc.Source);
        Assert.Contains("Test Post Title", doc.Body);
        Assert.NotNull(doc.Metadata);
    }

    [Fact]
    public void Stitch_SetsCorrectDocumentFields()
    {
        var thread = new RedditThreadData(TestPost, new List<RedditCommentData>());
        var doc = _stitcher.Stitch(thread, minCommentScore: 1, minCommentWords: 1, maxComments: 50);

        Assert.Equal("thread", doc.DocType);
        Assert.Equal("reddit", doc.Source);
        Assert.Equal(100, doc.Score);
        Assert.Null(doc.ParentUrl);
        Assert.NotNull(doc.PublishedAt);
        Assert.Contains("/r/csharp/comments/abc123/", doc.Url);
    }
}
