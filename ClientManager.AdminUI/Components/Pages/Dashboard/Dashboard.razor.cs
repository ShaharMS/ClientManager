using ClientManager.AdminUI.Components.Shared;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace ClientManager.AdminUI.Components.Pages.Dashboard;

public partial class Dashboard : ComponentBase, IAsyncDisposable
{
    [Inject] private StatisticsApiService StatsService { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResources> Localizer { get; set; } = null!;
    [Inject] private ApiErrorLocalizer Errors { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    private bool _loading = true;
    private string? _error;
    private DashboardOverview? _overview;
    private string _rpmDisplay = "-";
    private PagePollingLifecycle? _polling;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await RefreshDataAsync();
        }
        catch (HttpRequestException ex)
        {
            _error = Errors.Format("Api.UnableToConnect", ex);
        }
        finally
        {
            _loading = false;
        }

        _polling = new PagePollingLifecycle(JS, InvokeAsync, RefreshOverviewAsync);
        _polling.Start(TimeSpan.FromSeconds(10));
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && _polling is not null)
        {
            await _polling.RegisterVisibilityAsync();
        }
    }

    private async Task RefreshOverviewAsync()
    {
        try
        {
            await RefreshDataAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (HttpRequestException ex)
        {
            _error = Errors.Format("Api.UnableToConnect", ex);
        }
    }

    private async Task RefreshDataAsync()
    {
        var overview = await StatsService.GetOverviewAsync();
        if (overview is null)
        {
            return;
        }

        _overview = overview;
        _rpmDisplay = overview.RequestsPerMinute > 0
            ? overview.RequestsPerMinute.ToString("N0")
            : "-";
        _error = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_polling is not null)
        {
            await _polling.DisposeAsync();
        }
    }
}
