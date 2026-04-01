# discourser-mcp

A local MCP server that exposes structured web retrieval as tools. LLM agents (Claude Code, QuillForge, etc.) call these tools to search Reddit, fetch threads, and get filtered results with stable source URLs. Summarization and query planning stay with the calling agent — this server just retrieves and structures the data.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A Reddit OAuth app (free, non-commercial personal use) — register at [reddit.com/prefs/apps](https://www.reddit.com/prefs/apps), select "script" type

## Build & Test

```bash
dotnet build discourser-mcp.slnx
dotnet test discourser-mcp.slnx
```

## Configuration

All settings live in `src/Discourser.Server/appsettings.json` or can be overridden via environment variables.

### Reddit Credentials (required)

Set your Reddit OAuth app credentials. Either edit `appsettings.json`:

```json
{
  "Discourser": {
    "Reddit": {
      "ClientId": "your_client_id",
      "ClientSecret": "your_client_secret"
    },
    "UserAgent": "linux:discourser-mcp:0.1.0 (by /u/your_reddit_username)"
  }
}
```

Or use environment variables:

```bash
export DISCOURSER__REDDIT__CLIENTID=your_client_id
export DISCOURSER__REDDIT__CLIENTSECRET=your_client_secret
export DISCOURSER__USERAGENT="linux:discourser-mcp:0.1.0 (by /u/your_reddit_username)"
```

### All Options

| Setting | Default | Description |
|---------|---------|-------------|
| `DatabasePath` | `~/.discourser-mcp/cache.db` | SQLite cache location |
| `ListenUrl` | `http://localhost:5200` | Server listen address |
| `CacheTtlThreadHours` | `24` | How long cached threads stay valid |
| `CacheTtlSearchHours` | `1` | How long cached search results stay valid |
| `CachePurgeIntervalMinutes` | `60` | Background cleanup interval |
| `DefaultMaxResults` | `25` | Default search result limit |
| `AbsoluteMaxResults` | `100` | Hard cap on search results |
| `DefaultMaxComments` | `50` | Max comments per thread stitch |
| `UserAgent` | *(must configure)* | Reddit-required User-Agent string |
| `MaxRetries` | `3` | HTTP retry attempts on failure |
| `BusyTimeoutMs` | `5000` | SQLite busy timeout |
| `Reddit:RateLimitPerMinute` | `55` | Reddit API rate limit |
| `Reddit:ArcticShiftBaseUrl` | `https://arctic-shift.photon-reddit.com` | Historical search fallback URL |
| `Reddit:HistoricalFallbackDays` | `30` | Days threshold for Arctic Shift fallback |

## Run

```bash
dotnet run --project src/Discourser.Server
```

CLI overrides:

```bash
dotnet run --project src/Discourser.Server -- --port 5201
dotnet run --project src/Discourser.Server -- --db-path /tmp/test-cache.db
```

The server listens on `http://localhost:5200` by default and exposes an MCP endpoint via SSE.

## MCP Tools

### search_reddit

Search Reddit posts. Returns structured results with source URLs.

| Parameter | Type | Description |
|-----------|------|-------------|
| `query` | string | Search terms (required) |
| `subreddit` | string? | Limit to a specific subreddit |
| `date_from` | string? | Earliest date, ISO 8601 |
| `date_to` | string? | Latest date, ISO 8601 |
| `min_score` | int? | Minimum post score |
| `min_words` | int? | Minimum body word count |
| `max_results` | int? | Max results to return |
| `force_refresh` | bool | Bypass cache |

### get_reddit_thread

Fetch a Reddit thread by URL. Stitches the post and top comments into a single document with comment URLs preserved in metadata for citation.

| Parameter | Type | Description |
|-----------|------|-------------|
| `url` | string | Reddit thread URL (required) |
| `min_comment_score` | int? | Minimum comment score to include |
| `min_comment_words` | int? | Minimum comment word count |
| `force_refresh` | bool | Bypass cache |

## Connect an MCP Client

Add to your `.mcp.json` or Claude Desktop config:

```json
{
  "mcpServers": {
    "discourser": {
      "type": "sse",
      "url": "http://localhost:5200/sse"
    }
  }
}
```

## Deploy as systemd Service

```bash
dotnet publish src/Discourser.Server -c Release -o /opt/discourser-mcp
sudo cp deploy/discourser-mcp.service /etc/systemd/system/
sudo systemctl enable --now discourser-mcp
```

## Architecture

```
src/Discourser.Core/       — Models, interfaces, connectors, filters, SQLite cache (no framework deps)
src/Discourser.Server/     — ASP.NET Core MCP server, tools, config, background services
tests/Discourser.Core.Tests/   — Unit + integration tests
tests/Architecture.Tests/      — Dependency boundary enforcement
```

Core has no dependency on ASP.NET or MCP. The Server project owns the transport boundary — JSON serialization and error shaping happen there, not in Core.

## Design Decisions

- **Raw thread caching**: Thread data is cached as raw API responses, then re-stitched per request with the caller's comment thresholds. This avoids stale filtered output when different threshold values are used.
- **Arctic Shift fallback**: Searches older than `HistoricalFallbackDays` automatically route to the Arctic Shift community API, which supports arbitrary date ranges (unlike Reddit's preset windows).
- **No magic numbers**: Every configurable value lives in `DiscourserOptions` and is bound from config. Nothing is hardcoded in business logic.
- **No server-side AI**: The server retrieves and structures data. Query translation, summarization, and output formatting are the calling agent's responsibility.
