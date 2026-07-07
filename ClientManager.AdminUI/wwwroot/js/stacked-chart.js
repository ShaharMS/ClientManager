const charts = new Map();

function cssVar(name, fallback) {
    const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    return value || fallback;
}

function formatNumber(value) {
    return new Intl.NumberFormat().format(Math.round(value));
}

function formatAxis(value, axisScale) {
    if (axisScale === 'logarithmic') {
        if (value <= 0) return '0';
        const exp = Math.log10(value);
        if (exp >= 3) return `${(value / 1000).toFixed(value >= 10000 ? 0 : 1)}k`;
        if (Number.isInteger(exp)) return `10^${exp}`;
        return formatNumber(value);
    }
    if (value >= 1000000) return `${(value / 1000000).toFixed(1)}M`;
    if (value >= 1000) return `${(value / 1000).toFixed(1)}k`;
    return formatNumber(value);
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
        datasets.push({
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
        });
    }

    return datasets;
}

function buildOptions(config) {
    const textColor = cssVar('--color-text-secondary', '#64748b');
    const gridColor = cssVar('--color-border', '#e2e8f0');

    return {
        responsive: true,
        maintainAspectRatio: false,
        animation: { duration: config.animate === false ? 0 : 250 },
        interaction: { mode: 'index', intersect: false },
        plugins: {
            legend: {
                position: 'right',
                labels: {
                    color: textColor,
                    boxWidth: 12,
                    usePointStyle: true
                },
                onClick: (_, item, legend) => {
                    const chart = legend.chart;
                    const meta = chart.getDatasetMeta(item.datasetIndex);
                    meta.hidden = meta.hidden === null ? !chart.data.datasets[item.datasetIndex].hidden : null;
                    chart.update();
                }
            },
            tooltip: {
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
                ticks: { color: textColor, maxRotation: 0, autoSkip: true, maxTicksLimit: 12 },
                grid: { color: gridColor }
            },
            y: {
                stacked: true,
                type: config.axisScale === 'logarithmic' ? 'logarithmic' : 'linear',
                ticks: {
                    color: textColor,
                    callback: (value) => formatAxis(value, config.axisScale)
                },
                grid: { color: gridColor }
            }
        }
    };
}

export function createOrUpdate(canvasId, config) {
    const canvas = document.getElementById(canvasId);
    if (!canvas || typeof Chart === 'undefined') {
        return;
    }

    const datasets = buildDatasets(config);

    const existing = charts.get(canvasId);
    if (existing) {
        existing.data.labels = config.labels;
        existing.data.datasets = datasets;
        existing.options = buildOptions(config);
        existing.update(config.animate === false ? 'none' : 'active');
        return;
    }

    const chart = new Chart(canvas, {
        type: 'line',
        data: { labels: config.labels, datasets },
        options: buildOptions(config)
    });

    charts.set(canvasId, chart);
}

export function destroy(canvasId) {
    const chart = charts.get(canvasId);
    if (chart) {
        chart.destroy();
        charts.delete(canvasId);
    }
}

export function destroyAll() {
    for (const id of charts.keys()) {
        destroy(id);
    }
}
