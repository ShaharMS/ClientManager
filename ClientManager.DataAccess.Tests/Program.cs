using ClientManager.DataAccess.Stores.Implementations;

await JsonFileCounterVerification.SetManyCountersRoundTripsAsync();
await JsonFileCounterVerification.ConcurrentCounterUpdatesRemainStableAsync();

Console.WriteLine("JsonFile counter verification passed.");

internal static class JsonFileCounterVerification
{
    private static readonly TimeSpan CounterWindow = TimeSpan.FromMinutes(10);

    public static async Task SetManyCountersRoundTripsAsync()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var store = new JsonFileDocumentStore(tempDirectory);
            await store.SetManyCountersAsync(new Dictionary<string, (long value, TimeSpan window)>
            {
                ["alpha"] = (3, CounterWindow),
                ["beta"] = (7, CounterWindow)
            });

            var counts = await store.GetManyCountersAsync(["alpha", "beta", "missing"]);
            Require(counts["alpha"] == 3, "alpha counter did not round-trip.");
            Require(counts["beta"] == 7, "beta counter did not round-trip.");
            Require(counts["missing"] == 0, "missing counter should read as zero.");
        }
        finally
        {
            DeleteDirectory(tempDirectory);
        }
    }

    public static async Task ConcurrentCounterUpdatesRemainStableAsync()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var firstStore = new JsonFileDocumentStore(tempDirectory);
            var secondStore = new JsonFileDocumentStore(tempDirectory);
            await firstStore.SetCounterAsync("floor", 2, CounterWindow);

            var tasks = Enumerable.Range(0, 200)
                .Select(index => RunCounterOperationAsync(firstStore, secondStore, index));
            await Task.WhenAll(tasks);

            var counts = await firstStore.GetManyCountersAsync(["shared", "floor"]);
            Require(counts["shared"] == 100, "shared counter lost concurrent increments.");
            Require(counts["floor"] == 0, "decremented counter should floor at zero.");
            RequireNoTempFiles(tempDirectory);
        }
        finally
        {
            DeleteDirectory(tempDirectory);
        }
    }

    private static Task RunCounterOperationAsync(
        JsonFileDocumentStore firstStore,
        JsonFileDocumentStore secondStore,
        int index)
    {
        var store = index % 2 == 0 ? firstStore : secondStore;
        return (index % 4) switch
        {
            0 => store.IncrementCounterAsync("shared", CounterWindow),
            1 => SetBatchCounterAsync(store, index),
            2 => store.DecrementCounterAsync("floor"),
            _ => IncrementBatchCounterAsync(store)
        };
    }

    private static Task SetBatchCounterAsync(JsonFileDocumentStore store, int index)
    {
        return store.SetManyCountersAsync(new Dictionary<string, (long value, TimeSpan window)>
        {
            [$"batch:{index % 8}"] = (index, CounterWindow)
        });
    }

    private static Task IncrementBatchCounterAsync(JsonFileDocumentStore store)
    {
        return store.IncrementManyCountersAsync(new Dictionary<string, (long amount, TimeSpan window)>
        {
            ["shared"] = (1, CounterWindow)
        });
    }

    private static string CreateTempDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"client-manager-json-counters-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    private static void RequireNoTempFiles(string tempDirectory)
    {
        var tempFiles = Directory.GetFiles(tempDirectory, "*.tmp", SearchOption.TopDirectoryOnly);
        Require(tempFiles.Length == 0, $"JsonFile temp files were left behind: {string.Join(", ", tempFiles)}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void DeleteDirectory(string tempDirectory)
    {
        if (Directory.Exists(tempDirectory))
            Directory.Delete(tempDirectory, recursive: true);
    }
}
