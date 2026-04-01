using Discourser.Core.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace Discourser.Core.Tests;

/// <summary>
/// Creates an isolated temp SQLite database for each test class.
/// </summary>
public sealed class TestDb : IAsyncLifetime
{
    private string _dbPath = null!;
    public DbConnectionFactory Db { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"discourser-test-{Guid.NewGuid()}.db");
        var init = new DatabaseInitializer(_dbPath, busyTimeoutMs: 5000, NullLogger<DatabaseInitializer>.Instance);
        await init.InitializeAsync();
        Db = new DbConnectionFactory(init.ConnectionString, busyTimeoutMs: 5000);
    }

    public Task DisposeAsync()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
        return Task.CompletedTask;
    }
}
