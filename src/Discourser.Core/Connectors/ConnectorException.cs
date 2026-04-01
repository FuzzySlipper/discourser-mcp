namespace Discourser.Core.Connectors;

/// <summary>
/// Domain exception thrown by connectors. The MCP tool layer catches these
/// and converts them to stable error JSON payloads.
/// </summary>
public class ConnectorException : Exception
{
    public ConnectorException(string message) : base(message) { }
    public ConnectorException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// The provided URL is not a valid thread URL for this connector.
/// </summary>
public class InvalidThreadUrlException : ConnectorException
{
    public string Url { get; }
    public InvalidThreadUrlException(string url)
        : base($"Not a valid thread URL: {url}") => Url = url;
}

/// <summary>
/// An upstream API returned a rate limit (HTTP 429) and retries were exhausted.
/// </summary>
public class RateLimitExceededException : ConnectorException
{
    public RateLimitExceededException(string source)
        : base($"Rate limit exceeded for {source} after maximum retries") { }
}

/// <summary>
/// An upstream API returned an unexpected error after retries.
/// </summary>
public class UpstreamApiException : ConnectorException
{
    public int? StatusCode { get; }
    public UpstreamApiException(string source, int? statusCode, string detail)
        : base($"{source} API error (HTTP {statusCode}): {detail}") => StatusCode = statusCode;
    public UpstreamApiException(string source, string detail, Exception inner)
        : base($"{source} API error: {detail}", inner) { }
}
