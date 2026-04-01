# Agents Guide — discourser-mcp

Directions for AI agents working on this codebase.

## Project Context

discourser-mcp is an MCP server for structured web retrieval. It follows den-mcp's patterns (same toolchain, DI style, test patterns) but the projects are fully independent — no shared code, no cross-references.

The project is tracked in den under project ID `discourser-mcp`. Check den for current tasks, priorities, and messages before starting work.

## Rules

- **No magic numbers.** Every threshold, default, limit, timeout, interval, and URL must be defined in `DiscourserOptions` (or nested options classes) and read from configuration. Never hardcode values in business logic.
- **Core stays typed.** `Discourser.Core` returns typed models and throws typed exceptions. No JSON serialization, no MCP-shaped payloads, no transport concerns. That boundary lives in `Discourser.Server/Tools/`.
- **All SQL is parameterized.** No string interpolation in queries.
- **Test what you build.** New Core code gets unit tests in `Discourser.Core.Tests`. Use the `TestDb` fixture for anything touching SQLite. Architecture boundary tests enforce that Core stays framework-free.

## Architecture

```
Discourser.Core          — models, interfaces, connectors, filters, cache (zero framework deps)
Discourser.Server        — MCP tools, DI wiring, config, background services
```

**Dependency boundaries** (enforced by `Architecture.Tests`):
- Core must not reference `ModelContextProtocol` or `Microsoft.AspNetCore`
- Server must not reference `Microsoft.Data.Sqlite` directly

**Data flow:**
```
MCP Client → RedditTools (Server) → IConnector (Core) → RedditApiClient / ArcticShiftClient
                                                       → RedditThreadStitcher → Document
```

JSON serialization and error-to-payload conversion happen at the tool boundary in `RedditTools.cs`, not in Core.

## Key Patterns

| Pattern | Where | Reference |
|---------|-------|-----------|
| Method parameter DI in MCP tools | `Server/Tools/RedditTools.cs` | Static methods, deps injected by MCP SDK |
| `JsonElement?` for mixed-type metadata | `Core/Models/Document.cs` | `GetRawText()` on write, `Deserialize<JsonElement>` on read |
| Raw-then-render caching | `Server/Tools/RedditTools.cs` | Cache raw thread data, re-stitch per request with caller's thresholds |
| Typed domain exceptions | `Core/Connectors/ConnectorException.cs` | `InvalidThreadUrlException`, `RateLimitExceededException`, `UpstreamApiException` |
| SHA-256 query hashing for cache | `Core/Data/SqliteCacheRepository.cs` | `ComputeQueryHash()` — serialize query to JSON, hash it |
| Centralized JSON options | `Server/JsonOpts.cs` | snake_case, ignore nulls, string enum converter |
| TestDb fixture | `Tests/Discourser.Core.Tests/TestDb.cs` | `IAsyncLifetime`, temp SQLite per test class |

## Adding a New Connector (e.g. v2ex, HN)

1. Create `Core/Connectors/{Site}/` with a client class returning typed intermediate data
2. Create a stitcher if the site has threaded content
3. Create a connector class implementing `IConnector`
4. Add options to `DiscourserOptions` for the new site (credentials, rate limits, base URLs)
5. When there are multiple connectors, switch `IConnector` registration from non-keyed singleton to either keyed singletons or a connector registry
6. Add `Server/Tools/{Site}Tools.cs` with MCP tool definitions — this is where JSON and error shaping happen
7. Wire DI in `Program.cs`
8. Add tests for URL parsing, response parsing, stitching, and orchestration logic
9. Query translation (e.g. English → Chinese for v2ex) is the calling agent's responsibility, not the connector's

## Adding a New Filter

1. Implement `IFilter` in `Core/Filters/`
2. Constructor takes threshold parameter (sourced from config at the call site)
3. Add tests in `Tests/Discourser.Core.Tests/Filters/FilterTests.cs`
4. Apply in the connector's `SearchAsync` or in the tool method, depending on scope

## Working with the Cache

- `cached_documents`: keyed by URL, TTL-based expiry. For threads, store raw API data (DocType `raw_thread`), not stitched output.
- `cached_searches`: keyed by (source, SHA-256 of serialized SearchQuery). Short TTL.
- `force_refresh` parameter on tools bypasses cache reads.
- `CachePurgeService` runs on a timer (interval from config). No manual purge needed.

## Before Submitting

```bash
dotnet build discourser-mcp.slnx    # must be 0 warnings, 0 errors
dotnet test discourser-mcp.slnx     # all tests must pass
```

Check that no new magic numbers snuck into the code. Every literal that could change should be in `DiscourserOptions`.
