using System.Text.RegularExpressions;

namespace Discourser.Core.Connectors.Reddit;

/// <summary>
/// Normalizes Reddit URLs and extracts subreddit + thread ID.
/// Handles: reddit.com, www.reddit.com, old.reddit.com, np.reddit.com, i.reddit.com
/// </summary>
public static partial class RedditUrlParser
{
    public static (string Subreddit, string ThreadId) Parse(string url)
    {
        var match = ThreadPattern().Match(url);
        if (!match.Success)
            throw new InvalidThreadUrlException(url);

        return (match.Groups["sub"].Value, match.Groups["id"].Value);
    }

    public static string Normalize(string url)
    {
        var (sub, id) = Parse(url);
        return $"https://www.reddit.com/r/{sub}/comments/{id}/";
    }

    public static bool IsThreadUrl(string url) => ThreadPattern().IsMatch(url);

    [GeneratedRegex(
        @"https?://(?:(?:www|old|np|i)\.)?reddit\.com/r/(?<sub>[A-Za-z0-9_]+)/comments/(?<id>[A-Za-z0-9]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ThreadPattern();
}
