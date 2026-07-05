using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Monitor;
using ClientManager.AdminUI.Utils;

namespace ClientManager.AdminUI.Components.Pages.Monitor;

public partial class Monitor
{
    private ChartTimeRange _defaultTimeRange = ChartTimeRange.FromPreset(TimeRangePreset.Default);
    private AxisScaleType _defaultAxisScale = AxisScaleType.Linear;
    private string _defaultPollKey = PollingIntervalPreset.Default.Key;
    private string _pollingKey = PollingIntervalPreset.Default.Key;
    private string? _initialTimeRangeKey;
    private string? _initialPollingKey;
    private AxisScaleType? _initialAxisScale;
    private DateTime? _initialCustomFromUtc;
    private DateTime? _initialCustomToUtc;

    private void HydrateFromUrl()
    {
        var query = UrlQuery.Parse();
        UrlQuery.SuppressWrite = true;
        try
        {
            if (query.TryGetValue(QueryParamParsers.Service, out var service)
                && _serviceOptions.Any(s => s.Id == service))
            {
                _selectedServiceId = service;
            }

            var validClientIds = _clientOptions.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
            _selectedClientIds = QueryParamParsers.ParseClientIds(
                query.GetValueOrDefault(QueryParamParsers.Clients), validClientIds);

            var parsedRange = QueryParamParsers.TryParseTimeRange(
                query.GetValueOrDefault(QueryParamParsers.Range),
                query.GetValueOrDefault(QueryParamParsers.From),
                query.GetValueOrDefault(QueryParamParsers.To));
            if (parsedRange is not null)
            {
                _timeRange = parsedRange;
            }

            if (QueryParamParsers.TryParseScale(query.GetValueOrDefault(QueryParamParsers.Scale)) is { } parsedScale)
            {
                _axisScaleType = parsedScale;
            }

            if (QueryParamParsers.TryParsePoll(query.GetValueOrDefault(QueryParamParsers.Poll)) is { } parsedPoll)
            {
                _pollingKey = parsedPoll.Key;
            }

            SetInitialChartDropdownState(query);
        }
        finally
        {
            UrlQuery.SuppressWrite = false;
        }
    }

    private void SetInitialChartDropdownState(IReadOnlyDictionary<string, string> query)
    {
        if (query.ContainsKey(QueryParamParsers.Range)
            || query.ContainsKey(QueryParamParsers.From)
            || query.ContainsKey(QueryParamParsers.To))
        {
            _initialTimeRangeKey = QueryParamParsers.TryGetRangeKey(_timeRange);
            if (_timeRange.Mode == ChartTimeRangeMode.Custom)
            {
                _initialTimeRangeKey = "custom";
                _initialCustomFromUtc = _timeRange.CustomFromUtc;
                _initialCustomToUtc = _timeRange.CustomToUtc;
            }
        }
        else
        {
            _initialTimeRangeKey = null;
            _initialCustomFromUtc = null;
            _initialCustomToUtc = null;
        }

        _initialPollingKey = query.ContainsKey(QueryParamParsers.Poll) ? _pollingKey : null;
        _initialAxisScale = query.ContainsKey(QueryParamParsers.Scale) ? _axisScaleType : null;
    }

    private Dictionary<string, string?> BuildQueryParams()
    {
        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (_selectedServiceId != MonitorLoadContext.AllServicesId)
        {
            query[QueryParamParsers.Service] = _selectedServiceId;
        }

        QueryParamParsers.WriteTimeRange(query, _timeRange, _defaultTimeRange);
        QueryParamParsers.WriteIfPresent(
            query, QueryParamParsers.Scale, QueryParamParsers.FormatScale(_axisScaleType, _defaultAxisScale));
        QueryParamParsers.WriteIfPresent(
            query, QueryParamParsers.Poll, QueryParamParsers.FormatPoll(_pollingKey, _defaultPollKey));
        QueryParamParsers.WriteIfPresent(
            query, QueryParamParsers.Clients, QueryParamParsers.FormatClientIds(_selectedClientIds));

        return query;
    }

    private void SyncUrl() => UrlQuery.Replace(BuildQueryParams());
}
