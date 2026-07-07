using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Models.Dashboard;
using ClientManager.AdminUI.Services.ChartData;
using ClientManager.AdminUI.Utils;

namespace ClientManager.AdminUI.Components.Pages.Dashboard;

public partial class Dashboard
{
    private int _chartLoadTicket;
    private bool _chartInitComplete;
    private bool _disposed;

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

    private async Task OnFilterChanged(object? _)
    {
        if (!_chartInitComplete)
        {
            return;
        }

        _filterTargets = _selectedFilterType == "Service" ? _allServices : _allPools;

        if (_filterTargets.Count > 0 && (_selectedTargetId is null || !_filterTargets.Any(t => t.Id == _selectedTargetId)))
        {
            _selectedTargetId = _filterTargets[0].Id;
        }

        SyncUrl();
        await LoadChartDataWithSkeletonAsync();
    }

    private async Task LoadChartDataWithSkeletonAsync()
    {
        if (_disposed)
        {
            return;
        }

        var ticket = System.Threading.Interlocked.Increment(ref _chartLoadTicket);
        _chartLoading = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            await LoadChartDataCoreAsync(ticket);
        }
        finally
        {
            if (!_disposed && ticket == _chartLoadTicket)
            {
                _chartLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private Task LoadChartDataAsync()
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        var ticket = System.Threading.Interlocked.Increment(ref _chartLoadTicket);
        return LoadChartDataCoreAsync(ticket);
    }

    private DashboardChartLoadContext CreateChartLoadContext() => new()
    {
        SelectedFilterType = _selectedFilterType,
        SelectedTargetId = _selectedTargetId,
        SelectedClientIds = _selectedClientIds,
        TimeRange = _timeRange,
        FilterTargets = _filterTargets,
        AllServices = _allServices,
        AllPools = _allPools,
        Clients = _clients,
        BucketCount = _chartBucketCount,
        ShowDeniedBreakdown = _showDeniedBreakdown
    };

    private async Task LoadChartDataCoreAsync(int ticket)
    {
        if (_disposed || _selectedTargetId is null || _chartLoader is null)
        {
            return;
        }

        try
        {
            var (charts, donut) = await _chartLoader.LoadAsync(CreateChartLoadContext());

            if (_disposed || ticket != _chartLoadTicket)
            {
                return;
            }

            _targetCharts = charts;
            _donutData = donut;
            _donutDataGeneration++;
            _chartError = null;
        }
        catch (HttpRequestException ex)
        {
            if (_disposed || ticket != _chartLoadTicket)
            {
                return;
            }

            _chartError = Errors.Format("Api.UnableToConnect", ex);
        }
    }
}
