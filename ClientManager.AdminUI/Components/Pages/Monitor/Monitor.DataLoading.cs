using ClientManager.AdminUI.Models.Monitor;
using ClientManager.AdminUI.Services.ChartData;
using ClientManager.AdminUI.Utils;

namespace ClientManager.AdminUI.Components.Pages.Monitor;

public partial class Monitor
{
    private int _loadVersion;
    private bool _pollingOverride;

    private MonitorLoadContext CreateLoadContext() => new()
    {
        SelectedServiceId = _selectedServiceId,
        SelectedClientIds = _selectedClientIds,
        TimeRange = _timeRange,
        AllServices = _allServices,
        AllClients = _allClients,
        BucketCount = _chartBucketCount
    };

    private async Task OnTimeRangeChanged(ChartTimeRange range)
    {
        _timeRange = range;
        if (!_pollingOverride)
        {
            var suggested = ChartPollingHelper.SuggestForRange(range);
            _pollingKey = suggested.Key;
            _polling?.SetInterval(suggested.Interval);
        }

        SyncUrl();
        await LoadChartDataWithSkeletonAsync();
    }

    private async Task OnFilterChanged()
    {
        SyncUrl();
        await LoadChartDataWithSkeletonAsync();
    }

    private async Task LoadChartDataWithSkeletonAsync()
    {
        _chartLoading = true;
        StateHasChanged();
        await LoadDataAsync();
        _chartLoading = false;
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
            var result = await _dataLoader.LoadAsync(CreateLoadContext());

            if (loadVersion != _loadVersion)
            {
                return;
            }

            await ApplyLoadResult(result);
            _error = null;
        }
        catch (Exception ex)
        {
            if (loadVersion != _loadVersion)
            {
                return;
            }

            _error = ex is HttpRequestException http
                ? $"Unable to load data: {http.Message}"
                : $"Unable to load data: {ex.Message}";
        }
    }

    private async Task TryRebuildChartsFromCacheAsync()
    {
        if (_dataLoader is null)
        {
            return;
        }

        if (!_dataLoader.TryRebuildFromCache(CreateLoadContext(), out var result))
        {
            await LoadDataAsync();
            return;
        }

        await ApplyLoadResult(result);
        await InvokeAsync(StateHasChanged);
    }

    private async Task ApplyLoadResult(MonitorLoadResult result)
    {
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
    }
}
