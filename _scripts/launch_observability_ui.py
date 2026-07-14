"""
Start the checked-in ClientManager observability compose stack.

Defaults to metrics-only (Prometheus + Grafana). Pass --traces for Tempo + OTel Collector.

  python _scripts/launch_observability_ui.py up
  python _scripts/launch_observability_ui.py up --traces
  python _scripts/launch_observability_ui.py down
"""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request
import webbrowser
from pathlib import Path

from configuration import CONFIGURATION

REPO_ROOT = Path(__file__).resolve().parent.parent
COMPOSE_FILES = [
    REPO_ROOT / "compose" / "observability.yml",
]
DEFAULTS = CONFIGURATION["scripts"]["launch_observability_ui"]["defaults"]
TIMEOUTS = CONFIGURATION["scripts"]["launch_observability_ui"]["timeouts"]


class CommandError(RuntimeError):
    pass


def parse_args() -> argparse.Namespace:
    raw_args = sys.argv[1:]
    if not raw_args:
        raw_args = ["up"]

    parser = argparse.ArgumentParser(description="Launch the checked-in observability compose stack.")
    subparsers = parser.add_subparsers(dest="command", required=True)

    up = subparsers.add_parser("up", help="Start Prometheus + Grafana (optional traces profile).")
    up.add_argument("--traces", action="store_true", help="Also start Tempo + OTel Collector.")
    up.add_argument("--grafana-port", type=int, default=DEFAULTS["grafana_port"])
    up.add_argument("--prometheus-port", type=int, default=DEFAULTS["prometheus_port"])
    up.add_argument("--open-browser", dest="open_browser", action="store_true")
    up.add_argument("--no-browser", dest="open_browser", action="store_false")
    up.set_defaults(open_browser=DEFAULTS["open_browser"])

    down = subparsers.add_parser("down", help="Stop the observability stack.")
    down.add_argument("--traces", action="store_true", help="Also stop trace services.")

    for name in ("print-env", "print-appsettings", "print-launchsettings"):
        snippet = subparsers.add_parser(name, help=f"Print OTLP snippet for {name}.")
        snippet.add_argument("--otlp-endpoint", default="http://localhost:4317")

    return parser.parse_args(raw_args)


def compose_base() -> list[str]:
    command = ["docker", "compose"]
    for compose_file in COMPOSE_FILES:
        command.extend(["-f", str(compose_file)])
    return command


def compose_up(traces: bool) -> None:
    command = compose_base() + ["up", "-d"]
    if traces:
        command.extend(["--profile", "traces"])
    run_checked(command, "Unable to start observability stack.")


def compose_down(traces: bool) -> None:
    command = compose_base() + ["down", "--remove-orphans"]
    if traces:
        command.extend(["--profile", "traces"])
    subprocess.run(command, cwd=REPO_ROOT, check=False)


def run_checked(command: list[str], message: str) -> None:
    result = subprocess.run(command, cwd=REPO_ROOT, capture_output=True, text=True)
    if result.returncode != 0:
        detail = (result.stderr or result.stdout).strip()
        raise CommandError(f"{message} {detail}".strip())


def wait_for_http(url: str, name: str) -> None:
    deadline = time.time() + TIMEOUTS["wait_for_http_seconds"]
    last_error = ""
    while time.time() < deadline:
        try:
            with urllib.request.urlopen(url, timeout=TIMEOUTS["http_request_seconds"]) as response:
                if response.status < 500:
                    return
        except (urllib.error.URLError, TimeoutError) as error:
            last_error = str(error)
        time.sleep(TIMEOUTS["poll_interval_seconds"])
    raise CommandError(f"Timed out waiting for {name} at {url}. {last_error}".strip())


def print_runbook() -> None:
    print("Runbook:")
    print("  API only (host):           dotnet run --project ClientManager.Api")
    print("  API + Admin UI:            docker compose up")
    print("  Multipod + observability:  edit docker-compose.yml includes, then docker compose up")
    print("  + traces:                  docker compose --profile traces up")
    print("  + load (multipod demo):    docker compose --profile load up")
    print("  Prod:                      see docs/observability/existing-monitoring.md")
    print()


def command_up(args: argparse.Namespace) -> None:
    compose_up(args.traces)
    grafana_url = f"http://localhost:{args.grafana_port}"
    prometheus_url = f"http://localhost:{args.prometheus_port}"
    wait_for_http(f"{grafana_url}/api/health", "Grafana")
    wait_for_http(f"{prometheus_url}/-/ready", "Prometheus")
    if args.traces:
        wait_for_http("http://localhost:3200/ready", "Tempo")

    print("Observability stack is ready.")
    print(f"Grafana:    {grafana_url}/d/clientmanager-observability")
    print(f"Prometheus: {prometheus_url}")
    if args.traces:
        print("Tempo:      http://localhost:3200")
        print('OTLP:       set Observability__OtlpEndpoint=http://localhost:4317 on API hosts')
    print()
    print_runbook()
    if args.open_browser:
        webbrowser.open(f"{grafana_url}/d/clientmanager-observability")


def command_down(args: argparse.Namespace) -> None:
    compose_down(args.traces)
    print("Observability stack stopped.")


def print_env_snippet(otlp_endpoint: str) -> None:
    print(f'$env:Observability__OtlpEndpoint = "{otlp_endpoint}"')


def print_appsettings_snippet(otlp_endpoint: str) -> None:
    print(json.dumps({"Observability": {"OtlpEndpoint": otlp_endpoint}}, indent=2))


def print_launchsettings_snippet(otlp_endpoint: str) -> None:
    print(json.dumps({"environmentVariables": {"Observability__OtlpEndpoint": otlp_endpoint}}, indent=2))


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
    except CommandError as error:
        print(f"error: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
