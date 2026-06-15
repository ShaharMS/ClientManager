using ClientManager.AdminUI.Models.Monitor;
using ClientManager.AdminUI.Services.ChartData;

namespace ClientManager.AdminUI.Components.Pages.Monitor;

public partial class Monitor
{
    private int _loadVersion;

    private async Task OnTimeRangeChanged(ChartTimeRange range)
    {
        _timeRange = range;
        await LoadDataAsync();
        StateHasChanged();
    }

    private async Task OnFilterChanged()
    {
        _loading = true;
        StateHasChanged();
        await LoadDataAsync();
        _loading = false;
        StateHasChanged();
    }

    private async Task LoadDataAsync()
    {
        if (_dataLoader is null)
        {
            return;
        }

        var loadVersion = System.Threading.Interlocked.Increment(ref _loadVersion);

        try
        {
            var context = new MonitorLoadContext
            {
                SelectedServiceId = _selectedServiceId,
                SelectedClientIds = _selectedClientIds,
                TimeRange = _timeRange,
                AllServices = _allServices,
                AllClients = _allClients,
                BucketCount = _chartBucketCount
            };

            var result = await _dataLoader.LoadAsync(context);

            if (loadVersion != _loadVersion)
            {
                return;
            }

            _targetCharts = result.Charts;
            _clientRows = result.ClientRows;
            _allServiceStats = result.ServiceStats;

            if (_clientGrid is not null)
            {
                await _clientGrid.ReloadAsync(InvokeAsync);
            }

            if (_servicesGrid is not null)
            {
                await _servicesGrid.ReloadAsync(InvokeAsync);
            }

            _error = null;
        }
        catch (HttpRequestException ex)
        {
            if (loadVersion != _loadVersion)
            {
                return;
            }

            _error = $"Unable to load data: {ex.Message}";
        }
    }
}
