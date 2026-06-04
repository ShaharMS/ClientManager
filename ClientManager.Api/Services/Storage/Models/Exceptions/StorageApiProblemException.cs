namespace ClientManager.Api.Services.Storage.Models.Exceptions;

/// <summary>
/// Expected in-process storage failure mapped to an RFC 7807 HTTP problem response.
/// </summary>
public class StorageApiProblemException(
    string message,
    int statusCode,
    string title,
    string errorCode,
    int? retryAfterSeconds = null)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public string Title { get; } = title;

    public string ErrorCode { get; } = errorCode;

    public int? RetryAfterSeconds { get; } = retryAfterSeconds;
}
