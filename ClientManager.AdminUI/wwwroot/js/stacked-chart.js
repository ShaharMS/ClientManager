const charts = new Map();
const legendHidden = new Map();
const resizeRefs = new Set();
const resizeObservers = new Map();
let resizeTimer = null;
let resizeActive = false;

function bindResize() {
    if (bindResize._bound) {
        return;
    }
    bindResize._bound = true;
    window.addEventListener('resize', handleResize);
    document.addEventListener('visibilitychange', handleVisibilityChange);
}

function unbindResize() {
    if (!bindResize._bound) {
        return;
    }
    bindResize._bound = false;
    window.removeEventListener('resize', handleResize);
    document.removeEventListener('visibilitychange', handleVisibilityChange);
    clearTimeout(resizeTimer);
    resizeTimer = null;
    resizeActive = false;
}

function handleResize() {
    if (!resizeActive) {
        resizeActive = true;
        notifyResize('OnChartResizeStart');
    }
    clearTimeout(resizeTimer);
    resizeTimer = setTimeout(() => {
        resizeActive = false;
        notifyResize('OnChartResizeEnd');
    }, 150);
}

function handleVisibilityChange() {
    if (document.hidden) {
        clearTimeout(resizeTimer);
        resizeActive = false;
        notifyResize('OnChartResizeStart');
        return;
    }

    requestAnimationFrame(() => {
        requestAnimationFrame(() => notifyResize('OnChartResizeEnd'));
    });
}

function notifyResize(method) {
    for (const ref of resizeRefs) {
        ref.invokeMethodAsync(method).catch(() => {});
    }
}

export function registerResize(dotNetRef) {
    bindResize();
    resizeRefs.add(dotNetRef);
}

export function unregisterResize(dotNetRef) {
    resizeRefs.delete(dotNetRef);
    if (resizeRefs.size === 0) {
        unbindResize();
    }
}

function hasValidDimensions(canvas) {
    const plot = canvas?.closest('.cm-stacked-chart__plot');
    if (!plot) {
        return false;
    }
    const { width, height } = plot.getBoundingClientRect();
    return width > 1 && height > 1;
}

function scheduleCreateOrUpdate(canvasId, config, attempt = 0) {
    if (attempt > 10 || document.hidden) {
        return;
    }
    requestAnimationFrame(() => {
        const canvas = document.getElementById(canvasId);
        if (!canvas) {
            return;
        }
        if (!hasValidDimensions(canvas)) {
            scheduleCreateOrUpdate(canvasId, config, attempt + 1);
            return;
        }
        doCreateOrUpdate(canvasId, config);
    });
}

function attachResizeObserver(canvasId, canvas) {
    if (resizeObservers.has(canvasId)) {
        return;
    }
    const plot = canvas.closest('.cm-stacked-chart__plot');
    if (!plot) {
        return;
    }
    const observer = new ResizeObserver(() => {
        const chart = charts.get(canvasId);
        if (!chart || document.hidden) {
            return;
        }
        if (!hasValidDimensions(canvas)) {
            return;
        }
        chart.resize();
    });
    observer.observe(plot);
    resizeObservers.set(canvasId, observer);
}

function cssVar(name, fallback) {
    const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    return value || fallback;
}

function legendIdFor(canvasId) {
    return `${canvasId}-legend`;
}

function formatNumber(value) {
    return new Intl.NumberFormat().format(Math.round(value));
}

function formatCompact(value) {
    const rounded = Math.round(value * 10) / 10;
    return rounded % 1 === 0 ? String(Math.round(rounded)) : rounded.toFixed(1);
}

// ponytail: mirrors LogarithmicScaleHelper.InverseTransform + FormatAxisLabel in C#
function inverseLogTransform(transformed) {
    return Math.pow(10, Number(transformed)) - 1;
}

function formatLogAxisLabel(transformed) {
    const original = inverseLogTransform(transformed);
    if (!Number.isFinite(original) || original < 0) {
        return '0';
    }
    if (original < 1) {
        return original.toFixed(1);
    }
    if (original < 1_000) {
        return formatNumber(original);
    }
    if (original < 1_000_000) {
        return `${formatCompact(original / 1_000)}K`;
    }
    return `${formatCompact(original / 1_000_000)}M`;
}

function formatLinearAxis(value) {
    const d = Number(value);
    if (!Number.isFinite(d)) {
        return '';
    }
    if (d < 1) {
        return d.toFixed(1);
    }
    if (d < 1_000) {
        return formatNumber(d);
    }
    if (d < 1_000_000) {
        return `${formatCompact(d / 1_000)}K`;
    }
    return `${formatCompact(d / 1_000_000)}M`;
}

function isDatasetHidden(chart, index) {
    const ds = chart.data.datasets[index];
    const meta = chart.getDatasetMeta(index);
    return meta.hidden !== null && meta.hidden !== undefined ? meta.hidden : !!ds.hidden;
}

function saveHiddenState(canvasId, chart) {
    const state = new Map();
    for (let i = 0; i < chart.data.datasets.length; i++) {
        state.set(chart.data.datasets[i].label, isDatasetHidden(chart, i));
    }
    legendHidden.set(canvasId, state);
}

function applyHiddenState(canvasId, chart) {
    const state = legendHidden.get(canvasId);
    if (!state) {
        return;
    }
    for (let i = 0; i < chart.data.datasets.length; i++) {
        const label = chart.data.datasets[i].label;
        if (state.has(label)) {
            chart.getDatasetMeta(i).hidden = state.get(label);
        }
    }
}

function legendSwatchColor(color) {
    if (typeof color !== 'string') {
        return color;
    }
    const match = color.match(/^rgba\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)/i);
    return match ? `rgb(${match[1]}, ${match[2]}, ${match[3]})` : color;
}

function themeColor(name, fallback) {
    return () => cssVar(name, fallback);
}

function buildDatasets(config) {
    const datasets = [];

    for (const series of config.series) {
        const dataset = {
            label: series.name,
            data: series.points,
            backgroundColor: series.fillColor,
            borderColor: series.strokeColor,
            borderWidth: 1,
            fill: true,
            stack: 'usage',
            hidden: series.hidden,
            pointRadius: 0,
            pointHoverRadius: 3,
            tension: 0.2
        };
        if (series.originalValues) {
            dataset.originalValues = series.originalValues;
        }
        datasets.push(dataset);
    }

    if (config.capLine?.points?.length && Math.max(...config.capLine.points) > 0) {
        const capDataset = {
            label: config.capLine.title,
            data: config.capLine.points,
            type: 'line',
            borderColor: cssVar('--color-text-secondary', '#64748b'),
            backgroundColor: 'transparent',
            borderDash: [6, 4],
            borderWidth: 2,
            fill: false,
            pointRadius: 0,
            stack: 'cap'
        };
        if (config.capLine.originalValues) {
            capDataset.originalValues = config.capLine.originalValues;
        }
        datasets.push(capDataset);
    }

    return datasets;
}

function buildOptions(config) {
    const textColor = themeColor('--color-text-primary', '#0f172a');
    const gridColor = themeColor('--color-border', '#e2e8f0');
    const fontFamily = cssVar('--font-family', "'Heebo', system-ui, sans-serif");
    const fontSize = parseFloat(cssVar('--font-size-sm', '0.875rem')) * 16 || 14;
    const isLog = config.axisScale === 'logarithmic';
    const formatTick = isLog ? formatLogAxisLabel : formatLinearAxis;

    return {
        responsive: true,
        maintainAspectRatio: false,
        animation: { duration: config.animate === false ? 0 : 250 },
        layout: {
            padding: { top: 6, bottom: 8, left: 4, right: 4 }
        },
        interaction: { mode: 'index', intersect: false },
        plugins: {
            legend: { display: false },
            tooltip: {
                titleFont: { family: fontFamily, size: fontSize },
                bodyFont: { family: fontFamily, size: fontSize },
                callbacks: {
                    label: (ctx) => {
                        const original = ctx.dataset.originalValues?.[ctx.dataIndex];
                        const value = original ?? ctx.parsed.y ?? 0;
                        return `${ctx.dataset.label}: ${formatNumber(value)}`;
                    }
                }
            }
        },
        scales: {
            x: {
                stacked: true,
                offset: true,
                ticks: {
                    color: textColor,
                    maxRotation: 0,
                    autoSkip: true,
                    maxTicksLimit: 12,
                    font: { family: fontFamily, size: fontSize }
                },
                grid: { color: gridColor, offset: true, drawOnChartArea: true }
            },
            y: {
                stacked: true,
                position: 'left',
                type: 'linear',
                beginAtZero: true,
                grace: '5%',
                ticks: {
                    color: textColor,
                    font: { family: fontFamily, size: fontSize },
                    callback: (value) => formatTick(value)
                },
                grid: { color: gridColor, drawOnChartArea: true }
            }
        }
    };
}

function renderHtmlLegend(chart) {
    const legendEl = document.getElementById(legendIdFor(chart.canvas.id));
    if (!legendEl) {
        return;
    }

    const textColor = cssVar('--color-text-primary', '#0f172a');
    legendEl.replaceChildren();

    for (let i = 0; i < chart.data.datasets.length; i++) {
        const ds = chart.data.datasets[i];
        const meta = chart.getDatasetMeta(i);
        const isLine = ds.type === 'line';
        const hidden = isDatasetHidden(chart, i);

        const item = document.createElement('li');
        item.className = 'cm-stacked-chart__legend-item' + (hidden ? ' cm-stacked-chart__legend-item--hidden' : '');
        item.dataset.index = String(i);

        const swatch = document.createElement('span');
        swatch.className = 'cm-stacked-chart__legend-swatch' + (isLine ? ' cm-stacked-chart__legend-swatch--line' : '');
        const stroke = legendSwatchColor(ds.borderColor || ds.backgroundColor);
        swatch.style.borderColor = stroke;
        if (isLine) {
            swatch.style.background = 'transparent';
        } else {
            swatch.style.background = legendSwatchColor(ds.backgroundColor);
        }

        const label = document.createElement('span');
        label.className = 'cm-stacked-chart__legend-label';
        label.textContent = ds.label;
        label.style.color = textColor;

        item.append(swatch, label);
        item.addEventListener('click', () => {
            meta.hidden = !isDatasetHidden(chart, i);
            saveHiddenState(chart.canvas.id, chart);
            chart.update();
            renderHtmlLegend(chart);
        });

        legendEl.append(item);
    }
}

function doCreateOrUpdate(canvasId, config) {
    const canvas = document.getElementById(canvasId);
    if (!canvas || typeof Chart === 'undefined') {
        return;
    }

    const datasets = buildDatasets(config);
    const options = buildOptions(config);

    const existing = charts.get(canvasId);
    if (existing) {
        saveHiddenState(canvasId, existing);
        existing.data.labels = config.labels;
        existing.data.datasets = datasets;
        existing.options = options;
        applyHiddenState(canvasId, existing);
        existing.update(config.animate === false ? 'none' : 'active');
        renderHtmlLegend(existing);
        attachResizeObserver(canvasId, canvas);
        return;
    }

    const chart = new Chart(canvas, {
        type: 'line',
        data: { labels: config.labels, datasets },
        options
    });

    charts.set(canvasId, chart);
    applyHiddenState(canvasId, chart);
    saveHiddenState(canvasId, chart);
    renderHtmlLegend(chart);
    attachResizeObserver(canvasId, canvas);
}

export function createOrUpdate(canvasId, config) {
    const canvas = document.getElementById(canvasId);
    if (!canvas || typeof Chart === 'undefined') {
        return;
    }
    if (!hasValidDimensions(canvas)) {
        scheduleCreateOrUpdate(canvasId, config);
        return;
    }
    doCreateOrUpdate(canvasId, config);
}

export function destroy(canvasId) {
    resizeObservers.get(canvasId)?.disconnect();
    resizeObservers.delete(canvasId);

    const chart = charts.get(canvasId);
    if (chart) {
        chart.destroy();
        charts.delete(canvasId);
    }
    legendHidden.delete(canvasId);

    const legendEl = document.getElementById(legendIdFor(canvasId));
    legendEl?.replaceChildren();
}

export function destroyAll() {
    for (const id of charts.keys()) {
        destroy(id);
    }
}
