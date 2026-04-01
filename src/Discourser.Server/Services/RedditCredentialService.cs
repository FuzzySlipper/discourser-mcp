using Microsoft.Extensions.Logging;

namespace Discourser.Server.Services;

/// <summary>
/// Validates Reddit credentials at startup and logs the operating mode.
/// </summary>
public sealed class RedditCredentialService
{
    public RedditCredentialService(DiscourserOptions options, ILogger<RedditCredentialService> logger)
    {
        var reddit = options.Reddit;

        if (string.IsNullOrEmpty(reddit.ClientId) || string.IsNullOrEmpty(reddit.ClientSecret))
        {
            logger.LogInformation(
                "Reddit credentials not configured — running in unauthenticated mode " +
                "(old.reddit.com .json endpoints). Search quality and rate limits are lower. " +
                "Set Discourser:Reddit:ClientId and Discourser:Reddit:ClientSecret to enable OAuth");
        }
        else
        {
            logger.LogInformation("Reddit OAuth configured (ClientId: {ClientIdPrefix}...)",
                reddit.ClientId[..Math.Min(4, reddit.ClientId.Length)]);
        }
    }
}
