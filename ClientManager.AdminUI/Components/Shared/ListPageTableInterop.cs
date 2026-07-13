using Microsoft.JSInterop;

namespace ClientManager.AdminUI.Components.Shared;

/// <summary>
/// Shared table.js registration and dynamic page-size calculation for list pages.
/// </summary>
public sealed class ListPageTableInterop : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly Func<int, Task> _onPageSizeChanged;
    private IJSObjectReference? _tableJs;
    private DotNetObjectReference<ListPageTableInterop>? _selfRef;

    public ListPageTableInterop(IJSRuntime js, Func<int, Task> onPageSizeChanged)
    {
        _js = js;
        _onPageSizeChanged = onPageSizeChanged;
    }

    public async Task InitializeAsync()
    {
        _tableJs = await _js.InvokeAsync<IJSObjectReference>("import", "./js/table.js");
        _selfRef = DotNetObjectReference.Create(this);
        await _tableJs.InvokeVoidAsync("register", _selfRef);
        await RecalculatePageSizeAsync();
    }

    [JSInvokable]
    public async Task OnWindowResize() => await RecalculatePageSizeAsync();

    private async Task RecalculatePageSizeAsync()
    {
        if (_tableJs is null)
        {
            return;
        }

        var pageSize = await _tableJs.InvokeAsync<int>("getPageSize", 45);
        await _onPageSizeChanged(pageSize);
    }

    public async ValueTask DisposeAsync()
    {
        if (_tableJs is not null)
        {
            await _tableJs.InvokeVoidAsync("unregister");
            await _tableJs.DisposeAsync();
        }

        _selfRef?.Dispose();
    }
}
