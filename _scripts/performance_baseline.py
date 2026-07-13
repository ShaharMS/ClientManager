"""
Runs a deterministic baseline load profile against the public ClientManager API.

The profile approximates 1M requests/day by pacing access checks and overview reads,
reuses seeded IDs from configuration.py, and reports latency percentiles.
"""

from __future__ import annotations

import argparse
import random
import statistics
import time
import urllib.error
import urllib.parse
import urllib.request

from configuration import CONFIGURATION

GLOBAL_SETTINGS = CONFIGURATION["global"]
BASELINE_SETTINGS = CONFIGURATION["scripts"]["performance_baseline"]

DEFAULT_BASE_URL = GLOBAL_SETTINGS["api"]["base_url"]
API_PREFIX = GLOBAL_SETTINGS["api"]["prefix"]
DEFAULT_REQUESTS_PER_DAY = BASELINE_SETTINGS["defaults"]["requests_per_day"]
DEFAULT_DURATION_SECONDS = BASELINE_SETTINGS["defaults"]["duration_seconds"]
DEFAULT_VIRTUAL_CLIENTS = BASELINE_SETTINGS["defaults"]["virtual_clients"]
DEFAULT_SEED = BASELINE_SETTINGS["defaults"]["seed"]
ENABLED_CLIENTS = GLOBAL_SETTINGS["catalogs"]["enabled_client_ids"]
CLIENT_SERVICES = GLOBAL_SETTINGS["catalogs"]["client_services"]
HEAVY_WEIGHT_PROBABILITY = BASELINE_SETTINGS["virtual_clients"]["heavy_weight_probability"]
HEAVY_WEIGHT_VALUE = BASELINE_SETTINGS["virtual_clients"]["heavy_weight_value"]
DEFAULT_WEIGHT_VALUE = BASELINE_SETTINGS["virtual_clients"]["default_weight_value"]
SECONDS_PER_DAY = BASELINE_SETTINGS["timing"]["seconds_per_day"]
MINIMUM_ELAPSED_SECONDS = BASELINE_SETTINGS["timing"]["minimum_elapsed_seconds"]
LATENCY_PERCENTILE = BASELINE_SETTINGS["metrics"]["latency_percentile"]
ACTION_WEIGHTS = BASELINE_SETTINGS["action_weights"]


def api_call(base_url: str, method: str, path: str, params: dict | None = None) -> tuple[int, float]:
    url = f"{base_url.rstrip('/')}/{path.lstrip('/')}"
    if params:
        url = f"{url}?{urllib.parse.urlencode(params)}"
    request = urllib.request.Request(url, method=method)
    start_time = time.perf_counter()
    try:
        with urllib.request.urlopen(request) as response:
            latency_ms = (time.perf_counter() - start_time) * 1000
            response.read()
            return response.status, latency_ms
    except urllib.error.HTTPError as error:
        latency_ms = (time.perf_counter() - start_time) * 1000
        error.read()
        return error.code, latency_ms
    except Exception:
        latency_ms = (time.perf_counter() - start_time) * 1000
        return 0, latency_ms


def build_virtual_clients(count: int, seed: int) -> list[dict]:
    randomizer = random.Random(seed)
    clients: list[dict] = []
    for index in range(count):
        actual_client = ENABLED_CLIENTS[index % len(ENABLED_CLIENTS)]
        services = CLIENT_SERVICES[actual_client]
        weight = HEAVY_WEIGHT_VALUE if randomizer.random() < HEAVY_WEIGHT_PROBABILITY else DEFAULT_WEIGHT_VALUE
        clients.append(
            {
                "client_id": actual_client,
                "service_id": services[index % len(services)],
                "weight": weight,
            }
        )
    return clients


def percentile(values: list[float], ratio: float) -> float:
    if not values:
        return 0.0
    ordered = sorted(values)
    index = min(len(ordered) - 1, int(ratio * len(ordered)))
    return ordered[index]


def main() -> int:
    parser = argparse.ArgumentParser(description="Run deterministic API baseline load")
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL)
    parser.add_argument("--requests-per-day", type=int, default=DEFAULT_REQUESTS_PER_DAY)
    parser.add_argument("--duration-seconds", type=float, default=DEFAULT_DURATION_SECONDS)
    parser.add_argument("--virtual-clients", type=int, default=DEFAULT_VIRTUAL_CLIENTS)
    parser.add_argument("--seed", type=int, default=DEFAULT_SEED)
    args = parser.parse_args()
    if args.requests_per_day <= 0 or args.duration_seconds <= 0 or args.virtual_clients <= 0:
        parser.error("requests-per-day, duration-seconds, and virtual-clients must be positive")

    base_url = args.base_url.rstrip("/")
    target_requests = max(1, int(args.requests_per_day * (args.duration_seconds / SECONDS_PER_DAY)))
    interval = args.duration_seconds / target_requests
    actors = build_virtual_clients(args.virtual_clients, args.seed)
    randomizer = random.Random(args.seed)

    access_latencies: list[float] = []
    overview_latencies: list[float] = []
    status_counts: dict[int, int] = {}

    print(
        f"Baseline load: {target_requests} requests over {args.duration_seconds:.0f}s "
        f"({args.requests_per_day:,}/day pace) against {base_url}"
    )

    start = time.perf_counter()
    for request_index in range(target_requests):
        actor = randomizer.choices(actors, weights=[actor["weight"] for actor in actors])[0]
        action = randomizer.choices(
            ["access", "overview"],
            weights=[ACTION_WEIGHTS["access"], ACTION_WEIGHTS["overview"]],
        )[0]

        if action == "access":
            status, latency_ms = api_call(
                base_url,
                "GET",
                f"{API_PREFIX}/access/check",
                {"clientId": actor["client_id"], "serviceId": actor["service_id"]},
            )
            access_latencies.append(latency_ms)
        else:
            status, latency_ms = api_call(base_url, "GET", f"{API_PREFIX}/statistics/overview")
            overview_latencies.append(latency_ms)

        status_counts[status] = status_counts.get(status, 0) + 1

        elapsed = time.perf_counter() - start
        target_elapsed = (request_index + 1) * interval
        sleep_seconds = target_elapsed - elapsed
        if sleep_seconds > MINIMUM_ELAPSED_SECONDS:
            time.sleep(sleep_seconds)

    total_elapsed = max(time.perf_counter() - start, MINIMUM_ELAPSED_SECONDS)
    combined = access_latencies + overview_latencies
    print("\n== results ==")
    print(f"completed_requests: {target_requests}")
    print(f"elapsed_seconds: {total_elapsed:.2f}")
    print(f"achieved_rpm: {target_requests / total_elapsed * 60:.0f}")
    print(f"status_counts: {status_counts}")
    if combined:
        print(f"p50_ms: {percentile(combined, 0.50):.1f}")
        print(f"p95_ms: {percentile(combined, LATENCY_PERCENTILE):.1f}")
        print(f"max_ms: {max(combined):.1f}")
        print(f"mean_ms: {statistics.mean(combined):.1f}")
    failures = sum(count for status, count in status_counts.items() if status == 0 or status >= 500)
    if failures:
        print(f"\nFAILED: {failures} requests failed to reach a healthy API")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
