using System.Diagnostics;
using ClientManager.Api.Services.Storage.Models.Exceptions;

namespace ClientManager.Api.Services.Storage.Utils.Instrumentation;

/// <summary>
/// Shared activity/stopwatch envelope for hot-path storage operations (access, resources).
/// Preserves result/reason tagging and StorageApiProblemException → denied mapping.
/// </summary>
public static class StorageHotPathTrace
{
    public static async Task<TResult> RunAsync<TResult>(
        ActivitySource activitySource,
        string activityName,
        Action<Activity?> configureTags,
        Func<StorageHotPathCompletion, CancellationToken, Task<TResult>> operation,
        Action<StorageHotPathCompletion> onComplete,
        CancellationToken cancellationToken)
    {
        using var activity = activitySource.StartActivity(activityName, ActivityKind.Internal);
        configureTags(activity);

        var stopwatch = Stopwatch.StartNew();
        var completion = new StorageHotPathCompletion(activity, stopwatch);

        try
        {
            return await operation(completion, cancellationToken);
        }
        catch (StorageApiProblemException exception)
        {
            completion.SetOutcome("denied", exception.ErrorCode);
            throw;
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            completion.SetOutcome("canceled", exception.GetType().Name, exception);
            throw;
        }
        catch (Exception exception)
        {
            completion.SetOutcome("exception", exception.GetType().Name, exception);
            activity?.SetTag("error.type", exception.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
        finally
        {
            completion.Finish(onComplete);
        }
    }
}

/// <summary>
/// Mutable completion state recorded in the finally block of <see cref="StorageHotPathTrace"/>.
/// </summary>
public sealed class StorageHotPathCompletion(Activity? activity, Stopwatch stopwatch)
{
    public Activity? Activity { get; } = activity;
    public string Result { get; private set; } = "unknown";
    public string Reason { get; private set; } = "Unknown";
    public Exception? UnexpectedException { get; private set; }
    public double DurationMs { get; private set; }

    public void SetOutcome(string result, string reason, Exception? unexpectedException = null)
    {
        Result = result;
        Reason = reason;
        UnexpectedException = unexpectedException;
    }

    public void Finish(Action<StorageHotPathCompletion> onComplete)
    {
        stopwatch.Stop();
        DurationMs = stopwatch.Elapsed.TotalMilliseconds;
        Activity?.SetTag("operation.result", Result);
        Activity?.SetTag("denial.reason", Reason);
        Activity?.SetTag("duration_ms", DurationMs);
        onComplete(this);
    }
}
