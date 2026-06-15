using ClientManager.AdminUI.Models.Allocations;
using ClientManager.AdminUI.Services.ChartData;

namespace ClientManager.AdminUI.Components.Pages.Allocations;

public partial class ActiveAllocations
{
    private int _loadVersion;

    private async Task OnTimeRangeChanged(ChartTimeRange range)
    {
        _timeRange = range;
        await LoadDataAsync();
    }

    private async Task OnMetricChanged()
    {
        _loading = true;
        StateHasChanged();
        await LoadDataAsync();
        _loading = false;
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
