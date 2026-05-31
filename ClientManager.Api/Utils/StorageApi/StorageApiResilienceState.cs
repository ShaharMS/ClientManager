using ClientManager.Api.Models.Configuration;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Utils.StorageApi;

/// <summary>
/// Tracks short-lived failure state for the storage-facing API circuit shared across outbound calls.
/// Consecutive transport failures are counted and, once the configured threshold is reached, the circuit
/// opens for a fixed window so the resilience handler can fast-fail instead of retrying an unhealthy host.
/// A single successful call resets the count and closes the circuit.
/// </summary>
public sealed class StorageApiResilienceState
{
    private readonly StorageApiOptions _options;
    private readonly object _sync = new();
    private int _consecutiveFailures;
    private DateTimeOffset? _openedUntilUtc;

    /// <summary>
    /// Initializes a new instance of <see cref="StorageApiResilienceState"/>.
    /// </summary>
    /// <param name="options">Failure threshold and circuit-break duration settings.</param>
    public StorageApiResilienceState(IOptions<StorageApiOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Determines whether the circuit is currently open and, if so, how long callers should wait.
    /// Closes an expired circuit as a side effect so the next call is allowed through.
    /// </summary>
    /// <param name="retryAfter">The remaining time the circuit stays open; <see cref="TimeSpan.Zero"/> when closed.</param>
    /// <returns><see langword="true"/> when the circuit is open and callers must fast-fail.</returns>
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

    /// <summary>
    /// Records a successful storage call, resetting the failure count and closing the circuit.
    /// </summary>
    public void RecordSuccess()
    {
        lock (_sync)
        {
            _consecutiveFailures = 0;
            _openedUntilUtc = null;
        }
    }

    /// <summary>
    /// Records a failed storage call, opening the circuit once the consecutive failure threshold is reached.
    /// </summary>
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
