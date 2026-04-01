using Microsoft.Data.Sqlite;

namespace Discourser.Core.Data;

public sealed class DbConnectionFactory
{
    private readonly string _connectionString;
    private readonly int _busyTimeoutMs;

    public DbConnectionFactory(string connectionString, int busyTimeoutMs)
    {
        _connectionString = connectionString;
        _busyTimeoutMs = busyTimeoutMs;
    }

    public async Task<SqliteConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA busy_timeout = {_busyTimeoutMs};";
        await cmd.ExecuteNonQueryAsync();

        return connection;
    }
}
