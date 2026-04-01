using Discourser.Core.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Discourser.Server.Services;

/// <summary>
/// Background service that periodically purges expired cache entries.
/// </summary>
public sealed class CachePurgeService : BackgroundService
{
    private readonly ICacheRepository _cache;
    private readonly TimeSpan _interval;
    private readonly ILogger<CachePurgeService> _logger;

    public CachePurgeService(
        ICacheRepository cache,
        DiscourserOptions options,
        ILogger<CachePurgeService> logger)
    {
        _cache = cache;
        _interval = TimeSpan.FromMinutes(options.CachePurgeIntervalMinutes);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await _cache.PurgeExpiredAsync(stoppingToken);
                _logger.LogDebug("Cache purge completed");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Cache purge failed");
            }
        }
    }
}
