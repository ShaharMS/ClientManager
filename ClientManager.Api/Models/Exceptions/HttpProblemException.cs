namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Base type for expected public-host failures that map directly to an RFC 7807 HTTP problem
/// response. Each derived exception declares the HTTP status code, problem title, and public
/// detail (the exception message) so the error-handling middleware can translate failures
/// without a type-by-type mapping table.
/// </summary>
public abstract class HttpProblemException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="HttpProblemException"/>.
    /// </summary>
    /// <param name="statusCode">The HTTP status code the failure maps to.</param>
    /// <param name="title">The RFC 7807 problem title for the failure.</param>
    /// <param name="message">The public-facing detail describing the failure.</param>
    /// <param name="retryAfterSeconds">
    /// The number of seconds the caller should wait before retrying, surfaced as a
    /// <c>Retry-After</c> header. Null when the failure does not warrant a retry hint.
    /// </param>
    /// <param name="innerException">The underlying cause, when one exists.</param>
    /// <param name="errorCode">
    /// Optional machine-readable code for tracing and hot-path denial tagging. Not surfaced in HTTP responses.
    /// </param>
    protected HttpProblemException(
        int statusCode,
        string title,
        string message,
        int? retryAfterSeconds = null,
        Exception? innerException = null,
        string? errorCode = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Title = title;
        RetryAfterSeconds = retryAfterSeconds;
        ErrorCode = errorCode;
    }

    /// <summary>
    /// The HTTP status code the failure maps to.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// The RFC 7807 problem title for the failure.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// The number of seconds the caller should wait before retrying, or null when no retry
    /// hint applies. Surfaced as a <c>Retry-After</c> header by the error-handling middleware.
    /// </summary>
    public int? RetryAfterSeconds { get; }

    /// <summary>
    /// Optional machine-readable code for tracing. Null when the failure does not define one.
    /// </summary>
    public string? ErrorCode { get; }
}
