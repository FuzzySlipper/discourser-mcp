using Microsoft.Extensions.Logging;

namespace Discourser.Server.Services;

/// <summary>
/// Validates Reddit credentials are configured at startup.
/// </summary>
public sealed class RedditCredentialService
{
    public RedditCredentialService(DiscourserOptions options, ILogger<RedditCredentialService> logger)
    {
        var reddit = options.Reddit;

        if (string.IsNullOrEmpty(reddit.ClientId) || string.IsNullOrEmpty(reddit.ClientSecret))
        {
            logger.LogWarning(
                "Reddit credentials not configured. Set Discourser:Reddit:ClientId and " +
                "Discourser:Reddit:ClientSecret in appsettings.json or via environment variables " +
                "(DISCOURSER_REDDIT__CLIENTID, DISCOURSER_REDDIT__CLIENTSECRET)");
        }
        else
        {
            logger.LogInformation("Reddit credentials configured (ClientId: {ClientIdPrefix}...)",
                reddit.ClientId[..Math.Min(4, reddit.ClientId.Length)]);
        }
    }
}
