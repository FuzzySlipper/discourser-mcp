using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Discourser.Core.Data;

public sealed class DatabaseInitializer
{
    private readonly string _connectionString;
    private readonly int _busyTimeoutMs;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(string databasePath, int busyTimeoutMs, ILogger<DatabaseInitializer> logger)
    {
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connectionString = $"Data Source={databasePath}";
        _busyTimeoutMs = busyTimeoutMs;
        _logger = logger;
    }

    public string ConnectionString => _connectionString;

    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var pragmaCmd = connection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA journal_mode = WAL;";
        await pragmaCmd.ExecuteNonQueryAsync();

        await using var schemaCmd = connection.CreateCommand();
        schemaCmd.CommandText = Schema;
        await schemaCmd.ExecuteNonQueryAsync();

        _logger.LogInformation("Cache database initialized at {ConnectionString}", _connectionString);
    }

    internal const string Schema = """
        ------------------------------------------------------------
        -- CACHED DOCUMENTS
        ------------------------------------------------------------
        CREATE TABLE IF NOT EXISTS cached_documents (
            url             TEXT PRIMARY KEY,
            source          TEXT NOT NULL,
            doc_type        TEXT NOT NULL,
            title           TEXT NOT NULL,
            body            TEXT NOT NULL,
            score           INTEGER,
            parent_url      TEXT,
            published_at    TEXT,
            fetched_at      TEXT NOT NULL,
            expires_at      TEXT NOT NULL,
            metadata_json   TEXT NOT NULL DEFAULT '{}'
        );

        CREATE INDEX IF NOT EXISTS idx_cached_documents_expires ON cached_documents(expires_at);
        CREATE INDEX IF NOT EXISTS idx_cached_documents_source  ON cached_documents(source);

        ------------------------------------------------------------
        -- CACHED SEARCHES
        ------------------------------------------------------------
        CREATE TABLE IF NOT EXISTS cached_searches (
            id              INTEGER PRIMARY KEY AUTOINCREMENT,
            source          TEXT NOT NULL,
            query_hash      TEXT NOT NULL,
            query_json      TEXT NOT NULL,
            result_json     TEXT NOT NULL,
            fetched_at      TEXT NOT NULL,
            expires_at      TEXT NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS idx_cached_searches_key ON cached_searches(source, query_hash);
        CREATE INDEX IF NOT EXISTS idx_cached_searches_expires    ON cached_searches(expires_at);
        """;
}
