using ClientManager.AdminUI.Models.Allocations;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Services.ChartData;
using ClientManager.Shared.Models.Entities;
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

    private bool _loading;
    private string? _error;
    private PagePollingLifecycle? _polling;
    private AllocationsDataLoader? _dataLoader;

    private TimeRangePreset _timeRange = TimeRangePreset.Default;
    private AxisScaleType _axisScaleType = AxisScaleType.Linear;
    private string _selectedMetric = AllocationsChartSection.ActiveAllocationsMetric;

    private List<TargetChartData> _targetCharts = [];
    private List<AllocationClientRow> _clientDetailRows = [];
    private AllocationsClientDetailGrid? _clientDetailGrid;
    private AllocationsPoolSummaryGrid? _allPoolsGrid;

    private bool IsAccessMetric => _selectedMetric == AllocationsChartSection.AccessRequestsMetric;

    protected override async Task OnInitializedAsync()
    {
        _dataLoader = new AllocationsDataLoader(StatsService, RateLimitApi);
        _loading = true;

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

            await LoadDataAsync();
        }
        catch (HttpRequestException ex)
        {
            _error = $"Unable to connect to the API: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }

        _polling = new PagePollingLifecycle(JS, InvokeAsync, LoadDataAsync);
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

    public async ValueTask DisposeAsync()
    {
        if (_polling is not null)
        {
            await _polling.DisposeAsync();
        }
    }
}
