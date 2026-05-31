using ClientManager.Api.Models.Configuration;
using Microsoft.Extensions.Options;

// CR: Namespace should match folder structre
namespace ClientManager.Api.Services.InternalClients.Implementations;

// CR: Extend this doc - why is this needed? whats the purpose? not too much doc - just enough to explain the intent of this class and how it should be used.
/// <summary>
/// Tracks short-lived failure state for the internal storage-facing API circuit.
/// </summary>
public sealed class StorageApiResilienceState
{
    private readonly StorageApiOptions _options;
    private readonly object _sync = new();
    private int _consecutiveFailures;
    private DateTimeOffset? _openedUntilUtc;

    public StorageApiResilienceState(IOptions<StorageApiOptions> options)
    {
        _options = options.Value;
    }

    public bool TryGetRetryAfter(out TimeSpan retryAfter)
    {
        lock (_sync)
        {
            if (_openedUntilUtc is null)
            {
                retryAfter = TimeSpan.Zero;
                return false;
            }

            retryAfter = _openedUntilUtc.Value - DateTimeOffset.UtcNow;
            if (retryAfter > TimeSpan.Zero)
            {
                return true;
            }

            _openedUntilUtc = null;
            _consecutiveFailures = 0;
            retryAfter = TimeSpan.Zero;
            return false;
        }
    }

    public void RecordSuccess()
    {
        lock (_sync)
        {
            _consecutiveFailures = 0;
            _openedUntilUtc = null;
        }
    }

    public void RecordFailure()
    {
        lock (_sync)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= Math.Max(1, _options.FailureThreshold))
            {
                _openedUntilUtc = DateTimeOffset.UtcNow.Add(_options.CircuitBreakDuration);
            }
        }
    }
}