using ClientManager.Shared.Models.Problems;

namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Storage or persistence backend is temporarily unreachable.
/// </summary>
public sealed class ServiceUnavailableException(string message, Exception? innerException = null)
    : HttpProblemException(
        StatusCodes.Status503ServiceUnavailable,
        "Service Unavailable",
        message,
        innerException: innerException,
        errorCode: StorageErrorCodes.StorageUnavailable);
