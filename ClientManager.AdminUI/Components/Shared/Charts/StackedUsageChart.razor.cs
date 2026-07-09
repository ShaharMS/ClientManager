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
    private DotNetObjectReference<StackedUsageChart>? _resizeRef;
    private string _renderToken = Guid.NewGuid().ToString("N");
    private bool _pendingUpdate;
    private bool _destroyPending;
    private bool _resizing;
    private bool _resizeRegistered;
    private bool _disposed;

    private string ResolvedCapLineTitle => CapLineTitle ?? Localizer["Common.Cap"];
    private string ResolvedEmptyMessage => EmptyMessage ?? Localizer["Charts.NoUsageData"];

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed)
        {
            return;
        }

        if (_destroyPending)
        {
            _destroyPending = false;
            await DestroyChartsAsync();
            return;
        }

        if (Loading || Charts.Count == 0)
        {
            return;
        }

        _module ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/stacked-chart.js?v=11");

        if (!_resizeRegistered)
        {
            _resizeRef ??= DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("registerResize", _resizeRef);
            _resizeRegistered = true;
        }

        if (_resizing)
        {
            return;
        }

        if (!_pendingUpdate && !firstRender)
        {
            return;
        }

        await RenderChartsAsync();
        _pendingUpdate = false;
    }

    protected override void OnParametersSet()
    {
        if (Loading)
        {
            _destroyPending = true;
            return;
        }

        if (Charts.Count > 0)
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
        var showCapLine = capPoints.Any(point => point.Value > 0);

        return new
        {
            labels,
            series,
            capLine = showCapLine
                ? new
                {
                    title = ResolvedCapLineTitle,
                    points = capPoints.Select(point => point.Value).ToArray(),
                    originalValues = capPoints.Select(point => point.OriginalValue != 0 ? point.OriginalValue : point.Value).ToArray()
                }
                : null,
            axisScale = AxisScale == AxisScaleType.Logarithmic ? "logarithmic" : "linear",
            animate = false
        };
    }

    private string GetCanvasId(int index) => $"stacked-chart-{_renderToken}-{index}";

    private string GetLegendId(int index) => $"{GetCanvasId(index)}-legend";

    private string FormatTitle(TargetChartData chart) =>
        TitleSuffix is null ? chart.TargetName : $"{chart.TargetName} - {TitleSuffix}";

    [JSInvokable]
    public async Task OnChartResizeStart()
    {
        if (_disposed || _resizing || Loading || Charts.Count == 0)
        {
            return;
        }

        _resizing = true;
        await DestroyChartsAsync();
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task OnChartResizeEnd()
    {
        if (_disposed || !_resizing)
        {
            return;
        }

        _resizing = false;
        _pendingUpdate = true;
        await InvokeAsync(StateHasChanged);
    }

    private async Task DestroyChartsAsync()
    {
        if (_module is null)
        {
            return;
        }

        for (var i = 0; i < Charts.Count; i++)
        {
            await _module.InvokeVoidAsync("destroy", GetCanvasId(i));
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;

        if (_module is not null)
        {
            if (_resizeRegistered && _resizeRef is not null)
            {
                await _module.InvokeVoidAsync("unregisterResize", _resizeRef);
            }

            await DestroyChartsAsync();
            await _module.DisposeAsync();
        }

        _resizeRef?.Dispose();
    }
}
