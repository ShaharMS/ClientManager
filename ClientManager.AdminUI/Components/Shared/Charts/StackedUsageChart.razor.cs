using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Utils;
using ClientManager.AdminUI.Resources;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace ClientManager.AdminUI.Components.Shared.Charts;

public partial class StackedUsageChart : ComponentBase, IAsyncDisposable
{
    [Inject] private EntityColorService Colors { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResources> Localizer { get; set; } = null!;

    [Parameter] public IReadOnlyList<TargetChartData> Charts { get; set; } = [];
    [Parameter] public AxisScaleType AxisScale { get; set; }
    [Parameter] public int HeightPx { get; set; } = 400;
    [Parameter] public string? CapLineTitle { get; set; }
    [Parameter] public bool Loading { get; set; }
    [Parameter] public bool ShowTitles { get; set; } = true;
    [Parameter] public string? EmptyMessage { get; set; }
    [Parameter] public string? TitleSuffix { get; set; }

    private IJSObjectReference? _module;
    private string _renderToken = Guid.NewGuid().ToString("N");
    private bool _pendingUpdate;

    private string ResolvedCapLineTitle => CapLineTitle ?? Localizer["Common.Cap"];
    private string ResolvedEmptyMessage => EmptyMessage ?? Localizer["Charts.NoUsageData"];

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Loading || Charts.Count == 0)
        {
            return;
        }

        if (!_pendingUpdate && !firstRender)
        {
            return;
        }

        _module ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/stacked-chart.js?v=7");
        await RenderChartsAsync();
        _pendingUpdate = false;
    }

    protected override void OnParametersSet()
    {
        if (!Loading && Charts.Count > 0)
        {
            _pendingUpdate = true;
        }
    }

    private async Task RenderChartsAsync()
    {
        if (_module is null || Loading)
        {
            return;
        }

        foreach (var (targetChart, index) in Charts.Select((chart, index) => (chart, index)))
        {
            var canvasId = GetCanvasId(index);
            var config = BuildConfig(targetChart);
            await _module.InvokeVoidAsync("createOrUpdate", canvasId, config);
        }

        _pendingUpdate = false;
    }

    private object BuildConfig(TargetChartData targetChart)
    {
        var allSeries = ChartSeriesTransform.GetChartSeries(targetChart.ClientSeries, AxisScale).ToList();

        var labels = allSeries.FirstOrDefault(s => s.Points.Count > 0)?.Points.Select(point => point.Label).ToArray()
            ?? targetChart.CapSeries.Select(point => point.Label).ToArray();

        var series = allSeries.Select(clientArea =>
        {
            var color = Colors.GetSeriesColor(clientArea.ClientId);
            return new
            {
                name = clientArea.ClientName,
                points = clientArea.Points.Select(point => point.Value).ToArray(),
                originalValues = clientArea.Points.Select(point => point.OriginalValue != 0 ? point.OriginalValue : point.Value).ToArray(),
                fillColor = color,
                strokeColor = color,
                hidden = clientArea.Hidden
            };
        }).ToArray();

        var capPoints = ChartSeriesTransform.TransformPoints(targetChart.CapSeries, AxisScale);

        return new
        {
            labels,
            series,
            capLine = new
            {
                title = ResolvedCapLineTitle,
                points = capPoints.Select(point => point.Value).ToArray(),
                originalValues = capPoints.Select(point => point.OriginalValue != 0 ? point.OriginalValue : point.Value).ToArray()
            },
            axisScale = AxisScale == AxisScaleType.Logarithmic ? "logarithmic" : "linear",
            animate = false
        };
    }

    private string GetCanvasId(int index) => $"stacked-chart-{_renderToken}-{index}";

    private string GetLegendId(int index) => $"{GetCanvasId(index)}-legend";

    private string FormatTitle(TargetChartData chart) =>
        TitleSuffix is null ? chart.TargetName : $"{chart.TargetName} - {TitleSuffix}";

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            for (var i = 0; i < Charts.Count; i++)
            {
                await _module.InvokeVoidAsync("destroy", GetCanvasId(i));
            }

            await _module.DisposeAsync();
        }
    }
}
