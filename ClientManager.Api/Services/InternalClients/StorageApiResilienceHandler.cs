using System.Net;
using System.Threading;
using ClientManager.Api.Models.Configuration;
using ClientManager.Api.Models.Exceptions;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Services.InternalClients.Implementations;

// CR: Extend this doc - why is this needed? whats the purpose? not too much doc - just enough to explain the intent of this class and how it should be used.
// CR: In general - class needs documentation.
/// <summary>
/// Adds narrow retry and fast-fail behavior around internal storage API calls.
/// </summary>
public sealed class StorageApiResilienceHandler : DelegatingHandler
{
    private readonly StorageApiResilienceState _state;
    private readonly StorageApiOptions _options;

    public StorageApiResilienceHandler(
        StorageApiResilienceState state,
        IOptions<StorageApiOptions> options)
    {
        _state = state;
        _options = options.Value;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_state.TryGetRetryAfter(out var retryAfter))
        {
            throw CreateUnavailableException(retryAfter);
        }

        var maxAttempts = IsRetryableRead(request) ? _options.ReadRetryCount + 1 : 1;
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

    private static bool IsRetryableRead(HttpRequestMessage request)
    {
        if (request.Method == HttpMethod.Get || request.Method == HttpMethod.Head)
        {
            return true;
        }

        var path = request.RequestUri?.ToString() ?? string.Empty;
        // CR: This looks a little sensitive - what if it doesnt end with /search, but /lookup? also , why specifically search? seems arbitrary while undocumented.
        return request.Method == HttpMethod.Post
            && path.EndsWith("/search", StringComparison.OrdinalIgnoreCase);
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