using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Monitor;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Services.ChartData;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Models.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace ClientManager.AdminUI.Components.Pages.Monitor;

public partial class Monitor : ComponentBase, IAsyncDisposable
{
    [Inject] private StatisticsApiService StatsService { get; set; } = null!;
    [Inject] private ServiceApiService ServiceService { get; set; } = null!;
    [Inject] private ClientApiService ClientService { get; set; } = null!;
    [Inject] private GlobalRateLimitApiService RateLimitApi { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResources> Localizer { get; set; } = null!;
    [Inject] private ApiErrorLocalizer Errors { get; set; } = null!;
    [Inject] private DeniedBreakdownFormatter DeniedBreakdownFormatter { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    private List<Service> _allServices = [];
    private List<NamedItem> _serviceOptions = [];
    private List<ClientOption> _clientOptions = [];
    private List<ClientConfiguration> _allClients = [];
    private string _selectedServiceId = MonitorLoadContext.AllServicesId;
    private IEnumerable<string>? _selectedClientIds;

    private bool _chartLoading = true;
    private string? _error;
    private PagePollingLifecycle? _polling;
    private MonitorDataLoader? _dataLoader;

    private List<TargetChartData> _targetCharts = [];
    private List<MonitorClientRow> _clientRows = [];
    private List<ServiceSummaryRow> _allServiceStats = [];
    private MonitorClientGrid? _clientGrid;
    private MonitorServicesGrid? _servicesGrid;

    private bool ShowDeniedBreakdown => DeniedBreakdownFormatter.ShowBreakdown(_selectedClientIds);

    private ChartTimeRange _timeRange = ChartTimeRange.FromPreset(TimeRangePreset.Default);
    private AxisScaleType _axisScaleType = AxisScaleType.Linear;
    private int _chartBucketCount = ChartBucketAggregator.DefaultBucketCount;
    private IJSObjectReference? _chartJs;
    private DotNetObjectReference<Monitor>? _chartSelfRef;

    protected override async Task OnInitializedAsync()
    {
        _dataLoader = new MonitorDataLoader(StatsService, RateLimitApi, Localizer);

        try
        {
            _allServices = await ServiceService.GetAllAsync();
            var clients = await ClientService.GetAllAsync();
            _allClients = clients;
            _clientOptions = clients.Select(c => new ClientOption(c.Id, c.Name)).ToList();

            _serviceOptions = new List<NamedItem> { new(MonitorLoadContext.AllServicesId, Localizer["Pages.Monitor.Chart.AllServices"]) }
                .Concat(_allServices.Select(s => new NamedItem(s.Id, s.Name))).ToList();
            _selectedServiceId = MonitorLoadContext.AllServicesId;
        }
        catch (HttpRequestException ex)
        {
            _error = Errors.Format("Pages.Monitor.LoadDataError", ex);
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
