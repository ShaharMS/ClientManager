using Microsoft.JSInterop;

namespace ClientManager.AdminUI.Components.Shared;

/// <summary>
/// Timer + page-visibility polling used by Dashboard, Monitor, and Active Allocations.
/// </summary>
public sealed class PagePollingLifecycle : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly Func<Func<Task>, Task> _dispatchAsync;
    private readonly Func<Task> _pollAsync;
    private Timer? _timer;
    private TimeSpan _interval = TimeSpan.FromSeconds(10);
    private bool _isVisible = true;
    private IJSObjectReference? _visibilityModule;
    private DotNetObjectReference<PagePollingLifecycle>? _dotNetRef;

    public PagePollingLifecycle(IJSRuntime js, Func<Func<Task>, Task> dispatchAsync, Func<Task> pollAsync)
    {
        _js = js;
        _dispatchAsync = dispatchAsync;
        _pollAsync = pollAsync;
    }

    public async Task RegisterVisibilityAsync()
    {
        _visibilityModule = await _js.InvokeAsync<IJSObjectReference>("import", "./js/visibility.js");
        _dotNetRef = DotNetObjectReference.Create(this);
        await _visibilityModule.InvokeVoidAsync("register", _dotNetRef);
    }

    public void Start(TimeSpan? interval = null)
    {
        if (interval is not null)
        {
            _interval = interval.Value;
        }

        _timer?.Dispose();
        _timer = new Timer(_ =>
        {
            if (!_isVisible)
            {
                return;
            }

            _ = _dispatchAsync(PollAndRefreshAsync);
        }, null, _interval, _interval);
    }

    public void SetInterval(TimeSpan interval)
    {
        _interval = interval;
        Start();
    }

    [JSInvokable]
    public async Task OnVisibilityChanged(bool visible)
    {
        _isVisible = visible;
        if (visible)
        {
            await _dispatchAsync(PollAndRefreshAsync);
            Start();
        }
        else
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _timer?.Dispose();
        if (_visibilityModule is not null)
        {
            await _visibilityModule.InvokeVoidAsync("unregister");
            await _visibilityModule.DisposeAsync();
        }

        _dotNetRef?.Dispose();
    }

    private Task PollAndRefreshAsync() => _pollAsync();
}
