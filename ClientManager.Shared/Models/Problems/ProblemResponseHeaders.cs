namespace ClientManager.Shared.Models.Problems;

/// <summary>
/// HTTP response headers echoed alongside <see cref="ProblemResponse"/> bodies so edge
/// proxies (for example nginx <c>auth_request</c>) can read failure metadata without the
/// subrequest response body, which <c>auth_request</c> discards.
/// </summary>
public static class ProblemResponseHeaders
{
    /// <summary>Short problem title (same value as <see cref="ProblemResponse.Title"/>).</summary>
    public const string Title = "X-Problem-Title";

    /// <summary>Problem detail message (same value as <see cref="ProblemResponse.Detail"/>).</summary>
    public const string Detail = "X-Problem-Detail";

    /// <summary>Request trace id (same value as <see cref="ProblemResponse.TraceId"/>).</summary>
    public const string TraceId = "X-Trace-Id";

    /// <summary>
    /// Compact JSON serialization of the problem payload (identical to the response body).
    /// Convenient for proxies that can forward a variable response body but not read the
    /// auth subrequest body directly.
    /// </summary>
    public const string Json = "X-Problem-Json";
}
