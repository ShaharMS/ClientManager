#!/usr/bin/env python3
"""Generate observability/grafana/dashboards/clientmanager.json."""

from __future__ import annotations

import json
import urllib.error
import urllib.request
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
OUTPUT = REPO_ROOT / "observability" / "grafana" / "dashboards" / "clientmanager.json"

JOB = 'job="clientmanager-api"'
API_INSTANCE = f'{JOB}, instance=~"api-[0-9]+:.*"'
DS = {"type": "prometheus", "uid": "${datasource}"}
RATE = "$__rate_interval"

TEMPO_DS = {"type": "tempo", "uid": "clientmanager-tempo"}
STORAGE_ROLES = ("Configuration", "RateLimiting", "Rpm")
ACCESS_CHECK_ENDPOINT = "/api/v2/access/check"


def prom(expr: str, legend: str = "", ref: str = "A") -> dict:
    return {
        "datasource": DS,
        "expr": expr,
        "legendFormat": legend,
        "refId": ref,
    }


def requests_label_filter() -> str:
    return 'service=~"$service", client=~"$client"'


def storage_label_filter() -> str:
    return 'serviceId=~"$service", clientId=~"$client"'


def global_filter(extra: str = "", *, storage: bool = False) -> str:
    labels = storage_label_filter() if storage else requests_label_filter()
    base = f"{{{JOB}, {labels}"
    if extra:
        base += f", {extra}"
    return base + "}"


def pod_filter(extra: str = "", *, storage: bool = False) -> str:
    labels = storage_label_filter() if storage else requests_label_filter()
    base = f'{{{JOB}, instance="$pod", {labels}'
    if extra:
        base += f", {extra}"
    return base + "}"


def storage_scope_filter(extra: str = "") -> str:
    base = f'{{{JOB}, instance=~"$storage_pod"'
    if extra:
        base += f", {extra}"
    return base + "}"


def cache_scope_filter(result: str) -> str:
    return f'{{{JOB}, instance=~"$storage_pod", result="{result}"}}'


def http_global_filter(extra: str = "") -> str:
    base = f"{{{JOB}"
    if extra:
        base += f", {extra}"
    return base + "}"


def http_pod_filter(extra: str = "") -> str:
    base = f'{{{JOB}, instance="$pod"'
    if extra:
        base += f", {extra}"
    return base + "}"


def http_client_response_targets(filter_fn) -> list[dict]:
    return [
        prom(
            f"sum by (endpoint, statusCode) (rate(clientmanager_http_requests_total{filter_fn('statusCode=~\"4..\"')}[{RATE}]))",
            "{{statusCode}} {{endpoint}}",
            "A",
        ),
        prom(
            f"sum by (endpoint, statusCode) (rate(clientmanager_http_requests_total{filter_fn(f'statusCode=\"200\", endpoint=\"{ACCESS_CHECK_ENDPOINT}\"')}[{RATE}]))",
            "{{statusCode}} {{endpoint}}",
            "B",
        ),
    ]


def gf_outcome_granted() -> str:
    return global_filter('outcome="granted"')


def pf_outcome_granted() -> str:
    return pod_filter('outcome="granted"')


def row_panel(title: str, y: int, panel_id: int) -> dict:
    return {
        "type": "row",
        "title": title,
        "gridPos": {"h": 1, "w": 24, "x": 0, "y": y},
        "collapsed": False,
        "panels": [],
        "id": panel_id,
    }


def stat_panel(
    panel_id: int,
    title: str,
    expr: str,
    y: int,
    x: int,
    w: int,
    h: int = 4,
    unit: str = "short",
    decimals: int | None = None,
) -> dict:
    defaults: dict = {"unit": unit, "color": {"mode": "thresholds"}}
    if decimals is not None:
        defaults["decimals"] = decimals
    return {
        "id": panel_id,
        "type": "stat",
        "title": title,
        "datasource": DS,
        "gridPos": {"h": h, "w": w, "x": x, "y": y},
        "targets": [prom(expr, title)],
        "fieldConfig": {"defaults": defaults, "overrides": []},
        "options": {
            "colorMode": "value",
            "graphMode": "area",
            "justifyMode": "auto",
            "orientation": "auto",
            "reduceOptions": {"calcs": ["lastNotNull"], "fields": "", "values": False},
            "textMode": "auto",
        },
    }


def timeseries_panel(
    panel_id: int,
    title: str,
    targets: list[dict],
    y: int,
    x: int,
    w: int,
    h: int = 8,
    unit: str = "short",
    stacking: str | None = None,
) -> dict:
    custom = {
        "drawStyle": "line",
        "lineWidth": 2,
        "fillOpacity": 10,
        "showPoints": "never",
        "spanNulls": False,
    }
    if stacking:
        custom["stacking"] = {"group": "A", "mode": stacking}
    return {
        "id": panel_id,
        "type": "timeseries",
        "title": title,
        "datasource": DS,
        "gridPos": {"h": h, "w": w, "x": x, "y": y},
        "targets": targets,
        "fieldConfig": {
            "defaults": {"unit": unit, "custom": custom},
            "overrides": [],
        },
        "options": {
            "legend": {"displayMode": "list", "placement": "bottom", "showLegend": True},
            "tooltip": {"mode": "multi", "sort": "desc"},
        },
    }


def latency_median_targets(bucket_metric: str, scope: str) -> list[dict]:
    return [
        prom(
            f"histogram_quantile(0.50, sum by (le) (rate({bucket_metric}{scope}[{RATE}])))",
            "median",
            "A",
        ),
    ]


def latency_median_p95_p99_targets(bucket_metric: str, scope: str) -> list[dict]:
    return [
        prom(
            f"histogram_quantile(0.50, sum by (le) (rate({bucket_metric}{scope}[{RATE}])))",
            "median",
            "A",
        ),
        prom(
            f"histogram_quantile(0.95, sum by (le) (rate({bucket_metric}{scope}[{RATE}])))",
            "p95",
            "B",
        ),
        prom(
            f"histogram_quantile(0.99, sum by (le) (rate({bucket_metric}{scope}[{RATE}])))",
            "p99",
            "C",
        ),
    ]


def text_panel(panel_id: int, content: str, y: int, h: int = 7) -> dict:
    return {
        "id": panel_id,
        "type": "text",
        "title": "",
        "gridPos": {"h": h, "w": 24, "x": 0, "y": y},
        "options": {"mode": "markdown", "content": content},
    }


def storage_role_ops_panel(
    panel_id: int,
    role: str | None,
    title: str,
    y: int,
    x: int,
    w: int = 12,
    h: int = 8,
    *,
    include_cache: bool = False,
) -> dict:
    scope = storage_scope_filter(f'role="{role}"') if role else storage_scope_filter()
    targets = [
        prom(
            f"sum by (operation) (rate("
            f"clientmanager_storage_document_store_duration_milliseconds_count{scope}[{RATE}]))",
            "{{operation}}",
            "A",
        ),
    ]
    if include_cache:
        targets.append(
            prom(
                f"sum(rate(clientmanager_storage_catalog_cache_total{cache_scope_filter('hit')}[{RATE}]))",
                "cache hit",
                "B",
            ),
        )
    return timeseries_panel(
        panel_id,
        f"{title} - ops/s",
        targets,
        y,
        x,
        w,
        h,
        unit="ops",
    )


def storage_role_latency_panel(
    panel_id: int,
    role: str | None,
    title: str,
    y: int,
    x: int,
    w: int = 12,
    h: int = 8,
    *,
    include_cache: bool = False,
) -> dict:
    scope = storage_scope_filter(f'role="{role}"') if role else storage_scope_filter()
    bucket = "clientmanager_storage_document_store_duration_milliseconds_bucket"
    targets = [
        prom(
            f"histogram_quantile(0.50, sum by (le, operation) (rate({bucket}{scope}[{RATE}])))",
            "{{operation}}",
            "A",
        ),
    ]
    if include_cache:
        cache_bucket = "clientmanager_storage_catalog_cache_duration_milliseconds_bucket"
        targets.append(
            prom(
                f"histogram_quantile(0.50, sum by (le) (rate({cache_bucket}{cache_scope_filter('hit')}[{RATE}])))",
                "cache hit",
                "B",
            ),
        )
    return timeseries_panel(
        panel_id,
        f"{title} - Median Latency",
        targets,
        y,
        x,
        w,
        h,
        unit="ms",
    )


DASHBOARD_UID = "clientmanager-observability"
TEMPO_SEARCH_URL = "http://localhost:3200/api/search?q=%7B%7D&limit=1"
TRACE_DASHBOARD_URL = (
    f"/d/{DASHBOARD_UID}?${{__url_time_range}}&var-traceId=${{__data.fields.traceID}}"
)
TRACE_ID_URL = f"/d/{DASHBOARD_UID}?${{__url_time_range}}&var-traceId=${{__value.raw}}"
TRACE_VIEW_LINK = {
    "title": "Show timeline",
    "url": TRACE_DASHBOARD_URL,
    "oneClick": True,
}
TRACE_ID_LINK = {
    "title": "Show timeline",
    "url": TRACE_ID_URL,
    "oneClick": True,
}
TRACE_LINK_FIELDS = (
    {
        "matcher": {"id": "byName", "options": "Duration"},
        "properties": [{"id": "links", "value": [TRACE_VIEW_LINK]}],
    },
    {
        "matcher": {"id": "byName", "options": "Start time"},
        "properties": [{"id": "links", "value": [TRACE_VIEW_LINK]}],
    },
    {
        "matcher": {"id": "byName", "options": "Name"},
        "properties": [{"id": "links", "value": [TRACE_VIEW_LINK]}],
    },
    {
        "matcher": {"id": "byRegexp", "options": "/^(traceID|Trace ID)$/"},
        "properties": [{"id": "links", "value": [TRACE_ID_LINK]}],
    },
)


def traces_picker_table_panel(panel_id: int, y: int) -> dict:
    return {
        "id": panel_id,
        "type": "table",
        "title": "Recent traces",
        "description": "Click Duration, Start time, or Name to load the waterfall on the right. Expand (>) to list spans in the table.",
        "datasource": TEMPO_DS,
        "gridPos": {"h": 16, "w": 10, "x": 0, "y": y},
        "interval": "",
        "targets": [
            {
                "datasource": TEMPO_DS,
                "queryType": "traceql",
                "query": '{ resource.service.name = "ClientManager.Api" }',
                "refId": "A",
                "limit": 15,
                "tableType": "traces",
                "spanLimit": 100,
            }
        ],
        "options": {
            "showHeader": True,
            "sortBy": [{"displayName": "Start time", "desc": True}],
            "footer": {"show": False},
            "cellHeight": "sm",
        },
        "fieldConfig": {
            "defaults": {"custom": {"filterable": False}},
            "overrides": list(TRACE_LINK_FIELDS),
        },
    }


def fetch_default_trace_id() -> str | None:
    # ponytail: best-effort at build time; avoids empty/invalid traceId panel queries.
    try:
        with urllib.request.urlopen(TEMPO_SEARCH_URL, timeout=3) as response:
            payload = json.loads(response.read().decode("utf-8"))
        traces = payload.get("traces") or []
        if traces:
            return str(traces[0]["traceID"])
    except (OSError, TimeoutError, urllib.error.URLError, KeyError, TypeError, ValueError):
        pass
    return None


def traces_timeline_panel(panel_id: int, y: int, default_trace_id: str | None) -> dict:
    if not default_trace_id:
        panel = text_panel(
            panel_id,
            "Click **Duration**, **Start time**, or **Name** on a trace row to load its waterfall here.",
            y,
            h=16,
        )
        panel["gridPos"] = {"h": 16, "w": 14, "x": 10, "y": y}
        return panel
    return {
        "id": panel_id,
        "type": "traces",
        "title": "Trace timeline",
        "description": "Span waterfall for the trace selected in the table on the left.",
        "datasource": TEMPO_DS,
        "gridPos": {"h": 16, "w": 14, "x": 10, "y": y},
        "interval": "",
        "targets": [
            {
                "datasource": TEMPO_DS,
                "queryType": "traceql",
                "query": "${traceId}",
                "refId": "A",
            }
        ],
        "options": {
            "showTraceId": True,
            "spanFilters": {
                "criticalPathOnly": False,
                "matchesOnly": False,
                "serviceNameOperator": "=",
                "spanNameOperator": "=",
                "tags": [],
            },
        },
    }


def build() -> dict:
    default_trace_id = fetch_default_trace_id()
    gf = global_filter
    pf = pod_filter
    hgf = http_global_filter
    hpf = http_pod_filter
    panels: list[dict] = []
    pid = 1
    y = 0

    panels.append(
        text_panel(
            pid,
            """### ClientManager observability

- **Global zone**: sums all API pods - unaffected by the pod picker below.
- **Pod zone**: all panels filter `instance="$pod"`.
- **Storage zone**: per-role subsections - ops/s and Median Latency (catalog cache on Configuration); **Storage pod** = *All* or one pod.
- **Traces** (bottom): click Duration / Start time / Name on a trace row; waterfall loads in the panel on the right. Requires `--profile traces`.
- Replicas: `${replicas}` (API pods only).
""",
            y,
            h=7,
        )
    )
    pid += 1
    y += 7

    panels.append(row_panel("Global - all replicas", y, pid))
    pid += 1
    y += 1

    panels.extend(
        [
            stat_panel(
                pid,
                "Server error % (5xx)",
                f"100 * sum(rate(clientmanager_http_requests_total{hgf('statusCode=~\"5..\"')}[{RATE}])) / sum(rate(clientmanager_http_requests_total{hgf()}[{RATE}]))",
                y, 0, 4, unit="percent", decimals=2,
            ),
            stat_panel(
                pid + 1,
                "Access denial %",
                f"100 * sum(rate(clientmanager_access_denied_total{global_filter(storage=True)}[{RATE}])) / sum(rate(clientmanager_requests_total{gf()}[{RATE}]))",
                y, 4, 4, unit="percent", decimals=2,
            ),
            stat_panel(
                pid + 2,
                "HTTP req/s",
                f"sum(rate(clientmanager_http_requests_total{hgf()}[{RATE}]))",
                y, 8, 4, unit="reqps", decimals=1,
            ),
            stat_panel(
                pid + 3,
                "Access granted/s",
                f"sum(rate(clientmanager_requests_total{gf_outcome_granted()}[{RATE}]))",
                y, 12, 4, unit="reqps", decimals=1,
            ),
            stat_panel(
                pid + 4,
                "Access denied/s",
                f"sum(rate(clientmanager_access_denied_total{global_filter(storage=True)}[{RATE}]))",
                y, 16, 4, unit="reqps", decimals=1,
            ),
            stat_panel(
                pid + 5,
                "Replicas up",
                f"count(up{{{API_INSTANCE}}} == 1)",
                y, 20, 4,
            ),
        ]
    )
    pid += 6
    y += 4

    panels.append(
        timeseries_panel(
            pid,
            "Global HTTP Median Latency",
            latency_median_targets(
                "clientmanager_http_requests_duration_milliseconds_bucket",
                hgf(),
            ),
            y, 0, 24, h=8, unit="ms",
        )
    )
    pid += 1
    y += 8

    panels.append(
        timeseries_panel(
            pid,
            "HTTP client responses (access 200 + 4xx/s)",
            http_client_response_targets(hgf),
            y, 0, 12, unit="reqps",
        )
    )
    panels.append(
        timeseries_panel(
            pid + 1,
            "Server errors (5xx/s)",
            [prom(
                f"sum by (endpoint, statusCode) (rate(clientmanager_http_requests_total{hgf('statusCode=~\"5..\"')}[{RATE}]))",
                "{{statusCode}} {{endpoint}}",
            )],
            y, 12, 12, unit="reqps",
        )
    )
    pid += 2
    y += 8

    panels.append(
        timeseries_panel(
            pid,
            "Global access outcomes",
            [prom(
                f"sum by (outcome) (rate(clientmanager_requests_total{gf()}[{RATE}]))",
                "{{outcome}}",
            )],
            y, 0, 12, unit="reqps",
        )
    )
    panels.append(
        timeseries_panel(
            pid + 1,
            "Global denials by reason",
            [prom(
                f"sum by (reason) (rate(clientmanager_access_denied_total{global_filter(storage=True)}[{RATE}]))",
                "{{reason}}",
            )],
            y, 12, 12, unit="reqps",
        )
    )
    pid += 2
    y += 8

    panels.append(
        timeseries_panel(
            pid,
            "Granted req/s by service",
            [prom(
                f"sum by (service) (rate(clientmanager_requests_total{gf_outcome_granted()}[{RATE}]))",
                "{{service}}",
            )],
            y, 0, 12, unit="reqps",
        )
    )
    panels.append(
        timeseries_panel(
            pid + 1,
            "Granted req/s by client",
            [prom(
                f"sum by (client) (rate(clientmanager_requests_total{gf_outcome_granted()}[{RATE}]))",
                "{{client}}",
            )],
            y, 12, 12, unit="reqps",
        )
    )
    pid += 2
    y += 8

    panels.append(
        timeseries_panel(
            pid,
            "Global rate-limit allowed / denied",
            [
                prom(
                    f"sum(rate(clientmanager_ratelimit_allowed_total{global_filter(storage=True)}[{RATE}]))",
                    "allowed",
                    "A",
                ),
                prom(
                    f"sum(rate(clientmanager_ratelimit_denied_total{global_filter(storage=True)}[{RATE}]))",
                    "denied",
                    "B",
                ),
            ],
            y, 0, 12, unit="reqps",
        )
    )
    panels.append(
        timeseries_panel(
            pid + 1,
            "Global rate-limit strategy Median Latency",
            [prom(
                f"histogram_quantile(0.50, sum by (le, strategy) (rate("
                f"clientmanager_storage_ratelimit_strategy_duration_milliseconds_bucket"
                f"{global_filter(storage=True)}[{RATE}])))",
                "{{strategy}}",
            )],
            y, 12, 12, unit="ms",
        )
    )
    pid += 2
    y += 8

    panels.append(
        timeseries_panel(
            pid,
            "Service access attempts - Latency",
            latency_median_p95_p99_targets(
                "clientmanager_storage_access_duration_milliseconds_bucket",
                global_filter(storage=True),
            ),
            y, 0, 24, h=8, unit="ms",
        )
    )
    pid += 1
    y += 8

    # --- Storage by role ---
    panels.append(row_panel("Storage - document store by role ($storage_pod)", y, pid))
    pid += 1
    y += 1

    for role in STORAGE_ROLES:
        panels.append(row_panel(f"Storage - {role}", y, pid))
        pid += 1
        y += 1
        include_cache = role == "Configuration"
        panels.append(storage_role_ops_panel(pid, role, role, y, 0, include_cache=include_cache))
        panels.append(storage_role_latency_panel(pid + 1, role, role, y, 12, include_cache=include_cache))
        pid += 2
        y += 8

    panels.append(row_panel("Storage - all roles", y, pid))
    pid += 1
    y += 1
    panels.append(storage_role_ops_panel(pid, None, "All roles", y, 0, include_cache=True))
    panels.append(storage_role_latency_panel(pid + 1, None, "All roles", y, 12, include_cache=True))
    pid += 2
    y += 8

    # --- Per-pod zone ---
    panels.append(row_panel("Pod - $pod", y, pid))
    pid += 1
    y += 1

    panels.extend(
        [
            stat_panel(
                pid,
                "Server error % (5xx)",
                f"100 * sum(rate(clientmanager_http_requests_total{hpf('statusCode=~\"5..\"')}[{RATE}])) / sum(rate(clientmanager_http_requests_total{hpf()}[{RATE}]))",
                y, 0, 6, unit="percent", decimals=2,
            ),
            stat_panel(
                pid + 1,
                "Pod HTTP req/s",
                f"sum(rate(clientmanager_http_requests_total{hpf()}[{RATE}]))",
                y, 6, 6, unit="reqps", decimals=1,
            ),
            stat_panel(
                pid + 2,
                "Pod granted/s",
                f"sum(rate(clientmanager_requests_total{pf_outcome_granted()}[{RATE}]))",
                y, 12, 6, unit="reqps", decimals=1,
            ),
            stat_panel(
                pid + 3,
                "Pod denied/s",
                f"sum(rate(clientmanager_access_denied_total{pod_filter(storage=True)}[{RATE}]))",
                y, 18, 6, unit="reqps", decimals=1,
            ),
        ]
    )
    pid += 4
    y += 4

    panels.append(
        timeseries_panel(
            pid,
            "Pod HTTP Median Latency",
            latency_median_targets(
                "clientmanager_http_requests_duration_milliseconds_bucket",
                hpf(),
            ),
            y, 0, 24, h=8, unit="ms",
        )
    )
    pid += 1
    y += 8

    panels.append(
        timeseries_panel(
            pid,
            "Pod HTTP client responses (access 200 + 4xx/s)",
            http_client_response_targets(hpf),
            y, 0, 12, unit="reqps",
        )
    )
    panels.append(
        timeseries_panel(
            pid + 1,
            "Pod server errors (5xx/s)",
            [prom(
                f"sum by (endpoint, statusCode) (rate(clientmanager_http_requests_total{hpf('statusCode=~\"5..\"')}[{RATE}]))",
                "{{statusCode}} {{endpoint}}",
            )],
            y, 12, 12, unit="reqps",
        )
    )
    pid += 2
    y += 8

    panels.append(
        timeseries_panel(
            pid,
            "Pod access outcomes",
            [prom(f"sum by (outcome) (rate(clientmanager_requests_total{pf()}[{RATE}]))", "{{outcome}}")],
            y, 0, 12, unit="reqps",
        )
    )
    panels.append(
        timeseries_panel(
            pid + 1,
            "Pod denials by reason",
            [prom(
                f"sum by (reason) (rate(clientmanager_access_denied_total{pod_filter(storage=True)}[{RATE}]))",
                "{{reason}}",
            )],
            y, 12, 12, unit="reqps",
        )
    )
    pid += 2
    y += 8

    panels.append(
        timeseries_panel(
            pid,
            "Pod rate-limit allowed / denied",
            [
                prom(
                    f"sum(rate(clientmanager_ratelimit_allowed_total{pod_filter(storage=True)}[{RATE}]))",
                    "allowed",
                    "A",
                ),
                prom(
                    f"sum(rate(clientmanager_ratelimit_denied_total{pod_filter(storage=True)}[{RATE}]))",
                    "denied",
                    "B",
                ),
            ],
            y, 0, 24, unit="reqps",
        )
    )
    pid += 1
    y += 8

    # --- Traces ---
    panels.append(row_panel("Traces - ClientManager.Api", y, pid))
    pid += 1
    y += 1
    panels.append(traces_picker_table_panel(pid, y))
    pid += 1
    panels.append(traces_timeline_panel(pid, y, default_trace_id))
    pid += 1
    y += 16

    return {
        "annotations": {"list": []},
        "editable": True,
        "fiscalYearStartMonth": 0,
        "graphTooltip": 1,
        "id": None,
        "links": [],
        "liveNow": False,
        "panels": panels,
        "schemaVersion": 39,
        "tags": ["clientmanager", "observability"],
        "templating": {
            "list": [
                {
                    "name": "datasource",
                    "type": "datasource",
                    "query": "prometheus",
                    "current": {
                        "selected": True,
                        "text": "ClientManager Prometheus",
                        "value": "clientmanager-prometheus",
                    },
                    "label": "Prometheus",
                },
                {
                    "name": "pod",
                    "type": "query",
                    "datasource": DS,
                    "query": f'label_values(up{{{API_INSTANCE}}}, instance)',
                    "refresh": 2,
                    "includeAll": False,
                    "multi": False,
                    "label": "Pod",
                },
                {
                    "name": "storage_pod",
                    "type": "query",
                    "datasource": DS,
                    "query": f'label_values(up{{{API_INSTANCE}}}, instance)',
                    "refresh": 2,
                    "includeAll": True,
                    "allValue": "api-[0-9]+:.*",
                    "multi": False,
                    "label": "Storage pod",
                },
                {
                    "name": "service",
                    "type": "query",
                    "datasource": DS,
                    "query": f'label_values(clientmanager_requests_total{{{JOB}}}, service)',
                    "refresh": 2,
                    "includeAll": True,
                    "allValue": ".*",
                    "multi": True,
                    "label": "Service",
                },
                {
                    "name": "client",
                    "type": "query",
                    "datasource": DS,
                    "query": f'label_values(clientmanager_requests_total{{{JOB}}}, client)',
                    "refresh": 2,
                    "includeAll": True,
                    "allValue": ".*",
                    "multi": True,
                    "label": "Client",
                },
                {
                    "name": "traceId",
                    "type": "textbox",
                    "label": "Trace",
                    "hide": 0,
                    "current": {
                        "selected": True,
                        "text": default_trace_id or "",
                        "value": default_trace_id or "",
                    },
                },
                {
                    "name": "replicas",
                    "type": "query",
                    "datasource": DS,
                    "query": f"count(up{{{API_INSTANCE}}} == 1)",
                    "hide": 2,
                    "refresh": 2,
                },
            ]
        },
        "time": {"from": "now-15m", "to": "now"},
        "timepicker": {},
        "timezone": "browser",
        "title": "ClientManager Observability",
        "uid": DASHBOARD_UID,
        "version": 16,
    }


def main() -> None:
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(build(), indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {OUTPUT}")


if __name__ == "__main__":
    main()
