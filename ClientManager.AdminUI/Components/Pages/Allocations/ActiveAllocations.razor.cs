using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Allocations;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Services.ChartData;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace ClientManager.AdminUI.Components.Pages.Allocations;

public partial class ActiveAllocations : ComponentBase, IAsyncDisposable
{
    [Inject] private StatisticsApiService StatsService { get; set; } = null!;
    [Inject] private ClientApiService ClientService { get; set; } = null!;
    [Inject] private GlobalRateLimitApiService RateLimitApi { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResources> Localizer { get; set; } = null!;
    [Inject] private ApiErrorLocalizer Errors { get; set; } = null!;
    [Inject] private DeniedBreakdownFormatter DeniedBreakdownFormatter { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private UrlQuerySync UrlQuery { get; set; } = null!;
    [Inject] private UserPreferencesService PreferencesService { get; set; } = null!;

    private List<MetricOption> MetricOptions =>
    [
        new(AllocationsChartSection.ActiveAllocationsMetric, Localizer["Pages.Allocations.Metric.ActiveAllocations"]),
        new(AllocationsChartSection.AccessRequestsMetric, Localizer["Pages.Allocations.Metric.AccessRequests"])
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

    private bool ShowDeniedBreakdown => DeniedBreakdownFormatter.ShowBreakdown(_selectedClientIds);

    private DeniedViewMode AllocationDeniedViewMode =>
        IsAccessMetric ? DeniedViewMode.RateLimitDenied : DeniedViewMode.CapacityDenied;

    private bool ShowPoolColumnInClientGrid => _selectedPoolId == AllocationsLoadContext.AllPoolsId;

    private string ClientDetailTitle =>
        _selectedPoolId == AllocationsLoadContext.AllPoolsId
            ? IsAccessMetric ? Localizer["Pages.Allocations.ClientAccessDetailTitle"] : Localizer["Pages.Allocations.ClientDetailTitle"]
            : $"{SelectedPoolName} - {(IsAccessMetric ? Localizer["Pages.Allocations.ClientAccessDetailTitle"] : Localizer["Pages.Allocations.ClientDetailTitle"])}";

    private string SelectedPoolName =>
        _selectedPoolId == AllocationsLoadContext.AllPoolsId || string.IsNullOrEmpty(_selectedPoolId)
            ? Localizer["Pages.Allocations.Target.AllPools"]
            : _poolOptions.FirstOrDefault(p => p.Id == _selectedPoolId)?.Name ?? Localizer["Pages.Allocations.Target.AllPools"];

    private string ChartTitle =>
        $"{SelectedPoolName} - {(IsAccessMetric ? Localizer["Pages.Allocations.Metric.AccessRequests"] : Localizer["Pages.Allocations.Metric.ActiveAllocations"])}";

    protected override async Task OnInitializedAsync()
    {
        _dataLoader = new AllocationsDataLoader(StatsService, RateLimitApi, Localizer);

        try
        {
            _pools = await StatsService.GetResourcePoolStatsAsync();
            var clients = await ClientService.GetAllAsync();
            _allClients = clients;
            _clientOptions = clients.Select(c => new NamedItem(c.Id, c.Name)).ToList();

            _poolOptions = new List<NamedItem> { new(AllocationsLoadContext.AllPoolsId, Localizer["Pages.Allocations.Target.AllPools"]) }
                .Concat(_pools.Select(p => new NamedItem(p.ResourcePoolId, p.Name)))
                .ToList();

            HydrateFromUrl();
        }
        catch (HttpRequestException ex)
        {
            _error = Errors.Format("Pages.Allocations.LoadDataError", ex);
            _chartLoading = false;
        }

        _polling = new PagePollingLifecycle(JS, InvokeAsync, LoadDataAsync);
        var pollInterval = PollingIntervalPreset.FindByKey(_pollingKey)?.Interval;
        _polling.Start(pollInterval);
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
        _pollingKey = preset.Key;
        _polling?.SetInterval(preset.Interval);
        SyncUrl();
        return Task.CompletedTask;
    }

    private Task OnAxisScaleChanged(AxisScaleType scale)
    {
        _axisScaleType = scale;
        SyncUrl();
        StateHasChanged();
        return Task.CompletedTask;
    }

    private Task OnPoolChangedAsync(string value)
    {
        _selectedPoolId = value;
        return Task.CompletedTask;
    }

    private Task OnClientsChangedAsync(IEnumerable<string>? value)
    {
        _selectedClientIds = value;
        return Task.CompletedTask;
    }

    private Task OnMetricChangedAsync(string value)
    {
        _selectedMetric = value;
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

        UrlQuery.Dispose();
    }
}
