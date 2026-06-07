using System.Diagnostics;
using ClientManager.Shared.Models.Entities;
using ClientManager.Api.Services.Storage.Instrumentation;

namespace ClientManager.Api.Services.Storage.RateLimiting.Strategies;

internal static class RateLimitStrategyInstrumentation
{
    internal static async Task<RateLimitResult> TraceAsync(
        StorageMetrics metrics,
        string strategyName,
        string mode,
        int counterKeyCount,
        Func<Task<RateLimitResult>> operation)
    {
        using var activity = metrics.ActivitySource.StartActivity(
            "storage.ratelimit.strategy",
            ActivityKind.Internal);
        var stopwatch = Stopwatch.StartNew();

        SetStartTags(activity, strategyName, mode, counterKeyCount);

        try
        {
            var result = await operation();
            stopwatch.Stop();
            Complete(metrics, activity, strategyName, mode, counterKeyCount, result, stopwatch.Elapsed.TotalMilliseconds);
            return result;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            Complete(metrics, activity, strategyName, mode, counterKeyCount, exception, stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    private static void SetStartTags(Activity? activity, string strategyName, string mode, int counterKeyCount)
    {
        activity?.SetTag("ratelimit.strategy", strategyName);
        activity?.SetTag("ratelimit.mode", mode);
        activity?.SetTag("ratelimit.counter_key_count", counterKeyCount);
    }

    private static void Complete(
        StorageMetrics metrics,
        Activity? activity,
        string strategyName,
        string mode,
        int counterKeyCount,
        RateLimitResult result,
        double durationMs)
    {
        var outcome = result.IsAllowed ? "allowed" : "denied";
        activity?.SetTag("ratelimit.result", outcome);
        activity?.SetTag("ratelimit.remaining_requests", result.RemainingRequests);
        activity?.SetTag("ratelimit.retry_after_seconds", result.RetryAfterSeconds);
        activity?.SetTag("duration_ms", durationMs);
        RecordDuration(metrics, strategyName, mode, counterKeyCount, outcome, durationMs);
    }

    private static void Complete(
        StorageMetrics metrics,
        Activity? activity,
        string strategyName,
        string mode,
        int counterKeyCount,
        Exception exception,
        double durationMs)
    {
        activity?.SetTag("error.type", exception.GetType().Name);
        activity?.SetTag("duration_ms", durationMs);
        activity?.SetStatus(ActivityStatusCode.Error);
        RecordDuration(metrics, strategyName, mode, counterKeyCount, "exception", durationMs);
    }

    private static void RecordDuration(
        StorageMetrics metrics,
        string strategyName,
        string mode,
        int counterKeyCount,
        string result,
        double durationMs)
    {
        metrics.RateLimitStrategyDuration.Record(durationMs, new TagList
        {
            { "strategy", strategyName },
            { "mode", mode },
            { "counter_key_count", counterKeyCount },
            { "result", result }
        });
    }
}