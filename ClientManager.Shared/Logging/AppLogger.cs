using System.Reflection;
using Microsoft.Extensions.Logging;
using NLog;
using LogLevel = NLog.LogLevel;

namespace ClientManager.Shared.Logging;

/// <summary>
/// NLog-backed implementation of <see cref="IAppLogger{T}"/>.
///
/// <para>
///     Attaches the public properties of the <c>extraData</c> object to each log event
///     under the <c>ExtraData.</c> prefix using reflection, making them available as
///     structured fields in NLog layouts and sinks.
/// </para>
/// </summary>
/// <typeparam name="T">The category type for the logger.</typeparam>
public class AppLogger<T> : IAppLogger<T>
{
    private readonly Logger _nlogLogger;

    public AppLogger()
    {
        _nlogLogger = LogManager.GetLogger(typeof(T).FullName ?? typeof(T).Name);
    }

    public void Trace(string message, object? extraData = null, Exception? exception = null) => Log(LogLevel.Trace, message, exception, extraData);

    public void Debug(string message, object? extraData = null, Exception? exception = null) => Log(LogLevel.Debug, message, exception, extraData);

    public void Info(string message, object? extraData = null, Exception? exception = null) => Log(LogLevel.Info, message, exception, extraData);

    public void Warn(string message, object? extraData = null, Exception? exception = null) => Log(LogLevel.Warn, message, exception, extraData);

    public void Error(string message, object? extraData = null, Exception? exception = null) => Log(LogLevel.Error, message, exception, extraData);

    public void Fatal(string message, object? extraData = null, Exception? exception = null) => Log(LogLevel.Fatal, message, exception, extraData);

    private void Log(LogLevel level, string message, Exception? exception, object? extraData)
    {
        if (!_nlogLogger.IsEnabled(level))
            return;

        var logEvent = new LogEventInfo(level, _nlogLogger.Name, message)
        {
            Exception = exception
        };

        if (extraData is not null)
        {
            foreach (var property in extraData.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var value = property.GetValue(extraData);
                if (value is null)
                    continue;

                logEvent.Properties[$"ExtraData.{property.Name}"] = value;
            }
        }

        _nlogLogger.Log(logEvent);
    }
}
