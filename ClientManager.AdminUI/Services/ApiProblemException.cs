namespace ClientManager.AdminUI.Services;

public sealed class ApiProblemException : HttpRequestException
{
    public ApiProblemException(string message, string? errorCode, System.Net.HttpStatusCode? statusCode)
        : base(message, null, statusCode)
    {
        ErrorCode = errorCode;
    }

    public string? ErrorCode { get; }
}
