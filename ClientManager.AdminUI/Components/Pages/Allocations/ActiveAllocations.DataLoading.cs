using ClientManager.AdminUI.Models.Allocations;
using ClientManager.AdminUI.Services.ChartData;

namespace ClientManager.AdminUI.Components.Pages.Allocations;

public partial class ActiveAllocations
{
    private int _loadVersion;

    private async Task OnTimeRangeChanged(ChartTimeRange range)
    {
        _timeRange = range;
        SyncUrl();
        await LoadChartDataWithSkeletonAsync();
    }

    private async Task OnMetricChanged()
    {
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
            var context = new AllocationsLoadContext
            {
                SelectedPoolId = _selectedPoolId,
                SelectedClientIds = _selectedClientIds,
                TimeRange = _timeRange,
                IsAccessMetric = IsAccessMetric,
                AllClients = _allClients,
                BucketCount = _chartBucketCount
            };

            var result = await _dataLoader.LoadAsync(context);

            if (loadVersion != _loadVersion)
            {
                return;
            }

            _targetCharts = result.Charts;
            _clientDetailRows = result.ClientRows;
            _pools = result.Pools;
            _poolSummaryRows = result.PoolSummaryRows;

            if (_clientDetailGrid is not null)
            {
                await _clientDetailGrid.ReloadAsync(InvokeAsync);
            }

            if (_allPoolsGrid is not null)
            {
                await _allPoolsGrid.ReloadAsync(InvokeAsync);
            }

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
}
