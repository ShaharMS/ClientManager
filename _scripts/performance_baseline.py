"""
Runs a deterministic baseline load profile against the public ClientManager API.

The profile approximates 1M requests/day by pacing requests at a configurable rate,
reuses the seeded IDs from traffic_generator.py, and reports latency plus Json-file
storage sizes from ClientManager.Api so later optimization steps have a stable
before-state to compare.
"""

from __future__ import annotations

import argparse
import json
import random
import statistics
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

from configuration import CONFIGURATION

GLOBAL_SETTINGS = CONFIGURATION["global"]
BASELINE_SETTINGS = CONFIGURATION["scripts"]["performance_baseline"]

DEFAULT_BASE_URL = GLOBAL_SETTINGS["api"]["base_url"]
API_PREFIX = GLOBAL_SETTINGS["api"]["prefix"]
DEFAULT_REQUESTS_PER_DAY = BASELINE_SETTINGS["defaults"]["requests_per_day"]
DEFAULT_DURATION_SECONDS = BASELINE_SETTINGS["defaults"]["duration_seconds"]
DEFAULT_VIRTUAL_CLIENTS = BASELINE_SETTINGS["defaults"]["virtual_clients"]
DEFAULT_SEED = BASELINE_SETTINGS["defaults"]["seed"]
DEFAULT_DATA_DIRECTORY = GLOBAL_SETTINGS["data"]["repo_data_directory"]
SEARCH_QUERY = GLOBAL_SETTINGS["queries"]["search_body"]
CLIENT_SUMMARIES_PAGE_SIZE = GLOBAL_SETTINGS["queries"]["client_summaries_page_size"]
ENABLED_CLIENTS = GLOBAL_SETTINGS["catalogs"]["enabled_client_ids"]
CLIENT_SERVICES = GLOBAL_SETTINGS["catalogs"]["client_services"]
CLIENT_POOLS = GLOBAL_SETTINGS["catalogs"]["client_pools"]
REAL_SERVICE_IDS = sorted({service_id for services in CLIENT_SERVICES.values() for service_id in services})
REAL_POOL_IDS = sorted({pool_id for pools in CLIENT_POOLS.values() for pool_id in pools})
DEFAULT_GRAPH_RANGES = BASELINE_SETTINGS["defaults"]["graph_ranges"]
GRAPH_RANGE_PRESETS = BASELINE_SETTINGS["graph"]["range_presets"]
HEAVY_WEIGHT_PROBABILITY = BASELINE_SETTINGS["virtual_clients"]["heavy_weight_probability"]
HEAVY_WEIGHT_VALUE = BASELINE_SETTINGS["virtual_clients"]["heavy_weight_value"]
DEFAULT_WEIGHT_VALUE = BASELINE_SETTINGS["virtual_clients"]["default_weight_value"]
MONITOR_WINDOW_SECONDS = BASELINE_SETTINGS["graph"]["monitor_window_seconds"]
SECONDS_PER_DAY = BASELINE_SETTINGS["timing"]["seconds_per_day"]
MINIMUM_ELAPSED_SECONDS = BASELINE_SETTINGS["timing"]["minimum_elapsed_seconds"]
LATENCY_PERCENTILE = BASELINE_SETTINGS["metrics"]["latency_percentile"]
PREFER_PRIMARY_SERVICE_EVERY = BASELINE_SETTINGS["routing"]["prefer_primary_service_every"]
PREFER_PRIMARY_POOL_EVERY = BASELINE_SETTINGS["routing"]["prefer_primary_pool_every"]
ACTION_WEIGHTS_WITH_GRAPH = BASELINE_SETTINGS["action_weights"]["with_graph"]
ACTION_WEIGHTS_WITHOUT_GRAPH = BASELINE_SETTINGS["action_weights"]["without_graph"]
USAGE_SNAPSHOTS_FILE = GLOBAL_SETTINGS["data"]["usage_snapshots_file"]
COUNTERS_FILE = GLOBAL_SETTINGS["data"]["counters_file"]


def api_call(base_url: str, method: str, path: str, body: dict | None = None) -> tuple[int, object | None, float]:
    request = urllib.request.Request(
        f"{base_url.rstrip('/')}/{path.lstrip('/')}",
        data=json.dumps(body).encode() if body is not None else None,
        method=method,
        headers={"Content-Type": "application/json"},
    )
    start_time = time.perf_counter()
    try:
        with urllib.request.urlopen(request) as response:
            payload = response.read()
            latency_ms = (time.perf_counter() - start_time) * 1000
            if not payload:
                return response.status, None, latency_ms

            try:
                return response.status, json.loads(payload), latency_ms
            except json.JSONDecodeError:
                return response.status, payload.decode(errors="ignore"), latency_ms
    except urllib.error.HTTPError as error:
        latency_ms = (time.perf_counter() - start_time) * 1000
        payload = error.read()
        if not payload:
            return error.code, None, latency_ms

        try:
            return error.code, json.loads(payload), latency_ms
        except json.JSONDecodeError:
            return error.code, payload.decode(errors="ignore"), latency_ms
    except Exception as error:  # pragma: no cover - smoke path for local runs
        latency_ms = (time.perf_counter() - start_time) * 1000
        return 0, str(error), latency_ms


def build_virtual_clients(count: int, seed: int) -> list[dict]:
    randomizer = random.Random(seed)
    clients: list[dict] = []
    for index in range(count):
        actual_client = ENABLED_CLIENTS[index % len(ENABLED_CLIENTS)]
        services = CLIENT_SERVICES[actual_client]
        pools = CLIENT_POOLS.get(actual_client, [])
        clients.append(
            {
                "virtual_id": f"virtual-{index + 1:03d}",
                "client_id": actual_client,
                "services": services,
                "pools": pools,
                "preferred_service": services[index % len(services)],
                "preferred_pool": pools[index % len(pools)] if pools else None,
                "weight": HEAVY_WEIGHT_VALUE if randomizer.random() < HEAVY_WEIGHT_PROBABILITY else DEFAULT_WEIGHT_VALUE,
            }
        )
    return clients


def percentile(values: list[float], value: float) -> float:
    if not values:
        return 0.0
    ordered = sorted(values)
    index = max(0, min(len(ordered) - 1, round((len(ordered) - 1) * value)))
    return ordered[index]


def snapshot_sizes(data_directory: Path) -> dict[str, int]:
    return {
        "UsageSnapshots": data_directory.joinpath(USAGE_SNAPSHOTS_FILE).stat().st_size
        if data_directory.joinpath(USAGE_SNAPSHOTS_FILE).exists()
        else 0,
        "_counters": data_directory.joinpath(COUNTERS_FILE).stat().st_size
        if data_directory.joinpath(COUNTERS_FILE).exists()
        else 0,
    }


def format_utc(timestamp: float) -> str:
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(timestamp))


def build_query_path(path: str, params: dict[str, str]) -> str:
    return f"{path}?{urllib.parse.urlencode(params)}"


def parse_graph_ranges(value: str) -> list[str]:
    keys = []
    for token in value.split(","):
        key = token.strip().lower()
        if not key:
            continue
        if key not in GRAPH_RANGE_PRESETS:
            raise argparse.ArgumentTypeError(
                f"Unsupported graph range '{token}'. Use one of: {', '.join(GRAPH_RANGE_PRESETS)}"
            )
        if key not in keys:
            keys.append(key)
    return keys or list(GRAPH_RANGE_PRESETS)


def pick_graph_range(available_keys: list[str], preferred_key: str) -> str:
    preferred_days = GRAPH_RANGE_PRESETS[preferred_key]["days"]
    ordered = sorted(available_keys, key=lambda key: GRAPH_RANGE_PRESETS[key]["days"])
    for key in ordered:
        if GRAPH_RANGE_PRESETS[key]["days"] >= preferred_days:
            return key
    return ordered[-1]


def clients_for_service(service_id: str) -> list[str]:
    return sorted(client_id for client_id in ENABLED_CLIENTS if service_id in CLIENT_SERVICES[client_id])


def choose_graph_service_id() -> str:
    return max(REAL_SERVICE_IDS, key=lambda service_id: (len(clients_for_service(service_id)), service_id))


def create_graph_scenario(
    name: str,
    filter_type: str,
    target_ids: list[str],
    client_ids: list[str] | None,
    range_key: str,
) -> dict[str, object]:
    range_spec = GRAPH_RANGE_PRESETS[range_key]
    now = time.time()
    params = {
        "filterType": filter_type,
        "targetIds": ",".join(target_ids),
        "from": format_utc(now - (range_spec["days"] * SECONDS_PER_DAY)),
        "to": format_utc(now),
        "granularity": range_spec["granularity"],
    }
    path = f"{API_PREFIX}/statistics/historical-usage"
    if client_ids is not None:
        path = f"{path}/by-client"
        params["clientIds"] = ",".join(client_ids)

    return {
        "name": f"{name}_{range_key}",
        "path": build_query_path(path, params),
        "filter_type": filter_type,
        "range_key": range_key,
        "granularity": range_spec["granularity"],
        "target_count": len(target_ids),
        "client_count": len(client_ids or []),
    }


def build_graph_scenarios(graph_range_keys: list[str]) -> list[dict[str, object]]:
    service_range_key = pick_graph_range(graph_range_keys, "7d")
    pools_range_key = pick_graph_range(graph_range_keys, "90d")
    pools_by_client_range_key = pick_graph_range(graph_range_keys, "30d")
    service_id = choose_graph_service_id()
    service_clients = clients_for_service(service_id)
    pool_clients = sorted(client_id for client_id in ENABLED_CLIENTS if CLIENT_POOLS.get(client_id))
    return [
        create_graph_scenario("historical_usage_all_services", "Service", REAL_SERVICE_IDS, None, service_range_key),
        create_graph_scenario("historical_usage_all_pools", "ResourcePool", REAL_POOL_IDS, None, pools_range_key),
        create_graph_scenario("historical_usage_service_by_client", "Service", [service_id], service_clients, service_range_key),
        create_graph_scenario("historical_usage_pools_by_client", "ResourcePool", REAL_POOL_IDS, pool_clients, pools_by_client_range_key),
    ]


def run_dashboard_read(base_url: str) -> tuple[int, float]:
    endpoints = [
        ("GET", f"{API_PREFIX}/statistics/overview", None),
        ("GET", f"{API_PREFIX}/statistics/global-usage", None),
        ("POST", f"{API_PREFIX}/statistics/resource-pools/search", SEARCH_QUERY),
        ("GET", f"{API_PREFIX}/statistics/client-summaries?pageSize={CLIENT_SUMMARIES_PAGE_SIZE}", None),
    ]
    total_ms = 0.0
    final_status = 200
    for method, path, body in endpoints:
        status, _, latency_ms = api_call(base_url, method, path, body)
        total_ms += latency_ms
        if status >= 400 or status == 0:
            final_status = status
    return final_status, total_ms


def run_monitor_read(base_url: str, service_id: str) -> tuple[int, float]:
    now = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    thirty_minutes_ago = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(time.time() - MONITOR_WINDOW_SECONDS))
    endpoints = [
        (
            "GET",
            f"{API_PREFIX}/statistics/usage-timeseries?filterType=Service&targetIds={service_id}&from={thirty_minutes_ago}&to={now}&granularity=FiveMinute",
            None,
        ),
        (
            "GET",
            f"{API_PREFIX}/statistics/client-usage-breakdown?filterType=Service&targetIds={service_id}&from={thirty_minutes_ago}&to={now}&granularity=FiveMinute",
            None,
        ),
    ]
    total_ms = 0.0
    final_status = 200
    for method, path, body in endpoints:
        status, _, latency_ms = api_call(base_url, method, path, body)
        total_ms += latency_ms
        if status >= 400 or status == 0:
            final_status = status
    return final_status, total_ms


def run_graph_read(base_url: str, scenario: dict[str, object]) -> tuple[int, float]:
    status, _, latency_ms = api_call(base_url, "GET", str(scenario["path"]))
    return status, latency_ms


def build_status_counts(samples: list[dict]) -> dict[str, int]:
    counts: dict[str, int] = {}
    for sample in samples:
        key = str(sample["status"])
        counts[key] = counts.get(key, 0) + 1
    return dict(sorted(counts.items(), key=lambda item: item[0]))


def flatten_samples(sample_groups: dict[str, list[dict]]) -> list[dict]:
    return [sample for group in sample_groups.values() for sample in group]


def summarize_operation(samples: list[dict], elapsed_seconds: float) -> dict[str, float | int]:
    latencies = [sample["latency_ms"] for sample in samples]
    successes = sum(1 for sample in samples if 200 <= sample["status"] < 300)
    return {
        "count": len(samples),
        "successes": successes,
        "service_unavailable_count": sum(1 for sample in samples if sample["status"] == 503),
        "status_counts": build_status_counts(samples),
        "throughput_per_second": round(len(samples) / elapsed_seconds, 3) if elapsed_seconds > 0 else 0.0,
        "average_latency_ms": round(statistics.fmean(latencies), 3) if latencies else 0.0,
        "p95_latency_ms": round(percentile(latencies, LATENCY_PERCENTILE), 3) if latencies else 0.0,
        "max_latency_ms": round(max(latencies), 3) if latencies else 0.0,
    }


def summarize_graph_samples(graph_samples: dict[str, list[dict]], elapsed_seconds: float) -> dict[str, float | int]:
    combined = flatten_samples(graph_samples)
    summary = summarize_operation(combined, elapsed_seconds)
    return {
        "count": summary["count"],
        "successes": summary["successes"],
        "service_unavailable_count": summary["service_unavailable_count"],
        "graph_p95_latency_ms": summary["p95_latency_ms"],
        "graph_max_latency_ms": summary["max_latency_ms"],
        "status_counts": summary["status_counts"],
    }


def summarize_hot_path(samples: list[dict], elapsed_seconds: float) -> dict[str, float | int]:
    summary = summarize_operation(samples, elapsed_seconds)
    return {
        **summary,
        "expected_client_error_count": sum(1 for sample in samples if 400 <= sample["status"] < 500),
        "unexpected_error_count": sum(1 for sample in samples if sample["status"] == 0 or sample["status"] >= 500),
    }


def summarize_unexpected_runtime_failures(sample_groups: dict[str, list[dict]]) -> list[dict[str, object]]:
    failures: list[dict[str, object]] = []
    for operation_name, samples in sample_groups.items():
        unexpected = [sample for sample in samples if sample["status"] == 0 or sample["status"] >= 500]
        if not unexpected:
            continue
        failures.append(
            {
                "operation": operation_name,
                "count": len(unexpected),
                "status_counts": build_status_counts(unexpected),
                "max_latency_ms": round(max(sample["latency_ms"] for sample in unexpected), 3),
            }
        )
    return failures


def main() -> None:
    parser = argparse.ArgumentParser(description="Run a deterministic ClientManager performance baseline")
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL, help="Public API base URL")
    parser.add_argument("--requests-per-day", type=int, default=DEFAULT_REQUESTS_PER_DAY)
    parser.add_argument("--duration-seconds", type=int, default=DEFAULT_DURATION_SECONDS)
    parser.add_argument("--virtual-clients", type=int, default=DEFAULT_VIRTUAL_CLIENTS)
    parser.add_argument("--seed", type=int, default=DEFAULT_SEED)
    parser.add_argument("--include-graph-reads", action="store_true", help="Include long-range graph scenarios in the paced profile")
    parser.add_argument(
        "--graph-ranges",
        default=DEFAULT_GRAPH_RANGES,
        help="Comma-separated graph range presets to use when graph reads are enabled (7d,30d,90d)",
    )
    parser.add_argument("--data-directory", type=Path, default=DEFAULT_DATA_DIRECTORY, help="JsonFile data directory owned by ClientManager.Api")
    parser.add_argument("--output", type=Path, help="Write the JSON summary to this file instead of stdout")
    args = parser.parse_args()

    randomizer = random.Random(args.seed)
    virtual_clients = build_virtual_clients(args.virtual_clients, args.seed)
    graph_range_keys = parse_graph_ranges(args.graph_ranges)
    graph_scenarios = build_graph_scenarios(graph_range_keys) if args.include_graph_reads else []
    requests_per_second = args.requests_per_day / SECONDS_PER_DAY
    interval_seconds = 1 / requests_per_second if requests_per_second > 0 else 0
    storage_before = snapshot_sizes(args.data_directory)
    runtime_samples = {"access": [], "acquire": [], "release": [], "dashboard": [], "monitor": []}
    graph_samples = {str(scenario["name"]): [] for scenario in graph_scenarios}
    active_allocations: list[dict] = []
    graph_index = 0

    start_time = time.perf_counter()
    next_deadline = start_time
    total_operations = max(1, round(args.duration_seconds * requests_per_second))

    for index in range(total_operations):
        actor = randomizer.choice(virtual_clients)
        service_id = (
            actor["preferred_service"]
            if index % PREFER_PRIMARY_SERVICE_EVERY == 0
            else randomizer.choice(actor["services"])
        )
        action_weights = ACTION_WEIGHTS_WITH_GRAPH if graph_scenarios else ACTION_WEIGHTS_WITHOUT_GRAPH
        action_types = ["access", "acquire", "release", "dashboard", "monitor"]
        weights = [
            action_weights["access"],
            action_weights["acquire"],
            action_weights["release"] if active_allocations else action_weights["idle_release"],
            action_weights["dashboard"],
            action_weights["monitor"],
        ]
        if graph_scenarios:
            action_types.append("graph")
            weights.append(action_weights["graph"])
        action = randomizer.choices(action_types, weights=weights, k=1)[0]

        if action == "acquire" and not actor["preferred_pool"]:
            action = "access"
        elif action == "release" and not active_allocations:
            action = "access"

        if action == "access":
            status, _, latency_ms = api_call(
                args.base_url,
                "POST",
                f"{API_PREFIX}/access/check",
                {"clientId": actor["client_id"], "serviceId": service_id},
            )
            runtime_samples["access"].append({"status": status, "latency_ms": latency_ms})
        elif action == "acquire" and actor["preferred_pool"]:
            pool_id = (
                actor["preferred_pool"]
                if index % PREFER_PRIMARY_POOL_EVERY == 0
                else randomizer.choice(actor["pools"])
            )
            status, payload, latency_ms = api_call(
                args.base_url,
                "POST",
                f"{API_PREFIX}/resources/acquire",
                {"clientId": actor["client_id"], "resourcePoolId": pool_id},
            )
            runtime_samples["acquire"].append({"status": status, "latency_ms": latency_ms})
            if status == 200 and isinstance(payload, dict) and payload.get("allocationId"):
                active_allocations.append({"allocationId": payload["allocationId"]})
        elif action == "release" and active_allocations:
            allocation = active_allocations.pop(0)
            status, _, latency_ms = api_call(
                args.base_url,
                "POST",
                f"{API_PREFIX}/resources/release",
                {"allocationId": allocation["allocationId"]},
            )
            runtime_samples["release"].append({"status": status, "latency_ms": latency_ms})
        elif action == "dashboard":
            status, latency_ms = run_dashboard_read(args.base_url)
            runtime_samples["dashboard"].append({"status": status, "latency_ms": latency_ms})
        elif action == "monitor":
            status, latency_ms = run_monitor_read(args.base_url, service_id)
            runtime_samples["monitor"].append({"status": status, "latency_ms": latency_ms})
        elif action == "graph":
            scenario = graph_scenarios[graph_index % len(graph_scenarios)]
            graph_index += 1
            status, latency_ms = run_graph_read(args.base_url, scenario)
            graph_samples[str(scenario["name"])].append({"status": status, "latency_ms": latency_ms})
        else:
            raise RuntimeError(f"Unsupported benchmark action: {action}")

        next_deadline += interval_seconds
        sleep_seconds = next_deadline - time.perf_counter()
        if sleep_seconds > 0:
            time.sleep(sleep_seconds)

    for allocation in active_allocations:
        api_call(args.base_url, "POST", f"{API_PREFIX}/resources/release", {"allocationId": allocation["allocationId"]})

    elapsed_seconds = max(time.perf_counter() - start_time, MINIMUM_ELAPSED_SECONDS)
    storage_after = snapshot_sizes(args.data_directory)
    summary = {
        "profile": {
            "base_url": args.base_url,
            "seed": args.seed,
            "duration_seconds": args.duration_seconds,
            "target_requests_per_day": args.requests_per_day,
            "target_requests_per_second": round(requests_per_second, 3),
            "virtual_clients": args.virtual_clients,
            "distinct_services": len(REAL_SERVICE_IDS),
            "distinct_resource_pools": len(REAL_POOL_IDS),
            "graph_reads_enabled": bool(graph_scenarios),
            "graph_ranges": graph_range_keys,
        },
        "operations": {
            name: summarize_operation(operation_samples, elapsed_seconds)
            for name, operation_samples in runtime_samples.items()
        },
        "runtime_summary": summarize_operation(flatten_samples(runtime_samples), elapsed_seconds),
        "hot_path_summary": {
            "access_checks": summarize_hot_path(runtime_samples["access"], elapsed_seconds),
            "resource_acquires": summarize_hot_path(runtime_samples["acquire"], elapsed_seconds),
            "resource_releases": summarize_hot_path(runtime_samples["release"], elapsed_seconds),
        },
        "runtime_unexpected_failures": summarize_unexpected_runtime_failures(runtime_samples),
        "graph_operations": {
            name: summarize_operation(operation_samples, elapsed_seconds)
            for name, operation_samples in graph_samples.items()
        },
        "graph_summary": summarize_graph_samples(graph_samples, elapsed_seconds),
        "graph_scenarios": graph_scenarios,
        "storage_bytes": {
            "before": storage_before,
            "after": storage_after,
        },
    }

    summary_json = json.dumps(summary, indent=2)
    if args.output is None:
        print(summary_json)
        return

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(f"{summary_json}\n", encoding="utf-8")


if __name__ == "__main__":
    main()