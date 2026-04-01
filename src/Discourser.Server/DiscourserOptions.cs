namespace Discourser.Server;

public sealed class DiscourserOptions
{
    public string DatabasePath { get; set; } = "";
    public string ListenUrl { get; set; } = "http://localhost:5200";

    // Cache TTLs
    public int CacheTtlThreadHours { get; set; } = 24;
    public int CacheTtlSearchHours { get; set; } = 1;
    public int CachePurgeIntervalMinutes { get; set; } = 60;

    // Result limits
    public int DefaultMaxResults { get; set; } = 25;
    public int AbsoluteMaxResults { get; set; } = 100;
    public int DefaultMaxComments { get; set; } = 50;

    // HTTP
    public string UserAgent { get; set; } = "linux:discourser-mcp:0.1.0 (by /u/CONFIGURE_ME)";
    public int MaxRetries { get; set; } = 3;

    // SQLite
    public int BusyTimeoutMs { get; set; } = 5000;

    // Reddit
    public RedditOptions Reddit { get; set; } = new();

    public string GetResolvedDatabasePath()
    {
        if (!string.IsNullOrEmpty(DatabasePath))
            return DatabasePath;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".discourser-mcp", "cache.db");
    }
}

public sealed class RedditOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public int RateLimitPerMinute { get; set; } = 55;
    public int ArcticShiftPoliteIntervalMs { get; set; } = 1000;
    public string ArcticShiftBaseUrl { get; set; } = "https://arctic-shift.photon-reddit.com";
    public int HistoricalFallbackDays { get; set; } = 30;
}
