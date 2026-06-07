namespace ClientManager.Shared.Models.Problems;

/// <summary>
/// RFC 7807 problem payload returned for every failure response. Carries the human-readable
/// title and detail, the HTTP status code, and the request trace identifier so callers can
/// correlate a failure with server-side logs.
/// </summary>
public record ProblemResponse
{
    /// <summary>
    /// Short, human-readable summary of the problem type (for example, "Not Found").
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// The HTTP status code generated for this occurrence of the problem.
    /// </summary>
    public int? Status { get; init; }

    /// <summary>
    /// Human-readable explanation specific to this occurrence of the problem.
    /// </summary>
    public string? Detail { get; init; }

    /// <summary>
    /// Identifier of the request that produced the failure, used to correlate with server logs.
    /// </summary>
    public string? TraceId { get; init; }
}