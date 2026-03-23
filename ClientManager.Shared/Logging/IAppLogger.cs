namespace ClientManager.Shared.Logging;

/// <summary>
/// Structured logging interface that enforces static messages with optional exception and extra data.
/// </summary>
/// <typeparam name="T">The category type for the logger.</typeparam>
public interface IAppLogger<T>
{
    void Trace(string message);
    void Trace(string message, object extraData);
    void Trace(string message, Exception exception, object? extraData = null);

    void Debug(string message);
    void Debug(string message, object extraData);
    void Debug(string message, Exception exception, object? extraData = null);

    void Info(string message);
    void Info(string message, object extraData);
    void Info(string message, Exception exception, object? extraData = null);

    void Warn(string message);
    void Warn(string message, object extraData);
    void Warn(string message, Exception exception, object? extraData = null);

    void Error(string message);
    void Error(string message, object extraData);
    void Error(string message, Exception exception, object? extraData = null);

    void Fatal(string message);
    void Fatal(string message, object extraData);
    void Fatal(string message, Exception exception, object? extraData = null);
}
