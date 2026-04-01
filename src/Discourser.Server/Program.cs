using Discourser.Core.Connectors;
using Discourser.Core.Connectors.Reddit;
using Discourser.Core.Data;
using Discourser.Server;
using Discourser.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;

var builder = WebApplication.CreateBuilder(args);

// Configuration (appsettings.json + environment variables + CLI args)
var options = new DiscourserOptions();
builder.Configuration.GetSection("Discourser").Bind(options);

// CLI overrides: --port and --db-path
if (builder.Configuration["port"] is { } port)
    options.ListenUrl = $"http://localhost:{port}";
if (builder.Configuration["db-path"] is { } dbPathOverride)
    options.DatabasePath = dbPathOverride;

builder.Services.AddSingleton(options);

// Kestrel
builder.WebHost.UseUrls(options.ListenUrl);

// Database
var dbPath = options.GetResolvedDatabasePath();
var initializer = new DatabaseInitializer(dbPath, options.BusyTimeoutMs, NullLogger<DatabaseInitializer>.Instance);
builder.Services.AddSingleton(new DbConnectionFactory(initializer.ConnectionString, options.BusyTimeoutMs));
builder.Services.AddSingleton<ICacheRepository, SqliteCacheRepository>();

// HTTP Clients
builder.Services.AddHttpClient("reddit");
builder.Services.AddHttpClient("arctic-shift");

// Reddit stack
builder.Services.AddSingleton(sp =>
{
    var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<RedditApiClient>>();
    return new RedditApiClient(
        httpFactory.CreateClient("reddit"),
        options.Reddit.ClientId,
        options.Reddit.ClientSecret,
        options.UserAgent,
        options.MaxRetries,
        logger);
});

builder.Services.AddSingleton(sp =>
{
    var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<ArcticShiftClient>>();
    return new ArcticShiftClient(
        httpFactory.CreateClient("arctic-shift"),
        options.Reddit.ArcticShiftBaseUrl,
        logger);
});

builder.Services.AddSingleton<RedditThreadStitcher>();

builder.Services.AddSingleton<IConnector>(sp =>
{
    var redditApi = sp.GetRequiredService<RedditApiClient>();
    var arcticShift = sp.GetRequiredService<ArcticShiftClient>();
    var stitcher = sp.GetRequiredService<RedditThreadStitcher>();
    var logger = sp.GetRequiredService<ILogger<RedditConnector>>();
    return new RedditConnector(
        redditApi, arcticShift, stitcher,
        options.Reddit.HistoricalFallbackDays,
        options.DefaultMaxResults,
        options.AbsoluteMaxResults,
        options.DefaultMaxComments,
        logger);
});

// Credential validation
builder.Services.AddSingleton<RedditCredentialService>();

// Background services
builder.Services.AddHostedService<CachePurgeService>();

// MCP
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// Initialize database on startup
await initializer.InitializeAsync();

// Validate credentials on startup (triggers log warning if missing)
_ = app.Services.GetRequiredService<RedditCredentialService>();

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// MCP endpoint
app.MapMcp();

app.Run();
