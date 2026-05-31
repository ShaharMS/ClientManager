using System.Net;
using System.Threading;
using ClientManager.Api.Models.Configuration;
using ClientManager.Api.Models.Exceptions;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Utils.StorageApi;

/// <summary>
/// Wraps outbound calls to the internal storage-facing API with narrow retry and fast-fail behavior.
/// Retries are bounded to requests that explicitly declare themselves retryable, transient gateway
/// failures are backed off with exponential delay, and repeated failures trip a short-lived circuit
/// so the public API fails fast instead of hammering an unhealthy storage host.
/// </summary>
public sealed class StorageApiResilienceHandler : DelegatingHandler
{
    private readonly StorageApiResilienceState _state;
    private readonly StorageApiOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="StorageApiResilienceHandler"/>.
    /// </summary>
    /// <param name="state">Shared circuit state tracking consecutive storage failures.</param>
    /// <param name="options">Outbound connection and resilience settings for the storage API.</param>
    public StorageApiResilienceHandler(
        StorageApiResilienceState state,
        IOptions<StorageApiOptions> options)
    {
        _state = state;
        _options = options.Value;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_state.TryGetRetryAfter(out var retryAfter))
        {
            throw CreateUnavailableException(retryAfter);
        }

        var maxAttempts = IsRetryable(request) ? _options.ReadRetryCount + 1 : 1;
        var delay = _options.InitialRetryDelay;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var attemptRequest = await CloneRequestAsync(request, cancellationToken);
            using var timeoutSource = CreateAttemptTimeoutSource(cancellationToken);
            try
            {
                var response = await base.SendAsync(attemptRequest, timeoutSource.Token);
                if (!IsTransientStatus(response.StatusCode))
                {
                    _state.RecordSuccess();
                    return response;
                }

                _state.RecordFailure();
                response.Dispose();

                if (attempt < maxAttempts)
                {
                    await DelayAsync(delay, cancellationToken);
                    delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
                    continue;
                }

                throw CreateUnavailableException(_options.CircuitBreakDuration);
            }
            catch (HttpRequestException exception)
            {
                _state.RecordFailure();
                if (attempt < maxAttempts)
                {
                    await DelayAsync(delay, cancellationToken);
                    delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
                    continue;
                }

                throw CreateUnavailableException(_options.CircuitBreakDuration, exception);
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                _state.RecordFailure();
                if (attempt < maxAttempts)
                {
                    await DelayAsync(delay, cancellationToken);
                    delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
                    continue;
                }

                throw CreateUnavailableException(_options.CircuitBreakDuration, exception);
            }
        }

        throw CreateUnavailableException(_options.CircuitBreakDuration);
    }

    private StorageApiUnavailableException CreateUnavailableException(TimeSpan retryAfter, Exception? innerException = null)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        return new StorageApiUnavailableException(
            "The storage service is temporarily unavailable.",
            seconds,
            innerException);
    }

    private static bool IsRetryable(HttpRequestMessage request)
    {
        if (request.Method == HttpMethod.Get || request.Method == HttpMethod.Head)
        {
            return true;
        }

        return request.Options.TryGetValue(StorageApiRequestOptions.Retryable, out var retryable) && retryable;
    }

    private static bool IsTransientStatus(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.BadGateway
        || statusCode == HttpStatusCode.ServiceUnavailable
        || statusCode == HttpStatusCode.GatewayTimeout;

    private static async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }
    }

    private CancellationTokenSource CreateAttemptTimeoutSource(CancellationToken cancellationToken)
    {
        var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_options.Timeout);
        return timeoutSource;
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var option in request.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is null)
        {
            return clone;
        }

        var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
        clone.Content = new ByteArrayContent(bytes);
        foreach (var header in request.Content.Headers)
        {
            clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
