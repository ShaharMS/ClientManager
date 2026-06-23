using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Allocations;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Services.ChartData;
using ClientManager.AdminUI.Utils;using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ClientManager.AdminUI.Components.Pages.Allocations;

public partial class ActiveAllocations : ComponentBase, IAsyncDisposable
{
    [Inject] private StatisticsApiService StatsService { get; set; } = null!;
    [Inject] private ClientApiService ClientService { get; set; } = null!;
    [Inject] private GlobalRateLimitApiService RateLimitApi { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    private static readonly List<MetricOption> MetricOptions =
    [
        new(AllocationsChartSection.ActiveAllocationsMetric, "Active Allocations"),
        new(AllocationsChartSection.AccessRequestsMetric, "Access Requests")
    ];

    private List<ResourcePoolStatisticsResponse> _pools = [];
    private List<PoolSummaryRow> _poolSummaryRows = [];
    private List<NamedItem> _poolOptions = [];
    private List<NamedItem> _clientOptions = [];
    private List<ClientConfiguration> _allClients = [];
    private string _selectedPoolId = AllocationsLoadContext.AllPoolsId;
    private IEnumerable<string>? _selectedClientIds;

    private bool _chartLoading = true;
    private string? _error;
    private PagePollingLifecycle? _polling;
    private AllocationsDataLoader? _dataLoader;

    private ChartTimeRange _timeRange = ChartTimeRange.FromPreset(TimeRangePreset.Default);
    private AxisScaleType _axisScaleType = AxisScaleType.Linear;
    private string _selectedMetric = AllocationsChartSection.ActiveAllocationsMetric;
    private int _chartBucketCount = ChartBucketAggregator.DefaultBucketCount;
    private IJSObjectReference? _chartJs;
    private DotNetObjectReference<ActiveAllocations>? _chartSelfRef;

    private List<TargetChartData> _targetCharts = [];
    private List<AllocationClientRow> _clientDetailRows = [];
    private AllocationsClientDetailGrid? _clientDetailGrid;
    private AllocationsPoolSummaryGrid? _allPoolsGrid;

    private bool IsAccessMetric => _selectedMetric == AllocationsChartSection.AccessRequestsMetric;

    private bool ShowDeniedBreakdown => DeniedBreakdownHelper.ShowBreakdown(_selectedClientIds);

    private DeniedViewMode AllocationDeniedViewMode =>
        IsAccessMetric ? DeniedViewMode.RateLimitDenied : DeniedViewMode.CapacityDenied;

    private bool ShowPoolColumnInClientGrid => _selectedPoolId == AllocationsLoadContext.AllPoolsId;

    private string ClientDetailTitle =>
        _selectedPoolId == AllocationsLoadContext.AllPoolsId
            ? IsAccessMetric ? "Client Access Detail" : "Client Allocation Detail"
            : $"{SelectedPoolName} - {(IsAccessMetric ? "Client Access Detail" : "Client Allocation Detail")}";

    private string SelectedPoolName =>
        _selectedPoolId == AllocationsLoadContext.AllPoolsId || string.IsNullOrEmpty(_selectedPoolId)
            ? "All Pools"
            : _poolOptions.FirstOrDefault(p => p.Id == _selectedPoolId)?.Name ?? "All Pools";

    private string ChartTitle =>
        $"{SelectedPoolName} - {(IsAccessMetric ? "Access Requests" : "Active Allocations")}";

    protected override async Task OnInitializedAsync()
    {
        _dataLoader = new AllocationsDataLoader(StatsService, RateLimitApi);

        try
        {
            _pools = await StatsService.GetResourcePoolStatsAsync();
            var clients = await ClientService.GetAllAsync();
            _allClients = clients;
            _clientOptions = clients.Select(c => new NamedItem(c.Id, c.Name)).ToList();

            _poolOptions = new List<NamedItem> { new(AllocationsLoadContext.AllPoolsId, "All Pools") }
                .Concat(_pools.Select(p => new NamedItem(p.ResourcePoolId, p.Name)))
                .ToList();
            _selectedPoolId = AllocationsLoadContext.AllPoolsId;
        }
        catch (HttpRequestException ex)
        {
            _error = $"Unable to connect to the API: {ex.Message}";
            _chartLoading = false;
        }

        _polling = new PagePollingLifecycle(JS, InvokeAsync, LoadDataAsync);
        _polling.Start();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _chartJs = await JS.InvokeAsync<IJSObjectReference>("import", "./js/chart.js");
            _chartSelfRef = DotNetObjectReference.Create(this);
            await _chartJs.InvokeVoidAsync("register", _chartSelfRef);

            var chartWidth = await _chartJs.InvokeAsync<int>("getChartCardWidth");
            _chartBucketCount = ChartBucketAggregator.GetBucketCountForWidth(chartWidth);

            if (_error is null)
            {
                await LoadDataAsync();
            }

            _chartLoading = false;
            StateHasChanged();
        }

        if (firstRender && _polling is not null)
        {
            await _polling.RegisterVisibilityAsync();
        }
    }

    [JSInvokable]
    public async Task OnChartResize() => await UpdateChartBucketCountAsync(reloadWhenChanged: true);

    private async Task UpdateChartBucketCountAsync(bool reloadWhenChanged)
    {
        if (_chartJs is null)
        {
            return;
        }

        var chartWidth = await _chartJs.InvokeAsync<int>("getChartCardWidth");
        var bucketCount = ChartBucketAggregator.GetBucketCountForWidth(chartWidth);
        if (bucketCount == _chartBucketCount)
        {
            return;
        }

        _chartBucketCount = bucketCount;
        if (reloadWhenChanged)
        {
            await LoadDataAsync();
            StateHasChanged();
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

    public async ValueTask DisposeAsync()
    {
        if (_chartJs is not null)
        {
            await _chartJs.InvokeVoidAsync("unregister");
            await _chartJs.DisposeAsync();
        }

        _chartSelfRef?.Dispose();

        if (_polling is not null)
        {
            await _polling.DisposeAsync();
        }
    }
}
