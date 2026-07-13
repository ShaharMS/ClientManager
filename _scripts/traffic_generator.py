"""
Generates semi-random live traffic against the public ClientManager API.

Before running this script, start ClientManager.Api and seed catalog data.

Modes:
  - hotpath (default): access checks only
  - prometheus: GET /prometheus/otel only

Usage:
    python traffic_generator.py [--base-url http://localhost:5062] [--interval 2.0]
    python traffic_generator.py --mode prometheus --workers 4
    Ctrl+C to stop.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import json
import random
import threading
import time
import urllib.error
import urllib.parse
import urllib.request

from configuration import CONFIGURATION

GLOBAL_SETTINGS = CONFIGURATION["global"]
TRAFFIC_SETTINGS = CONFIGURATION["scripts"]["traffic_generator"]

BASE_URL = GLOBAL_SETTINGS["api"]["base_url"]
INTERVAL = TRAFFIC_SETTINGS["defaults"]["interval_seconds"]
API_PREFIX = GLOBAL_SETTINGS["api"]["prefix_with_leading_slash"]
VALID_ACCESS_COMBINATION_PROBABILITY = TRAFFIC_SETTINGS["probabilities"]["valid_access_combination"]
BURST_SIZES = TRAFFIC_SETTINGS["burst"]["sizes"]
BURST_WEIGHTS = TRAFFIC_SETTINGS["burst"]["weights"]
STATS_EVERY_ITERATIONS = TRAFFIC_SETTINGS["timing"]["stats_every_iterations"]
MINIMUM_SLEEP_SECONDS = TRAFFIC_SETTINGS["timing"]["minimum_sleep_seconds"]
SLEEP_JITTER_MULTIPLIER = TRAFFIC_SETTINGS["timing"]["sleep_jitter_multiplier"]

ENABLED_CLIENTS = GLOBAL_SETTINGS["catalogs"]["enabled_client_ids"]
DISABLED_CLIENTS = GLOBAL_SETTINGS["catalogs"]["disabled_client_ids"]
ALL_CLIENTS = GLOBAL_SETTINGS["catalogs"]["all_client_ids"]
SERVICES = GLOBAL_SETTINGS["catalogs"]["service_ids"]
CLIENT_SERVICES = GLOBAL_SETTINGS["catalogs"]["client_services"]
CLIENT_WEIGHT = TRAFFIC_SETTINGS["client_weights"]

MODE = "hotpath"
WORKERS = 1
PROMETHEUS_PATH = "/prometheus/otel"

stats_lock = threading.Lock()
stats = {"requests": 0, "errors": 0, "total": 0}


def api(method: str, path: str, params=None, body=None):
    url = f"{BASE_URL.rstrip('/')}/{path.lstrip('/')}"
    if params:
        url = f"{url}?{urllib.parse.urlencode(params)}"
    data = None
    headers = {}
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=data, method=method, headers=headers)
    start_time = time.time()
    try:
        with urllib.request.urlopen(req) as resp:
            ms = (time.time() - start_time) * 1000
            payload = resp.read()
            if not payload:
                return resp.status, None, ms
            try:
                return resp.status, json.loads(payload), ms
            except json.JSONDecodeError:
                return resp.status, payload.decode(errors="ignore"), ms
    except urllib.error.HTTPError as error:
        ms = (time.time() - start_time) * 1000
        payload = error.read()
        if not payload:
            return error.code, None, ms
        try:
            return error.code, json.loads(payload), ms
        except json.JSONDecodeError:
            return error.code, payload.decode(errors="ignore"), ms
    except Exception as error:
        ms = (time.time() - start_time) * 1000
        return 0, str(error), ms


def record_request(status: int) -> None:
    with stats_lock:
        stats["total"] += 1
        stats["requests"] += 1
        if status == 0 or status >= 500:
            stats["errors"] += 1


def do_access_check() -> str:
    if random.random() < VALID_ACCESS_COMBINATION_PROBABILITY:
        client = random.choices(ENABLED_CLIENTS, weights=[CLIENT_WEIGHT[c] for c in ENABLED_CLIENTS])[0]
        service = random.choice(CLIENT_SERVICES[client])
    else:
        client = random.choice(ALL_CLIENTS)
        service = random.choice(SERVICES)

    status, _, ms = api("GET", f"{API_PREFIX}/access/check", {"clientId": client, "serviceId": service})
    record_request(status)
    return f"ACCESS  {client} -> {service}: {status} ({ms:.1f}MS)"


def do_prometheus_fetch() -> str:
    status, _, ms = api("GET", PROMETHEUS_PATH)
    record_request(status)
    return f"PROM    GET {PROMETHEUS_PATH}: {status} ({ms:.1f}MS)"


def do_request() -> str:
    if MODE == "prometheus":
        return do_prometheus_fetch()
    return do_access_check()


def run_burst() -> list[str]:
    burst_size = random.choices(BURST_SIZES, weights=BURST_WEIGHTS)[0]
    if WORKERS == 1:
        return [do_request() for _ in range(burst_size)]

    with concurrent.futures.ThreadPoolExecutor(max_workers=min(WORKERS, burst_size)) as executor:
        return list(executor.map(lambda _: do_request(), range(burst_size)))


def worker_loop(stop_event: threading.Event, worker_id: int | None = None) -> None:
    iteration = 0
    prefix = f"[w{worker_id}] " if worker_id is not None else ""
    while not stop_event.is_set():
        iteration += 1
        for line in run_burst():
            if line:
                print(f"{prefix}{line}")

        if iteration % STATS_EVERY_ITERATIONS == 0:
            print_stats(prefix=prefix)

        sleep_seconds = INTERVAL * (1 + random.uniform(-SLEEP_JITTER_MULTIPLIER, SLEEP_JITTER_MULTIPLIER))
        if stop_event.wait(max(MINIMUM_SLEEP_SECONDS, sleep_seconds)):
            return


def print_stats(prefix: str = "") -> None:
    with stats_lock:
        snapshot = dict(stats)
    elapsed = time.time() - snapshot.get("_start", time.time())
    mins = elapsed / 60
    rpm = snapshot["total"] / mins if mins > 0 else 0
    print(
        f"\n{prefix} Stats: {snapshot['total']} total ({rpm:.0f} req/min) | "
        f"mode={MODE} requests={snapshot['requests']} errors={snapshot['errors']}"
    )


def run() -> None:
    stats["_start"] = time.time()
    stop_event = threading.Event()

    print(f"Traffic generator running against {BASE_URL}")
    print(f"Mode: {MODE} | workers: {WORKERS} | average interval: {INTERVAL}s between bursts")
    print("Press Ctrl+C to stop.\n")

    try:
        if WORKERS == 1:
            worker_loop(stop_event)
            return

        with concurrent.futures.ThreadPoolExecutor(max_workers=WORKERS) as executor:
            futures = [executor.submit(worker_loop, stop_event, worker_id) for worker_id in range(1, WORKERS + 1)]
            concurrent.futures.wait(futures)
    except KeyboardInterrupt:
        stop_event.set()
        raise


def main() -> int:
    global BASE_URL, INTERVAL, MODE, WORKERS

    parser = argparse.ArgumentParser(description="Generate live API traffic")
    parser.add_argument("--base-url", default=BASE_URL)
    parser.add_argument("--interval", type=float, default=INTERVAL)
    parser.add_argument(
        "--mode",
        choices=("hotpath", "prometheus"),
        default="hotpath",
        help="hotpath: access checks only; prometheus: /prometheus/otel only (default: hotpath)",
    )
    parser.add_argument(
        "--workers",
        type=int,
        default=1,
        metavar="N",
        help="Concurrent worker threads to increase throughput (default: 1)",
    )
    args = parser.parse_args()
    if args.workers < 1:
        parser.error("--workers must be at least 1")

    BASE_URL = args.base_url.rstrip("/")
    INTERVAL = args.interval
    MODE = args.mode
    WORKERS = args.workers

    try:
        run()
    except KeyboardInterrupt:
        print_stats()
        print("\nStopped.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
