namespace ClientManager.Shared.Logging;

/// <summary>
/// Structured logging interface that enforces static messages with optional exception and extra data.
/// </summary>
/// <typeparam name="T">The category type for the logger - typically the consuming class,
/// used to name the underlying NLog logger.</typeparam>
public interface IAppLogger<T>
{
    void Trace(string message, object? extraData = null, Exception? exception = null);

    void Debug(string message, object? extraData = null, Exception? exception = null);

    void Info(string message, object? extraData = null, Exception? exception = null);

    void Warn(string message, object? extraData = null, Exception? exception = null);

    void Error(string message, object? extraData = null, Exception? exception = null);

    void Fatal(string message, object? extraData = null, Exception? exception = null);
}
