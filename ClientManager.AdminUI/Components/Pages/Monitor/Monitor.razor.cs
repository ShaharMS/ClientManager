using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Monitor;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Services.ChartData;
using ClientManager.Shared.Models.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ClientManager.AdminUI.Components.Pages.Monitor;

public partial class Monitor : ComponentBase, IAsyncDisposable
{
    [Inject] private StatisticsApiService StatsService { get; set; } = null!;
    [Inject] private ServiceApiService ServiceService { get; set; } = null!;
    [Inject] private ClientApiService ClientService { get; set; } = null!;
    [Inject] private GlobalRateLimitApiService RateLimitApi { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    private List<Service> _allServices = [];
    private List<NamedItem> _serviceOptions = [];
    private List<ClientOption> _clientOptions = [];
    private List<ClientConfiguration> _allClients = [];
    private string _selectedServiceId = MonitorLoadContext.AllServicesId;
    private IEnumerable<string>? _selectedClientIds;

    private bool _loading;
    private string? _error;
    private PagePollingLifecycle? _polling;
    private MonitorDataLoader? _dataLoader;

    private List<TargetChartData> _targetCharts = [];
    private List<MonitorClientRow> _clientRows = [];
    private List<ServiceSummaryRow> _allServiceStats = [];
    private MonitorClientGrid? _clientGrid;
    private MonitorServicesGrid? _servicesGrid;

    private TimeRangePreset _timeRange = TimeRangePreset.Default;
    private AxisScaleType _axisScaleType = AxisScaleType.Linear;

    protected override async Task OnInitializedAsync()
    {
        _dataLoader = new MonitorDataLoader(StatsService, RateLimitApi);
        _loading = true;

        try
        {
            _allServices = await ServiceService.GetAllAsync();
            var clients = await ClientService.GetAllAsync();
            _allClients = clients;
            _clientOptions = clients.Select(c => new ClientOption(c.Id, c.Name)).ToList();

            _serviceOptions = new List<NamedItem> { new(MonitorLoadContext.AllServicesId, "All Services") }
                .Concat(_allServices.Select(s => new NamedItem(s.Id, s.Name))).ToList();
            _selectedServiceId = MonitorLoadContext.AllServicesId;

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
