const charts = new Map();

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

    if (config.capLine?.points?.length) {
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
        const hidden = meta.hidden === true;

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
            meta.hidden = meta.hidden === null ? !ds.hidden : null;
            chart.update();
            renderHtmlLegend(chart);
        });

        legendEl.append(item);
    }
}

export function createOrUpdate(canvasId, config) {
    const canvas = document.getElementById(canvasId);
    if (!canvas || typeof Chart === 'undefined') {
        return;
    }

    const datasets = buildDatasets(config);
    const options = buildOptions(config);

    const existing = charts.get(canvasId);
    if (existing) {
        existing.data.labels = config.labels;
        existing.data.datasets = datasets;
        existing.options = options;
        existing.update(config.animate === false ? 'none' : 'active');
        renderHtmlLegend(existing);
        return;
    }

    const chart = new Chart(canvas, {
        type: 'line',
        data: { labels: config.labels, datasets },
        options
    });

    charts.set(canvasId, chart);
    renderHtmlLegend(chart);
}

export function destroy(canvasId) {
    const chart = charts.get(canvasId);
    if (chart) {
        chart.destroy();
        charts.delete(canvasId);
    }

    const legendEl = document.getElementById(legendIdFor(canvasId));
    legendEl?.replaceChildren();
}

export function destroyAll() {
    for (const id of charts.keys()) {
        destroy(id);
    }
}
