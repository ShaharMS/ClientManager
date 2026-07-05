using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Dashboard;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Services.ChartData;
using ClientManager.Shared.Models.Responses;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace ClientManager.AdminUI.Components.Pages.Dashboard;

public partial class Dashboard : ComponentBase, IAsyncDisposable
{
    [Inject] private StatisticsApiService StatsService { get; set; } = null!;
    [Inject] private ClientApiService ClientService { get; set; } = null!;
    [Inject] private ServiceApiService ServiceService { get; set; } = null!;
    [Inject] private ResourcePoolApiService PoolService { get; set; } = null!;
    [Inject] private GlobalRateLimitApiService RateLimitApi { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResources> Localizer { get; set; } = null!;
    [Inject] private ApiErrorLocalizer Errors { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private UrlQuerySync UrlQuery { get; set; } = null!;
    [Inject] private UserPreferencesService PreferencesService { get; set; } = null!;

    private bool _loading = true;
    private bool _chartLoading = true;
    private string? _error;
    private string? _chartError;
    private SystemOverviewResponse? _overview;

    private string _globalUsage = "-";
    private int _acquisitionPct;

    private string _selectedFilterType = "Service";
    private string? _selectedTargetId;
    private IEnumerable<string>? _selectedClientIds;
    private string? _tableSearch;

    private PagePollingLifecycle? _polling;
    private DashboardChartDataLoader? _chartLoader;

    private List<FilterOption> _filterTypes = [];

    protected override void OnInitialized()
    {
        _filterTypes =
        [
            new(Localizer["Pages.Dashboard.FilterType.Service"], "Service"),
            new(Localizer["Pages.Dashboard.FilterType.ResourcePool"], "ResourcePool")
        ];
    }

    private List<NamedItem> _filterTargets = [];
    private List<NamedItem> _allServices = [];
    private List<NamedItem> _allPools = [];
    private List<NamedItem> _clients = [];

    private List<TargetChartData> _targetCharts = [];
    private DashboardDonutData _donutData = new([], []);
    private int _donutDataGeneration;

    private ChartTimeRange _timeRange = ChartTimeRange.FromPreset(TimeRangePreset.Default);
    private AxisScaleType _axisScaleType = AxisScaleType.Linear;
    private int _chartBucketCount = ChartBucketAggregator.DefaultBucketCount;
    private IJSObjectReference? _chartJs;
    private DotNetObjectReference<Dashboard>? _chartSelfRef;

    private List<ClientSummaryTableRow> _clientSummaries = [];
    private List<ClientSummaryTableRow> _filteredClientSummaries = [];

    protected override async Task OnInitializedAsync()
    {
        _chartLoader = new DashboardChartDataLoader(StatsService, PoolService, RateLimitApi, Localizer);

        try
        {
            var overviewTask = StatsService.GetOverviewAsync();
            var servicesTask = ServiceService.GetAllAsync();
            var poolsTask = PoolService.GetAllAsync();
            var clientsTask = ClientService.GetAllAsync();
            var globalUsageTask = StatsService.GetGlobalUsageStatsAsync();
            var summariesTask = StatsService.GetClientSummariesAsync();

            await Task.WhenAll(overviewTask, servicesTask, poolsTask, clientsTask, globalUsageTask, summariesTask);

            _overview = await overviewTask;

            var services = await servicesTask;
            var pools = await poolsTask;
            var clients = await clientsTask;

            _clients = clients.Select(c => new NamedItem(c.Id, c.Name)).ToList();
            _allServices = new List<NamedItem> { new(DashboardChartLoadContext.AllTargetsId, Localizer["Pages.Dashboard.Target.AllServices"]) }
                .Concat(services.Select(s => new NamedItem(s.Id, s.Name))).ToList();
            _allPools = new List<NamedItem> { new(DashboardChartLoadContext.AllTargetsId, Localizer["Pages.Dashboard.Target.AllResourcePools"]) }
                .Concat(pools.Select(p => new NamedItem(p.Id, p.Name))).ToList();
            _filterTargets = _allServices;

            var globalUsage = await globalUsageTask;
            if (globalUsage is not null)
            {
                _globalUsage = globalUsage.RequestsPerMinute > 0
                    ? globalUsage.RequestsPerMinute.ToString("N0")
                    : "-";
                _acquisitionPct = (int)Math.Round(globalUsage.AcquisitionPercentage);
            }

            var summaries = await summariesTask;
            _clientSummaries = summaries.Select(r => new ClientSummaryTableRow(
                r.ClientId, r.DisplayName, r.AccessibleServices,
                r.TotalRateLimitRequests, r.AccessiblePools,
                r.TotalAccessibleSlots
            )).ToList();
            _filteredClientSummaries = _clientSummaries;

            if (_filterTargets.Count > 0 && _selectedTargetId is null)
            {
                _selectedTargetId = _filterTargets[0].Id;
            }

            HydrateFromUrl();
        }
        catch (HttpRequestException ex)
        {
            _error = Errors.Format("Api.UnableToConnect", ex);
        }
        finally
        {
            _loading = false;
        }

        _polling = new PagePollingLifecycle(JS, InvokeAsync, RefreshDataAsync);
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
                await LoadChartDataAsync();
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
            await LoadChartDataAsync();
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

        SyncUrlDebounced();
    }

    private Task OnFilterTypeChangedAsync(string value)
    {
        _selectedFilterType = value;
        return Task.CompletedTask;
    }

    private Task OnTargetChangedAsync(string? value)
    {
        _selectedTargetId = value;
        return Task.CompletedTask;
    }

    private Task OnClientsChangedAsync(IEnumerable<string>? value)
    {
        _selectedClientIds = value;
        return Task.CompletedTask;
    }

    private Task OnTableSearchTextChangedAsync(string? value)
    {
        _tableSearch = value;
        OnTableSearchChanged();
        return Task.CompletedTask;
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
        }
        catch (HttpRequestException ex)
        {
            _chartError = Errors.Format("Api.UnableToConnect", ex);
        }
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
