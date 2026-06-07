"""
Launches a local Grafana, Prometheus, and Jaeger stack for ClientManager.

The stack is meant to visualize public API metrics (including the in-process
storage metrics) plus distributed traces emitted from the existing OTLP
instrumentation.

Running the script without a subcommand defaults to the full stack startup path.

By default the script starts:
- Grafana on http://localhost:3000
- Prometheus on http://localhost:9090
- Jaeger on http://localhost:16686

It also includes lightweight helper commands for configuring the existing hosts
to export traces to the local OTLP endpoint without editing the launcher itself.
"""

from __future__ import annotations

import argparse
import http.client
import json
import os
import subprocess
import sys
import textwrap
import time
import urllib.error
import urllib.parse
import urllib.request
import webbrowser
from pathlib import Path

from configuration import CONFIGURATION

GLOBAL_SETTINGS = CONFIGURATION["global"]
OBSERVABILITY_SETTINGS = CONFIGURATION["scripts"]["launch_observability_ui"]
OBSERVABILITY_PATHS = OBSERVABILITY_SETTINGS["paths"]
OBSERVABILITY_CONTAINERS = OBSERVABILITY_SETTINGS["containers"]
OBSERVABILITY_DEFAULTS = OBSERVABILITY_SETTINGS["defaults"]
OBSERVABILITY_DOCKER = OBSERVABILITY_SETTINGS["docker"]
OBSERVABILITY_GRAFANA = OBSERVABILITY_SETTINGS["grafana"]
OBSERVABILITY_TIMEOUTS = OBSERVABILITY_SETTINGS["timeouts"]


def resolve_image(image_key: str) -> str:
    override = OBSERVABILITY_DOCKER.get("image_overrides", {}).get(image_key)
    if override:
        return override

    base_image = OBSERVABILITY_DOCKER["images"][image_key]
    registry_prefix = OBSERVABILITY_DOCKER.get("registry_prefix")
    if registry_prefix:
        return f"{registry_prefix.rstrip('/')}/{base_image}"

    return base_image

SCRIPT_DIR = Path(__file__).resolve().parent
GENERATED_DIR = SCRIPT_DIR / OBSERVABILITY_PATHS["generated_directory"]
COMPOSE_FILE = GENERATED_DIR / OBSERVABILITY_PATHS["compose_file"]
PROMETHEUS_FILE = GENERATED_DIR / OBSERVABILITY_PATHS["prometheus_directory"] / OBSERVABILITY_PATHS["prometheus_file"]
GRAFANA_PROVISIONING_DIR = GENERATED_DIR / OBSERVABILITY_PATHS["grafana_directory"] / OBSERVABILITY_PATHS["provisioning_directory"]
GRAFANA_DATASOURCES_FILE = GRAFANA_PROVISIONING_DIR / OBSERVABILITY_PATHS["datasources_directory"] / OBSERVABILITY_PATHS["datasources_file"]
GRAFANA_DASHBOARD_PROVIDER_FILE = GRAFANA_PROVISIONING_DIR / OBSERVABILITY_PATHS["dashboards_directory"] / OBSERVABILITY_PATHS["dashboard_provider_file"]
GRAFANA_DASHBOARD_FILE = (
    GRAFANA_PROVISIONING_DIR
    / OBSERVABILITY_PATHS["dashboards_directory"]
    / OBSERVABILITY_PATHS["dashboard_folder"]
    / OBSERVABILITY_PATHS["dashboard_file"]
)

STACK_NETWORK = OBSERVABILITY_CONTAINERS["network"]
GRAFANA_CONTAINER = OBSERVABILITY_CONTAINERS["grafana_container"]
PROMETHEUS_CONTAINER = OBSERVABILITY_CONTAINERS["prometheus_container"]
JAEGER_CONTAINER = OBSERVABILITY_CONTAINERS["jaeger_container"]
CONTAINER_NAMES = (GRAFANA_CONTAINER, PROMETHEUS_CONTAINER, JAEGER_CONTAINER)

DEFAULT_API_HOST = GLOBAL_SETTINGS["local_runtime"]["docker_host"]
DEFAULT_API_PORT = GLOBAL_SETTINGS["local_runtime"]["api_port"]
DEFAULT_GRAFANA_PORT = OBSERVABILITY_DEFAULTS["grafana_port"]
DEFAULT_PROMETHEUS_PORT = OBSERVABILITY_DEFAULTS["prometheus_port"]
DEFAULT_JAEGER_PORT = OBSERVABILITY_DEFAULTS["jaeger_port"]
DEFAULT_OTLP_GRPC_PORT = OBSERVABILITY_DEFAULTS["otlp_grpc_port"]
DEFAULT_OTLP_HTTP_PORT = OBSERVABILITY_DEFAULTS["otlp_http_port"]
DEFAULT_OTLP_ENDPOINT = OBSERVABILITY_DEFAULTS["otlp_endpoint"]
DEFAULT_SCRAPE_INTERVAL = OBSERVABILITY_DEFAULTS["scrape_interval"]
DOCKER_API_FALLBACKS = tuple(OBSERVABILITY_DOCKER["api_fallbacks"])
JAEGER_IMAGE = resolve_image("jaeger")
PROMETHEUS_IMAGE = resolve_image("prometheus")
GRAFANA_IMAGE = resolve_image("grafana")
GRAFANA_ENVIRONMENT = OBSERVABILITY_GRAFANA["environment"]
DASHBOARD_SETTINGS = OBSERVABILITY_GRAFANA["dashboard"]
REQUEST_TIMEOUT_SECONDS = OBSERVABILITY_TIMEOUTS["http_request_seconds"]

DOCKER_ENV_OVERRIDES: dict[str, str] = {}


class CommandError(RuntimeError):
    pass


def parse_args() -> argparse.Namespace:
    raw_args = sys.argv[1:]
    if not raw_args:
        raw_args = ["up"]

    parser = argparse.ArgumentParser(
        description="Launch a local observability website for ClientManager traces and metrics."
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    up_parser = subparsers.add_parser(
        "up",
        help="Start Grafana, Prometheus, and Jaeger for local ClientManager observability.",
    )
    up_parser.add_argument(
        "--launcher",
        choices=("auto", "compose", "docker-run"),
        default=OBSERVABILITY_DEFAULTS["launcher"],
        help="Container launch strategy. Defaults to compose when available.",
    )
    up_parser.add_argument("--api-host", default=DEFAULT_API_HOST, help="Host Prometheus should use to scrape the public API.")
    up_parser.add_argument("--api-port", type=int, default=DEFAULT_API_PORT, help="Public API port exposing /prometheus/otel.")
    up_parser.add_argument(
        "--grafana-port",
        type=int,
        default=DEFAULT_GRAFANA_PORT,
        help="Local Grafana port.",
    )
    up_parser.add_argument(
        "--prometheus-port",
        type=int,
        default=DEFAULT_PROMETHEUS_PORT,
        help="Local Prometheus port.",
    )
    up_parser.add_argument(
        "--jaeger-port",
        type=int,
        default=DEFAULT_JAEGER_PORT,
        help="Local Jaeger UI port.",
    )
    up_parser.add_argument(
        "--otlp-grpc-port",
        type=int,
        default=DEFAULT_OTLP_GRPC_PORT,
        help="Local OTLP gRPC port Jaeger should listen on.",
    )
    up_parser.add_argument(
        "--otlp-http-port",
        type=int,
        default=DEFAULT_OTLP_HTTP_PORT,
        help="Local OTLP HTTP port Jaeger should listen on.",
    )
    up_parser.add_argument(
        "--scrape-interval",
        default=DEFAULT_SCRAPE_INTERVAL,
        help="Prometheus scrape interval for both ClientManager hosts.",
    )
    up_parser.add_argument(
        "--open-browser",
        dest="open_browser",
        action="store_true",
        help="Open Grafana automatically after the stack is ready.",
    )
    up_parser.add_argument(
        "--no-browser",
        dest="open_browser",
        action="store_false",
        help="Do not open Grafana automatically.",
    )
    up_parser.set_defaults(open_browser=OBSERVABILITY_DEFAULTS["open_browser"])

    down_parser = subparsers.add_parser("down", help="Stop and remove the local observability stack.")
    down_parser.add_argument(
        "--launcher",
        choices=("auto", "compose", "docker-run"),
        default=OBSERVABILITY_DEFAULTS["launcher"],
        help="Optional hint for cleanup. Generic cleanup still runs either way.",
    )

    for subcommand in ("print-env", "print-appsettings", "print-launchsettings"):
        snippet_parser = subparsers.add_parser(
            subcommand,
            help=f"Print the {subcommand.replace('-', ' ')} snippet for the OTLP endpoint.",
        )
        snippet_parser.add_argument(
            "--otlp-endpoint",
            default=DEFAULT_OTLP_ENDPOINT,
            help="OTLP endpoint to inject into ClientManager.Api.",
        )

    return parser.parse_args(raw_args)


def main() -> int:
    args = parse_args()

    try:
        if args.command == "up":
            command_up(args)
        elif args.command == "down":
            command_down(args)
        elif args.command == "print-env":
            print_env_snippet(args.otlp_endpoint)
        elif args.command == "print-appsettings":
            print_appsettings_snippet(args.otlp_endpoint)
        elif args.command == "print-launchsettings":
            print_launchsettings_snippet(args.otlp_endpoint)
        else:  # pragma: no cover - argparse enforces this
            raise CommandError(f"Unsupported command '{args.command}'.")
    except CommandError as error:
        print(f"error: {error}", file=sys.stderr)
        return 1

    return 0


def command_up(args: argparse.Namespace) -> None:
    ensure_docker_available()
    launcher = resolve_launcher(args.launcher)
    write_stack_files(args)

    stop_stack(ignore_errors=True)

    if launcher == "compose":
        run_checked(
            ["docker", "compose", "-f", str(COMPOSE_FILE), "up", "-d"],
            "Unable to start the observability stack with Docker Compose.",
        )
    else:
        start_with_docker_run(args)

    grafana_url = f"http://localhost:{args.grafana_port}"
    api_trace_search_url = build_grafana_trace_search_url(args.grafana_port, "ClientManager.Api")
    prometheus_url = f"http://localhost:{args.prometheus_port}"
    jaeger_url = f"http://localhost:{args.jaeger_port}"
    otlp_endpoint = f"http://localhost:{args.otlp_grpc_port}"

    wait_for_http(f"{grafana_url}/api/health", "Grafana")
    wait_for_http(f"{prometheus_url}/-/ready", "Prometheus")
    wait_for_http(jaeger_url, "Jaeger")

    print("Observability stack is ready.")
    print()
    print(f"Grafana:    {grafana_url}")
    print(f"Trace Search (API):     {api_trace_search_url}")
    print(f"Prometheus: {prometheus_url}")
    print(f"Jaeger:     {jaeger_url}")
    print(f"OTLP gRPC:  {otlp_endpoint}")
    print()
    if args.otlp_grpc_port == DEFAULT_OTLP_GRPC_PORT:
        print("Development defaults in ClientManager.Api already export to this OTLP endpoint.")
        print("Use the helper commands below only if you override the OTLP port or run outside Development.")
    else:
        print("This stack is using a non-default OTLP endpoint. Configure the API host before generating traces:")
        print(f'  $env:Observability__OtlpEndpoint = "{otlp_endpoint}"')
        print()
        print("Or update the hosts with one of these helpers:")
    print(f"  python {Path(__file__).relative_to(SCRIPT_DIR.parent)} print-env")
    print(f"  python {Path(__file__).relative_to(SCRIPT_DIR.parent)} print-appsettings")
    print(f"  python {Path(__file__).relative_to(SCRIPT_DIR.parent)} print-launchsettings")
    print()
    print("Grafana comes preloaded with:")
    print("  - a Prometheus datasource for the Api /prometheus/otel")
    print("  - a Jaeger datasource for ClientManager.Api traces")
    print("  - a starter dashboard for request, storage-client, and document-store latency")
    print("  - direct Grafana Trace Search URLs that open Jaeger Explore in Search mode")

    report_scrape_target("Public API metrics", f"http://localhost:{args.api_port}/prometheus/otel")

    if args.open_browser:
        webbrowser.open(api_trace_search_url)


def command_down(args: argparse.Namespace) -> None:
    if args.launcher == "compose" and COMPOSE_FILE.exists():
        subprocess.run(
            ["docker", "compose", "-f", str(COMPOSE_FILE), "down", "--remove-orphans"],
            capture_output=True,
            text=True,
            env=build_subprocess_env(),
        )

    stop_stack(ignore_errors=True)
    print("Observability stack stopped.")


def ensure_docker_available() -> None:
    if command_available(["docker", "version"]):
        return

    for api_version in DOCKER_API_FALLBACKS:
        if command_available(["docker", "version"], env_overrides={"DOCKER_API_VERSION": api_version}):
            DOCKER_ENV_OVERRIDES["DOCKER_API_VERSION"] = api_version
            return

    raise CommandError("Docker is required but was not found or the daemon is unavailable.")


def resolve_launcher(choice: str) -> str:
    if choice == "compose":
        if not command_available(["docker", "compose", "version"]):
            raise CommandError("Docker Compose is not available. Use --launcher docker-run instead.")
        return "compose"

    if choice == "docker-run":
        return "docker-run"

    if command_available(["docker", "compose", "version"]):
        return "compose"

    return "docker-run"


def build_subprocess_env(env_overrides: dict[str, str] | None = None) -> dict[str, str]:
    environment = os.environ.copy()
    environment.update(DOCKER_ENV_OVERRIDES)
    if env_overrides:
        environment.update(env_overrides)
    return environment


def command_available(command: list[str], env_overrides: dict[str, str] | None = None) -> bool:
    try:
        result = subprocess.run(
            command,
            capture_output=True,
            text=True,
            timeout=OBSERVABILITY_TIMEOUTS["command_available_seconds"],
            env=build_subprocess_env(env_overrides),
        )
    except (FileNotFoundError, subprocess.TimeoutExpired):
        return False
    return result.returncode == 0


def run_checked(command: list[str], failure_message: str) -> str:
    try:
        result = subprocess.run(
            command,
            capture_output=True,
            text=True,
            check=False,
            env=build_subprocess_env(),
        )
    except FileNotFoundError as error:
        raise CommandError(f"{failure_message} Command not found: {command[0]}") from error

    if result.returncode != 0:
        output = (result.stderr or result.stdout).strip()
        raise CommandError(f"{failure_message} {output}".strip())

    return result.stdout.strip()


def stop_stack(ignore_errors: bool) -> None:
    for name in CONTAINER_NAMES:
        try:
            result = subprocess.run(
                ["docker", "rm", "-f", name],
                capture_output=True,
                text=True,
                timeout=OBSERVABILITY_TIMEOUTS["container_stop_seconds"],
                env=build_subprocess_env(),
            )
        except subprocess.TimeoutExpired:
            if not ignore_errors:
                raise CommandError(f"Timed out stopping container '{name}'.")
            continue

        if result.returncode != 0 and not ignore_errors and "No such container" not in (result.stderr or result.stdout):
            raise CommandError((result.stderr or result.stdout).strip())

    try:
        subprocess.run(
            ["docker", "network", "rm", STACK_NETWORK],
            capture_output=True,
            text=True,
            timeout=OBSERVABILITY_TIMEOUTS["network_remove_seconds"],
            env=build_subprocess_env(),
        )
    except subprocess.TimeoutExpired:
        if not ignore_errors:
            raise CommandError(f"Timed out removing Docker network '{STACK_NETWORK}'.")


def start_with_docker_run(args: argparse.Namespace) -> None:
    subprocess.run(
        ["docker", "network", "create", STACK_NETWORK],
        capture_output=True,
        text=True,
        env=build_subprocess_env(),
    )

    host_args = [] if sys.platform.startswith("win") else ["--add-host", "host.docker.internal:host-gateway"]

    run_checked(
        [
            "docker",
            "run",
            "-d",
            "--name",
            JAEGER_CONTAINER,
            "--network",
            STACK_NETWORK,
            "-p",
            f"{args.jaeger_port}:16686",
            "-p",
            f"{args.otlp_grpc_port}:4317",
            "-p",
            f"{args.otlp_http_port}:4318",
            "-e",
            f"COLLECTOR_OTLP_ENABLED={OBSERVABILITY_DOCKER['collector_otlp_enabled']}",
            JAEGER_IMAGE,
        ],
        "Unable to start Jaeger.",
    )

    run_checked(
        [
            "docker",
            "run",
            "-d",
            "--name",
            PROMETHEUS_CONTAINER,
            "--network",
            STACK_NETWORK,
            "-p",
            f"{args.prometheus_port}:9090",
            *host_args,
            "-v",
            f"{PROMETHEUS_FILE.resolve()}:/etc/prometheus/prometheus.yml:ro",
            PROMETHEUS_IMAGE,
            "--config.file=/etc/prometheus/prometheus.yml",
        ],
        "Unable to start Prometheus.",
    )

    run_checked(
        [
            "docker",
            "run",
            "-d",
            "--name",
            GRAFANA_CONTAINER,
            "--network",
            STACK_NETWORK,
            "-p",
            f"{args.grafana_port}:3000",
            "-e",
            f"GF_AUTH_ANONYMOUS_ENABLED={GRAFANA_ENVIRONMENT['GF_AUTH_ANONYMOUS_ENABLED']}",
            "-e",
            f"GF_AUTH_ANONYMOUS_ORG_ROLE={GRAFANA_ENVIRONMENT['GF_AUTH_ANONYMOUS_ORG_ROLE']}",
            "-e",
            f"GF_AUTH_DISABLE_LOGIN_FORM={GRAFANA_ENVIRONMENT['GF_AUTH_DISABLE_LOGIN_FORM']}",
            "-e",
            f"GF_USERS_DEFAULT_THEME={GRAFANA_ENVIRONMENT['GF_USERS_DEFAULT_THEME']}",
            "-v",
            f"{GRAFANA_PROVISIONING_DIR.resolve()}:/etc/grafana/provisioning:ro",
            GRAFANA_IMAGE,
        ],
        "Unable to start Grafana.",
    )


def write_stack_files(args: argparse.Namespace) -> None:
    write_text(
        PROMETHEUS_FILE,
        build_prometheus_config(
            api_host=args.api_host,
            api_port=args.api_port,
            scrape_interval=args.scrape_interval,
        ),
    )
    write_text(
        GRAFANA_DATASOURCES_FILE,
        build_grafana_datasources(),
    )
    write_text(
        GRAFANA_DASHBOARD_PROVIDER_FILE,
        build_grafana_dashboard_provider(),
    )
    write_text(
        GRAFANA_DASHBOARD_FILE,
        json.dumps(
            build_grafana_dashboard(
                grafana_port=args.grafana_port,
                jaeger_port=args.jaeger_port,
                prometheus_port=args.prometheus_port,
                otlp_endpoint=f"http://localhost:{args.otlp_grpc_port}",
            ),
            indent=2,
        ) + "\n",
    )
    write_text(
        COMPOSE_FILE,
        build_compose_file(args),
    )


def write_text(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")


def build_prometheus_config(
    api_host: str,
    api_port: int,
    scrape_interval: str,
) -> str:
    return textwrap.dedent(
        f"""
        global:
          scrape_interval: {scrape_interval}
          evaluation_interval: {scrape_interval}

        scrape_configs:
          - job_name: clientmanager-api
            metrics_path: /prometheus/otel
            static_configs:
              - targets:
                  - {api_host}:{api_port}
        """
    ).strip() + "\n"


def build_grafana_datasources() -> str:
    return textwrap.dedent(
        """
        apiVersion: 1
        datasources:
          - name: ClientManager Prometheus
            uid: clientmanager-prometheus
            type: prometheus
            access: proxy
            url: http://prometheus:9090
            isDefault: true

          - name: ClientManager Jaeger
            uid: clientmanager-jaeger
            type: jaeger
            access: proxy
            url: http://jaeger:16686
        """
    ).strip() + "\n"


def build_grafana_dashboard_provider() -> str:
    return textwrap.dedent(
        """
        apiVersion: 1
        providers:
          - name: ClientManager
            folder: ClientManager
            type: file
            allowUiUpdates: false
            disableDeletion: false
            options:
              path: /etc/grafana/provisioning/dashboards/clientmanager
        """
    ).strip() + "\n"


def build_grafana_dashboard(grafana_port: int, jaeger_port: int, prometheus_port: int, otlp_endpoint: str) -> dict:
    api_trace_search_url = build_grafana_trace_search_url(grafana_port, "ClientManager.Api")
    panels = [
        text_panel(
            panel_id=1,
            title="How To Use This Stack",
            content=textwrap.dedent(
                f"""
                ## ClientManager Observability

                Metrics are preloaded in this dashboard from the API host.

                Traces are available in Grafana Explore via the **ClientManager Jaeger** datasource.
                Search by service name:

                - `ClientManager.Api`

                Direct links:

                - [Grafana Home](http://localhost:{grafana_port})
                - [Grafana API Trace Search]({api_trace_search_url})
                - [Prometheus](http://localhost:{prometheus_port})
                - [Jaeger UI](http://localhost:{jaeger_port})

                OTLP endpoint to set on the API host:

                - `{otlp_endpoint}`
                """
            ).strip(),
            x=0,
            y=0,
            w=24,
            h=7,
        ),
        stat_panel(
            panel_id=2,
            title="API Requests / Sec",
            expression="sum(rate(clientmanager_requests_total{job=\"clientmanager-api\"}[$__rate_interval]))",
            unit="ops",
            x=0,
            y=7,
            w=6,
            h=5,
        ),
        stat_panel(
            panel_id=3,
            title="Access Granted / Sec",
            expression="sum(rate(clientmanager_access_granted_total{job=\"clientmanager-api\"}[$__rate_interval]))",
            unit="ops",
            x=6,
            y=7,
            w=6,
            h=5,
        ),
        stat_panel(
            panel_id=4,
            title="API Errors / Sec",
            expression="sum(rate(clientmanager_requests_errors_total{job=\"clientmanager-api\"}[$__rate_interval]))",
            unit="ops",
            x=12,
            y=7,
            w=6,
            h=5,
        ),
        stat_panel(
            panel_id=5,
            title="Access Denied / Sec",
            expression="sum(rate(clientmanager_access_denied_total{job=\"clientmanager-api\"}[$__rate_interval]))",
            unit="ops",
            x=18,
            y=7,
            w=6,
            h=5,
        ),
        timeseries_panel(
            panel_id=6,
            title="API Request P95",
            expression=(
                "histogram_quantile(0.95, "
                "sum by (le) (rate({__name__=~\"clientmanager_requests_duration(_milliseconds)?_bucket\", "
                "job=\"clientmanager-api\"}[$__rate_interval])))"
            ),
            legend_format="API",
            unit="ms",
            x=0,
            y=12,
            w=24,
            h=8,
        ),
        timeseries_panel(
            panel_id=7,
            title="Storage Document Store P95 By Operation",
            expression=(
                "histogram_quantile(0.95, "
                "sum by (le, operation) (rate({__name__=~\"clientmanager_storage_document_store_duration(_milliseconds)?_bucket\", "
                "job=\"clientmanager-api\"}[$__rate_interval])))"
            ),
            legend_format="{{operation}}",
            unit="ms",
            x=0,
            y=20,
            w=12,
            h=8,
        ),
        timeseries_panel(
            panel_id=8,
            title="Storage Rate-Limit Strategy P95 By Strategy",
            expression=(
                "histogram_quantile(0.95, "
                "sum by (le, strategy) (rate({__name__=~\"clientmanager_storage_ratelimit_strategy_duration(_milliseconds)?_bucket\", "
                "job=\"clientmanager-api\"}[$__rate_interval])))"
            ),
            legend_format="{{strategy}}",
            unit="ms",
            x=12,
            y=20,
            w=12,
            h=8,
        ),
        timeseries_panel(
            panel_id=9,
            title="Storage Access Check P95",
            expression=(
                "histogram_quantile(0.95, "
                "sum by (le) (rate({__name__=~\"clientmanager_storage_access_duration(_milliseconds)?_bucket\", "
                "job=\"clientmanager-api\"}[$__rate_interval])))"
            ),
            legend_format="Access",
            unit="ms",
            x=0,
            y=28,
            w=12,
            h=8,
        ),
        timeseries_panel(
            panel_id=10,
            title="Storage Resource Acquire P95",
            expression=(
                "histogram_quantile(0.95, "
                "sum by (le) (rate({__name__=~\"clientmanager_storage_resources_acquire_duration(_milliseconds)?_bucket\", "
                "job=\"clientmanager-api\"}[$__rate_interval])))"
            ),
            legend_format="Acquire",
            unit="ms",
            x=12,
            y=28,
            w=12,
            h=8,
        ),
        timeseries_panel(
            panel_id=11,
            title="API Error Rate By Endpoint",
            expression=(
                "sum by (endpoint, statusCode) (rate(clientmanager_requests_errors_total{job=\"clientmanager-api\"}[$__rate_interval]))"
            ),
            legend_format="{{statusCode}} {{endpoint}}",
            unit="ops",
            x=0,
            y=36,
            w=24,
            h=8,
        ),
    ]

    return {
        "annotations": {"list": []},
        "editable": False,
        "fiscalYearStartMonth": 0,
        "graphTooltip": 0,
        "id": None,
        "links": [],
        "liveNow": False,
        "panels": panels,
        "refresh": DASHBOARD_SETTINGS["refresh"],
        "schemaVersion": DASHBOARD_SETTINGS["schema_version"],
        "style": DASHBOARD_SETTINGS["style"],
        "tags": DASHBOARD_SETTINGS["tags"],
        "templating": {"list": []},
        "time": DASHBOARD_SETTINGS["time_range"],
        "timepicker": {},
        "timezone": "browser",
        "title": DASHBOARD_SETTINGS["title"],
        "uid": DASHBOARD_SETTINGS["uid"],
        "version": 1,
        "weekStart": "",
    }


def build_grafana_trace_search_url(grafana_port: int, service_name: str) -> str:
    pane_state = {
        "trace": {
            "datasource": "clientmanager-jaeger",
            "queries": [
                {
                    "refId": "A",
                    "queryType": "search",
                    "service": service_name,
                    "datasource": {
                        "type": "jaeger",
                        "uid": "clientmanager-jaeger",
                    },
                }
            ],
            "range": DASHBOARD_SETTINGS["trace_search_time_range"],
            "compact": False,
        }
    }
    encoded = urllib.parse.quote(json.dumps(pane_state, separators=(",", ":")))
    return f"http://localhost:{grafana_port}/explore?schemaVersion=1&panes={encoded}&orgId=1"


def text_panel(panel_id: int, title: str, content: str, x: int, y: int, w: int, h: int) -> dict:
    return {
        "datasource": None,
        "gridPos": {"h": h, "w": w, "x": x, "y": y},
        "id": panel_id,
        "options": {"content": content, "mode": "markdown"},
        "title": title,
        "type": "text",
    }


def stat_panel(panel_id: int, title: str, expression: str, unit: str, x: int, y: int, w: int, h: int) -> dict:
    return {
        "datasource": {"type": "prometheus", "uid": "clientmanager-prometheus"},
        "fieldConfig": {
            "defaults": {
                "color": {"mode": "thresholds"},
                "mappings": [],
                "thresholds": {"mode": "absolute", "steps": [{"color": "green", "value": None}]},
                "unit": unit,
            },
            "overrides": [],
        },
        "gridPos": {"h": h, "w": w, "x": x, "y": y},
        "id": panel_id,
        "options": {
            "colorMode": "value",
            "graphMode": "area",
            "justifyMode": "auto",
            "orientation": "auto",
            "reduceOptions": {"calcs": ["lastNotNull"], "fields": "", "values": False},
            "textMode": "auto",
        },
        "targets": [
            {
                "datasource": {"type": "prometheus", "uid": "clientmanager-prometheus"},
                "expr": expression,
                "legendFormat": title,
                "refId": "A",
            }
        ],
        "title": title,
        "type": "stat",
    }


def timeseries_panel(
    panel_id: int,
    title: str,
    expression: str,
    legend_format: str,
    unit: str,
    x: int,
    y: int,
    w: int,
    h: int,
) -> dict:
    return {
        "datasource": {"type": "prometheus", "uid": "clientmanager-prometheus"},
        "fieldConfig": {
            "defaults": {
                "color": {"mode": "palette-classic"},
                "custom": {
                    "axisCenteredZero": False,
                    "axisColorMode": "text",
                    "axisLabel": "",
                    "axisPlacement": "auto",
                    "drawStyle": "line",
                    "fillOpacity": 10,
                    "gradientMode": "none",
                    "hideFrom": {"legend": False, "tooltip": False, "viz": False},
                    "lineInterpolation": "linear",
                    "lineWidth": 2,
                    "pointSize": 4,
                    "showPoints": "never",
                    "spanNulls": False,
                    "stacking": {"group": "A", "mode": "none"},
                    "thresholdsStyle": {"mode": "off"},
                },
                "mappings": [],
                "thresholds": {"mode": "absolute", "steps": [{"color": "green", "value": None}]},
                "unit": unit,
            },
            "overrides": [],
        },
        "gridPos": {"h": h, "w": w, "x": x, "y": y},
        "id": panel_id,
        "options": {
            "legend": {"calcs": [], "displayMode": "list", "placement": "bottom", "showLegend": True},
            "tooltip": {"mode": "multi", "sort": "desc"},
        },
        "targets": [
            {
                "datasource": {"type": "prometheus", "uid": "clientmanager-prometheus"},
                "expr": expression,
                "legendFormat": legend_format,
                "refId": "A",
            }
        ],
        "title": title,
        "type": "timeseries",
    }


def build_compose_file(args: argparse.Namespace) -> str:
    return textwrap.dedent(
        f"""
        services:
          jaeger:
                        image: {JAEGER_IMAGE}
            container_name: {JAEGER_CONTAINER}
            environment:
                            COLLECTOR_OTLP_ENABLED: "{OBSERVABILITY_DOCKER['collector_otlp_enabled']}"
            ports:
              - "{args.jaeger_port}:16686"
              - "{args.otlp_grpc_port}:4317"
              - "{args.otlp_http_port}:4318"
            networks:
              - {STACK_NETWORK}

          prometheus:
                        image: {PROMETHEUS_IMAGE}
            container_name: {PROMETHEUS_CONTAINER}
            command:
              - --config.file=/etc/prometheus/prometheus.yml
            ports:
              - "{args.prometheus_port}:9090"
            volumes:
              - ./prometheus/prometheus.yml:/etc/prometheus/prometheus.yml:ro
            networks:
              - {STACK_NETWORK}

          grafana:
                        image: {GRAFANA_IMAGE}
            container_name: {GRAFANA_CONTAINER}
            depends_on:
              - prometheus
              - jaeger
            environment:
                            GF_AUTH_ANONYMOUS_ENABLED: "{GRAFANA_ENVIRONMENT['GF_AUTH_ANONYMOUS_ENABLED']}"
                            GF_AUTH_ANONYMOUS_ORG_ROLE: {GRAFANA_ENVIRONMENT['GF_AUTH_ANONYMOUS_ORG_ROLE']}
                            GF_AUTH_DISABLE_LOGIN_FORM: "{GRAFANA_ENVIRONMENT['GF_AUTH_DISABLE_LOGIN_FORM']}"
                            GF_USERS_DEFAULT_THEME: {GRAFANA_ENVIRONMENT['GF_USERS_DEFAULT_THEME']}
            ports:
              - "{args.grafana_port}:3000"
            volumes:
              - ./grafana/provisioning:/etc/grafana/provisioning:ro
            networks:
              - {STACK_NETWORK}

        networks:
          {STACK_NETWORK}:
            name: {STACK_NETWORK}
        """
    ).strip() + "\n"


def wait_for_http(url: str, name: str, timeout_seconds: int = OBSERVABILITY_TIMEOUTS["wait_for_http_seconds"]) -> None:
    deadline = time.time() + timeout_seconds
    last_error = ""
    while time.time() < deadline:
        try:
            with urllib.request.urlopen(url, timeout=REQUEST_TIMEOUT_SECONDS) as response:
                if response.status < 500:
                    return
        except urllib.error.URLError as error:
            last_error = str(error)
        except http.client.RemoteDisconnected as error:
            last_error = str(error)
        except ConnectionAbortedError as error:
            last_error = str(error)
        except ConnectionResetError as error:
            last_error = str(error)
        except TimeoutError as error:  # pragma: no cover - depends on local environment
            last_error = str(error)

        time.sleep(OBSERVABILITY_TIMEOUTS["poll_interval_seconds"])

    detail = f" Last error: {last_error}" if last_error else ""
    raise CommandError(f"Timed out waiting for {name} at {url}.{detail}")


def report_scrape_target(label: str, url: str) -> None:
    try:
        with urllib.request.urlopen(url, timeout=REQUEST_TIMEOUT_SECONDS) as response:
            status = response.status
        print(f"{label}: reachable ({status})")
    except Exception as error:  # pragma: no cover - depends on local app state
        print(f"{label}: not reachable yet ({error})")


def print_env_snippet(otlp_endpoint: str) -> None:
    print("Set this in the same PowerShell session before starting the API host:")
    print()
    print(f'$env:Observability__OtlpEndpoint = "{otlp_endpoint}"')
    print()
    print("Then start:")
    print("  - ClientManager.Api")


def print_appsettings_snippet(otlp_endpoint: str) -> None:
    snippet = {"Observability": {"OtlpEndpoint": otlp_endpoint}}
    print("Add this to the appsettings.Development.json file:")
    print()
    print(json.dumps(snippet, indent=2))
    print()
    print("Files:")
    print("  - ClientManager.Api/appsettings.Development.json")


def print_launchsettings_snippet(otlp_endpoint: str) -> None:
    snippet = {"environmentVariables": {"Observability__OtlpEndpoint": otlp_endpoint}}
    print("Add this to the active launch profile in the launchSettings.json file:")
    print()
    print(json.dumps(snippet, indent=2))
    print()
    print("Files:")
    print("  - ClientManager.Api/Properties/launchSettings.json")


if __name__ == "__main__":
    raise SystemExit(main())