using ClientManager.AdminUI.Models.Allocations;
using ClientManager.AdminUI.Services.ChartData;
using ClientManager.AdminUI.Utils;

namespace ClientManager.AdminUI.Components.Pages.Allocations;

public partial class ActiveAllocations
{
    private int _loadVersion;
    private bool _pollingOverride;

    private AllocationsLoadContext CreateLoadContext() => new()
    {
        SelectedPoolId = _selectedPoolId,
        SelectedClientIds = _selectedClientIds,
        TimeRange = _timeRange,
        IsAccessMetric = IsAccessMetric,
        AllClients = _allClients,
        BucketCount = _chartBucketCount,
        KnownPools = _pools
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

    private async Task ApplyLoadResult(AllocationsLoadResult result)
    {
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
    }
}
