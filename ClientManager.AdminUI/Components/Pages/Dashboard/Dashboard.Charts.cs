using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Dashboard;
using ClientManager.AdminUI.Services.ChartData;

namespace ClientManager.AdminUI.Components.Pages.Dashboard;

public partial class Dashboard
{
    private int _chartLoadVersion;

    private async Task OnTimeRangeChanged(ChartTimeRange range)
    {
        _timeRange = range;
        await LoadChartDataAsync();
    }

    private async Task OnFilterChanged(object? _)
    {
        _filterTargets = _selectedFilterType == "Service" ? _allServices : _allPools;

        if (_filterTargets.Count > 0 && (_selectedTargetId is null || !_filterTargets.Any(t => t.Id == _selectedTargetId)))
        {
            _selectedTargetId = _filterTargets[0].Id;
        }

        await LoadChartDataAsync();
    }

    private async Task LoadChartDataAsync()
    {
        if (_selectedTargetId is null || _chartLoader is null)
        {
            return;
        }

        var loadVersion = System.Threading.Interlocked.Increment(ref _chartLoadVersion);

        try
        {
            var context = new DashboardChartLoadContext
            {
                SelectedFilterType = _selectedFilterType,
                SelectedTargetId = _selectedTargetId,
                SelectedClientIds = _selectedClientIds,
                TimeRange = _timeRange,
                FilterTargets = _filterTargets,
                AllServices = _allServices,
                AllPools = _allPools,
                Clients = _clients,
                BucketCount = _chartBucketCount
            };

            var (charts, donut) = await _chartLoader.LoadAsync(context);

            if (loadVersion != _chartLoadVersion)
            {
                return;
            }

            _targetCharts = charts;
            _perClientUsage = donut;
        }
        catch (HttpRequestException)
        {
            if (loadVersion != _chartLoadVersion)
            {
                return;
            }
        }
    }
}
