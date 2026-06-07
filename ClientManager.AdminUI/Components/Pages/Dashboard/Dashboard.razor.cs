using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Dashboard;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Services.ChartData;
using ClientManager.Shared.Models.Responses;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ClientManager.AdminUI.Components.Pages.Dashboard;

public partial class Dashboard : ComponentBase, IAsyncDisposable
{
    [Inject] private StatisticsApiService StatsService { get; set; } = null!;
    [Inject] private ClientApiService ClientService { get; set; } = null!;
    [Inject] private ServiceApiService ServiceService { get; set; } = null!;
    [Inject] private ResourcePoolApiService PoolService { get; set; } = null!;
    [Inject] private GlobalRateLimitApiService RateLimitApi { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    private bool _loading = true;
    private string? _error;
    private SystemOverviewResponse? _overview;

    private string _globalUsage = "-";
    private int _acquisitionPct;

    private string _selectedFilterType = "Service";
    private string? _selectedTargetId;
    private IEnumerable<string>? _selectedClientIds;
    private string? _tableSearch;

    private PagePollingLifecycle? _polling;
    private DashboardChartDataLoader? _chartLoader;

    private List<FilterOption> _filterTypes =
    [
        new("Service", "Service"),
        new("Resource Pool", "ResourcePool")
    ];
    private List<NamedItem> _filterTargets = [];
    private List<NamedItem> _allServices = [];
    private List<NamedItem> _allPools = [];
    private List<NamedItem> _clients = [];

    private List<TargetChartData> _targetCharts = [];
    private List<ClientUsagePoint> _perClientUsage = [];

    private TimeRangePreset _timeRange = TimeRangePreset.Default;
    private AxisScaleType _axisScaleType = AxisScaleType.Linear;

    private List<ClientSummaryTableRow> _clientSummaries = [];
    private List<ClientSummaryTableRow> _filteredClientSummaries = [];

    protected override async Task OnInitializedAsync()
    {
        _chartLoader = new DashboardChartDataLoader(StatsService, PoolService, RateLimitApi);

        try
        {
            _overview = await StatsService.GetOverviewAsync();

            var services = await ServiceService.GetAllAsync();
            var pools = await PoolService.GetAllAsync();
            var clients = await ClientService.GetAllAsync();

            _clients = clients.Select(c => new NamedItem(c.Id, c.Name)).ToList();
            _allServices = new List<NamedItem> { new(DashboardChartLoadContext.AllTargetsId, "All Services") }
                .Concat(services.Select(s => new NamedItem(s.Id, s.Name))).ToList();
            _allPools = new List<NamedItem> { new(DashboardChartLoadContext.AllTargetsId, "All Resource Pools") }
                .Concat(pools.Select(p => new NamedItem(p.Id, p.Name))).ToList();
            _filterTargets = _allServices;

            var globalUsage = await StatsService.GetGlobalUsageStatsAsync();
            if (globalUsage is not null)
            {
                _globalUsage = globalUsage.RequestsPerMinute > 0
                    ? globalUsage.RequestsPerMinute.ToString("N0")
                    : "-";
                _acquisitionPct = (int)Math.Round(globalUsage.AcquisitionPercentage);
            }

            var summaries = await StatsService.GetClientSummariesAsync();
            _clientSummaries = summaries.Select(r => new ClientSummaryTableRow(
                r.ClientId, r.DisplayName, r.AccessibleServices,
                r.TotalRateLimitCap, r.AccessiblePools,
                $"{r.UsedSlots}/{r.TotalAccessibleSlots}"
            )).ToList();
            _filteredClientSummaries = _clientSummaries;

            if (_filterTargets.Count > 0)
            {
                _selectedTargetId = _filterTargets[0].Id;
            }
        }
        catch (HttpRequestException ex)
        {
            _error = $"Unable to connect to the API: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }

        _polling = new PagePollingLifecycle(JS, InvokeAsync, RefreshDataAsync);
        _polling.Start();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && _polling is not null)
        {
            await _polling.RegisterVisibilityAsync();
        }
    }

    private Task OnPollingIntervalChanged(PollingIntervalPreset preset)
    {
        _polling?.SetInterval(preset.Interval);
        return Task.CompletedTask;
    }

    private Task OnAxisScaleChanged(AxisScaleType scale)
    {
        _axisScaleType = scale;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private void OnTableSearchChanged()
    {
        if (string.IsNullOrWhiteSpace(_tableSearch))
        {
            _filteredClientSummaries = _clientSummaries;
        }
        else
        {
            _filteredClientSummaries = _clientSummaries
                .Where(c => c.ClientId.Contains(_tableSearch, StringComparison.OrdinalIgnoreCase)
                    || c.DisplayName.Contains(_tableSearch, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    private async Task RefreshDataAsync()
    {
        try
        {
            var globalUsage = await StatsService.GetGlobalUsageStatsAsync();
            if (globalUsage is not null)
            {
                _globalUsage = globalUsage.RequestsPerMinute > 0
                    ? globalUsage.RequestsPerMinute.ToString("N0")
                    : "-";
                _acquisitionPct = (int)Math.Round(globalUsage.AcquisitionPercentage);
            }

            await LoadChartDataAsync();
        }
        catch (HttpRequestException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_polling is not null)
        {
            await _polling.DisposeAsync();
        }
    }
}
