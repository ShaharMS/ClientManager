using ClientManager.Api.Storage.Databases.Interfaces;
using ClientManager.Shared.Configuration.Storage;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Buffers RPM events per replica and flushes them into the global second-bucket ring.
/// </summary>
/// <remarks>
/// <para>
/// Access checks call <see cref="RecordRequest"/> on the hot path. Events accumulate locally until
/// either the configured event count or flush interval is reached, then merge into storage through
/// <see cref="IRpmRingDatabase"/>.
/// </para>
/// <para>
/// Dashboard RPM reads sum request counts over the last 60 seconds.
/// </para>
/// </remarks>
public sealed class RpmAccountingService : IDisposable
{
    private readonly IRpmRingDatabase _database;
    private readonly RpmOptions _options;
    private readonly object _sync = new();
    private readonly Dictionary<string, long> _buffer = new(StringComparer.Ordinal);
    private int _bufferedEvents;
    private Timer? _flushTimer;

    public RpmAccountingService(IRpmRingDatabase database, IOptions<RpmOptions> options)
    {
        _database = database;
        _options = options.Value;
        if (_options.FlushEventCount > 1)
        {
            _flushTimer = new Timer(_ => _ = FlushAsync(CancellationToken.None), null, _options.FlushInterval, _options.FlushInterval);
        }
    }

    /// <summary>
    /// Records one granted access-check request in the local buffer.
    /// </summary>
    public void RecordRequest()
    {
        var bucketKey = GetBucketKey(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        lock (_sync)
        {
            _buffer[bucketKey] = _buffer.GetValueOrDefault(bucketKey) + 1;
            _bufferedEvents++;
            if (_options.FlushEventCount == 1 || _bufferedEvents >= _options.FlushEventCount)
            {
                _ = FlushAsync(CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Returns the request count summed over the last 60 seconds.
    /// </summary>
    public async Task<double> GetRequestsPerMinuteAsync(CancellationToken cancellationToken = default)
    {
        await FlushAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStart = now - (long)RpmOptions.RpmWindow.TotalSeconds;
        var firstOverlappingBucket = windowStart - _options.BucketSizeSeconds + 1;
        var buckets = await _database.GetBucketsByPrefixAsync(string.Empty, cancellationToken);
        var total = buckets
            .Where(pair => long.TryParse(StripBucketKey(pair.Key), out var bucket) && bucket >= firstOverlappingBucket && bucket <= now)
            .Sum(pair => pair.Value);
        return total;
    }

    /// <inheritdoc />
    public void Dispose() => _flushTimer?.Dispose();

    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, long> snapshot;
        lock (_sync)
        {
            if (_buffer.Count == 0)
            {
                return;
            }

            snapshot = new Dictionary<string, long>(_buffer, StringComparer.Ordinal);
            _buffer.Clear();
            _bufferedEvents = 0;
        }

        await _database.IncrementBucketsAsync(snapshot, _options.Retention, cancellationToken);
    }

    private static string StripBucketKey(string key) =>
        key.StartsWith("rpm:", StringComparison.Ordinal) ? key["rpm:".Length..] : key;

    private string GetBucketKey(long epochSecond)
    {
        var aligned = epochSecond / _options.BucketSizeSeconds * _options.BucketSizeSeconds;
        return aligned.ToString();
    }
}
