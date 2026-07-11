using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Utils;

/// <summary>
/// NDJSON line types and helpers for seed export/import streams.
/// </summary>
public static class SeedNdjson
{
    public const string TypeProperty = "$type";

    public const string TypeService = "service";
    public const string TypeResourcePool = "resourcePool";
    public const string TypeGlobalRateLimit = "globalRateLimit";
    public const string TypeClientConfiguration = "clientConfiguration";
    public const string TypeUsageSnapshot = "usageSnapshot";
    public const string TypeProgress = "_progress";
    public const string TypeSummary = "_summary";

    public const int BatchSize = 500;
    public const int ProgressIntervalDocuments = 500;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static string SerializeEntityLine(string type, object entity)
    {
        var node = JsonSerializer.SerializeToNode(entity, entity.GetType(), JsonOptions)!;
        node.AsObject()[TypeProperty] = type;
        return node.ToJsonString(JsonOptions);
    }

    public static string SerializeProgressLine(int processed, long elapsedMs) =>
        JsonSerializer.Serialize(new { type = TypeProgress, processed, elapsedMs }, JsonOptions)
            .Replace("\"type\":", $"\"{TypeProperty}\":", StringComparison.Ordinal);

    public static string SerializeSummaryLine(SeedImportSummary summary) =>
        JsonSerializer.Serialize(new
        {
            type = TypeSummary,
            summary.Created,
            summary.Updated,
            summary.Skipped,
            summary.Deleted,
            summary.Processed,
            summary.ElapsedMs
        }, JsonOptions).Replace("\"type\":", $"\"{TypeProperty}\":", StringComparison.Ordinal);

    public static bool IsControlLine(string? type) =>
        string.Equals(type, TypeProgress, StringComparison.Ordinal) ||
        string.Equals(type, TypeSummary, StringComparison.Ordinal);

    public static async Task WriteLineAsync(Stream output, string line, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(output, leaveOpen: true) { AutoFlush = true };
        await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
    }

    /// <summary>ponytail: minimal round-trip check for NDJSON line encoding.</summary>
    public static void AssertSelfCheck()
    {
        var line = SerializeEntityLine(TypeService, new Service { Id = "x", Name = "X", IsEnabled = true });
        var node = JsonNode.Parse(line)!.AsObject();
        Debug.Assert(node[TypeProperty]!.GetValue<string>() == TypeService);
        Debug.Assert(node["id"]!.GetValue<string>() == "x");
    }
}

public sealed class SeedProgressTracker(int progressIntervalDocuments = SeedNdjson.ProgressIntervalDocuments)
{
    public int Processed { get; set; }
    private int _sinceLastProgress;

    public async Task MaybeWriteProgressAsync(Stream output, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        _sinceLastProgress++;
        if (_sinceLastProgress < progressIntervalDocuments)
        {
            return;
        }

        _sinceLastProgress = 0;
        await SeedNdjson.WriteLineAsync(output, SeedNdjson.SerializeProgressLine(Processed, stopwatch.ElapsedMilliseconds), cancellationToken);
    }
}

/// <summary>
/// Parsed seed NDJSON entity line ready for import dispatch.
/// </summary>
public sealed record SeedNdjsonEntity(string Type, JsonObject Payload);

/// <summary>
/// Reads seed NDJSON lines from a stream, skipping control lines.
/// </summary>
public static class SeedNdjsonReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async IAsyncEnumerable<SeedNdjsonEntity> ReadEntitiesAsync(
        Stream input,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(input, leaveOpen: true);
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                yield break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var node = JsonNode.Parse(line)?.AsObject()
                ?? throw new JsonException("Seed NDJSON line must be a JSON object.");

            var type = node[SeedNdjson.TypeProperty]?.GetValue<string>()
                ?? throw new JsonException($"Seed NDJSON line missing '{SeedNdjson.TypeProperty}'.");

            if (SeedNdjson.IsControlLine(type))
            {
                continue;
            }

            node.Remove(SeedNdjson.TypeProperty);
            yield return new SeedNdjsonEntity(type, node);
        }
    }

    public static T Deserialize<T>(SeedNdjsonEntity entity) where T : class =>
        entity.Payload.Deserialize<T>(JsonOptions)
        ?? throw new JsonException($"Could not deserialize {typeof(T).Name} from seed NDJSON line.");
}
