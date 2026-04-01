using Discourser.Core.Connectors;
using Discourser.Core.Connectors.Reddit;

namespace Discourser.Core.Tests.Connectors;

public class RedditUrlParserTests
{
    [Theory]
    [InlineData("https://www.reddit.com/r/csharp/comments/abc123/some_title/", "csharp", "abc123")]
    [InlineData("https://reddit.com/r/dotnet/comments/xyz789/my_post/", "dotnet", "xyz789")]
    [InlineData("https://old.reddit.com/r/programming/comments/def456/test/", "programming", "def456")]
    [InlineData("https://np.reddit.com/r/AskReddit/comments/ghi012/question/", "AskReddit", "ghi012")]
    [InlineData("https://i.reddit.com/r/pics/comments/jkl345/image/", "pics", "jkl345")]
    [InlineData("http://www.reddit.com/r/test/comments/mno678/post/", "test", "mno678")]
    public void Parse_ExtractsSubredditAndThreadId(string url, string expectedSub, string expectedId)
    {
        var (subreddit, threadId) = RedditUrlParser.Parse(url);
        Assert.Equal(expectedSub, subreddit);
        Assert.Equal(expectedId, threadId);
    }

    [Theory]
    [InlineData("https://www.reddit.com/r/csharp/")]
    [InlineData("https://www.reddit.com/user/someone/")]
    [InlineData("https://google.com")]
    [InlineData("not a url")]
    public void Parse_RejectsNonThreadUrls(string url)
    {
        Assert.Throws<InvalidThreadUrlException>(() => RedditUrlParser.Parse(url));
    }

    [Fact]
    public void Normalize_ProducesCanonicalUrl()
    {
        var normalized = RedditUrlParser.Normalize(
            "https://old.reddit.com/r/csharp/comments/abc123/some_long_title/");
        Assert.Equal("https://www.reddit.com/r/csharp/comments/abc123/", normalized);
    }

    [Theory]
    [InlineData("https://www.reddit.com/r/test/comments/abc/title/", true)]
    [InlineData("https://www.reddit.com/r/test/", false)]
    [InlineData("https://google.com", false)]
    public void IsThreadUrl_CorrectlyIdentifies(string url, bool expected)
    {
        Assert.Equal(expected, RedditUrlParser.IsThreadUrl(url));
    }
}
