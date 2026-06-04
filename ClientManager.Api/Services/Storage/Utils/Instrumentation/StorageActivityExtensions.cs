using System.Diagnostics;

namespace ClientManager.Api.Services.Storage.Utils.Instrumentation;

/// <summary>
/// Small helpers for internal storage activities with optional tags.
/// </summary>
public static class StorageActivityExtensions
{
    public static Activity? StartInternalActivity(
        this ActivitySource source,
        string name,
        Action<Activity?>? configure = null)
    {
        var activity = source.StartActivity(name, ActivityKind.Internal);
        configure?.Invoke(activity);
        return activity;
    }
}
