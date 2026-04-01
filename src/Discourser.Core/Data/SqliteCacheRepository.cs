using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Discourser.Core.Models;
using Microsoft.Data.Sqlite;

namespace Discourser.Core.Data;

public sealed class SqliteCacheRepository : ICacheRepository
{
    private readonly DbConnectionFactory _db;

    public SqliteCacheRepository(DbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<Document?> GetDocumentAsync(string url, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT url, source, doc_type, title, body, score, parent_url,
                   published_at, fetched_at, metadata_json
            FROM cached_documents
            WHERE url = @url AND expires_at > datetime('now')
            """;
        cmd.Parameters.AddWithValue("@url", url);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return ReadDocument(reader);
    }

    public async Task SetDocumentAsync(Document document, TimeSpan ttl, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO cached_documents
                (url, source, doc_type, title, body, score, parent_url,
                 published_at, fetched_at, expires_at, metadata_json)
            VALUES
                (@url, @source, @docType, @title, @body, @score, @parentUrl,
                 @publishedAt, @fetchedAt, @expiresAt, @metadataJson)
            """;
        cmd.Parameters.AddWithValue("@url", document.Url);
        cmd.Parameters.AddWithValue("@source", document.Source);
        cmd.Parameters.AddWithValue("@docType", document.DocType);
        cmd.Parameters.AddWithValue("@title", document.Title);
        cmd.Parameters.AddWithValue("@body", document.Body);
        cmd.Parameters.AddWithValue("@score", (object?)document.Score ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@parentUrl", (object?)document.ParentUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@publishedAt",
            document.PublishedAt.HasValue
                ? document.PublishedAt.Value.ToString("o")
                : DBNull.Value);
        cmd.Parameters.AddWithValue("@fetchedAt", document.FetchedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@expiresAt",
            DateTime.UtcNow.Add(ttl).ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@metadataJson",
            document.Metadata.HasValue ? document.Metadata.Value.GetRawText() : "{}");

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<SearchResult?> GetSearchResultAsync(
        string source, SearchQuery query, CancellationToken ct = default)
    {
        var hash = ComputeQueryHash(query);

        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT result_json
            FROM cached_searches
            WHERE source = @source AND query_hash = @hash AND expires_at > datetime('now')
            """;
        cmd.Parameters.AddWithValue("@source", source);
        cmd.Parameters.AddWithValue("@hash", hash);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var json = reader.GetString(0);
        return JsonSerializer.Deserialize<SearchResult>(json);
    }

    public async Task SetSearchResultAsync(
        SearchResult result, SearchQuery query, TimeSpan ttl, CancellationToken ct = default)
    {
        var hash = ComputeQueryHash(query);
        var resultJson = JsonSerializer.Serialize(result);
        var queryJson = JsonSerializer.Serialize(query);

        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO cached_searches
                (source, query_hash, query_json, result_json, fetched_at, expires_at)
            VALUES
                (@source, @hash, @queryJson, @resultJson, @fetchedAt, @expiresAt)
            """;
        cmd.Parameters.AddWithValue("@source", result.Source);
        cmd.Parameters.AddWithValue("@hash", hash);
        cmd.Parameters.AddWithValue("@queryJson", queryJson);
        cmd.Parameters.AddWithValue("@resultJson", resultJson);
        cmd.Parameters.AddWithValue("@fetchedAt", result.RetrievedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@expiresAt",
            DateTime.UtcNow.Add(ttl).ToString("yyyy-MM-dd HH:mm:ss"));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task PurgeExpiredAsync(CancellationToken ct = default)
    {
        await using var conn = await _db.CreateConnectionAsync();

        await using var docCmd = conn.CreateCommand();
        docCmd.CommandText = "DELETE FROM cached_documents WHERE expires_at < datetime('now')";
        await docCmd.ExecuteNonQueryAsync(ct);

        await using var searchCmd = conn.CreateCommand();
        searchCmd.CommandText = "DELETE FROM cached_searches WHERE expires_at < datetime('now')";
        await searchCmd.ExecuteNonQueryAsync(ct);
    }

    internal static string ComputeQueryHash(SearchQuery query)
    {
        var json = JsonSerializer.Serialize(query);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexStringLower(bytes);
    }

    private static Document ReadDocument(SqliteDataReader reader)
    {
        var metaJson = reader.GetString(9);
        JsonElement? metadata = metaJson != "{}"
            ? JsonSerializer.Deserialize<JsonElement>(metaJson)
            : null;

        return new Document
        {
            Url = reader.GetString(0),
            Source = reader.GetString(1),
            DocType = reader.GetString(2),
            Title = reader.GetString(3),
            Body = reader.GetString(4),
            Score = reader.IsDBNull(5) ? null : reader.GetInt32(5),
            ParentUrl = reader.IsDBNull(6) ? null : reader.GetString(6),
            PublishedAt = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7)),
            FetchedAt = DateTime.Parse(reader.GetString(8)),
            Metadata = metadata
        };
    }
}
