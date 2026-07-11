"""ponytail: verify statistics latency and cross-pod consistency across API replicas."""

from __future__ import annotations

import argparse
import json
import random
import statistics
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timedelta, timezone

from configuration import CONFIGURATION, REPO_ROOT

GLOBAL = CONFIGURATION["global"]
API_PREFIX = GLOBAL["api"]["prefix_with_leading_slash"]
ENABLED_CLIENTS = GLOBAL["catalogs"]["enabled_client_ids"]
SERVICES = GLOBAL["catalogs"]["service_ids"]
CLIENT_SERVICES = GLOBAL["catalogs"]["client_services"]


def api_url(base_url: str, path: str, params: dict | None = None) -> str:
    root = base_url.rstrip("/")
    suffix = path.lstrip("/")
    url = f"{root}/{suffix}"
    if params:
        url = f"{url}?{urllib.parse.urlencode(params)}"
    return url


def get_json(base_url: str, path: str) -> dict:
    request = urllib.request.Request(api_url(base_url, path), method="GET")
    with urllib.request.urlopen(request, timeout=120) as response:
        return json.loads(response.read())


def post_json(base_url: str, path: str, body: dict) -> tuple[int, dict, float]:
    url = api_url(base_url, path)
    payload = json.dumps(body).encode()
    request = urllib.request.Request(
        url,
        data=payload,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    start = time.perf_counter()
    try:
        with urllib.request.urlopen(request, timeout=120) as response:
            data = json.loads(response.read())
            return response.status, data, (time.perf_counter() - start) * 1000
    except urllib.error.HTTPError as error:
        error.read()
        return error.code, {}, (time.perf_counter() - start) * 1000


def live_timeseries_body(**overrides) -> dict:
    now = datetime.now(timezone.utc)
    body = {
        "searchCategory": "ServiceRequests",
        "fromUtc": (now - timedelta(minutes=30)).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "toUtc": now.strftime("%Y-%m-%dT%H:%M:%SZ"),
        "bucketCount": 12,
    }
    body.update(overrides)
    return body


def sum_granted(response: dict) -> int:
    total = 0
    for target in response.get("targets", []):
        for bucket in target.get("aggregateBuckets", []):
            total += int(bucket.get("grantedCount", 0))
    return total


def sum_denied(response: dict) -> int:
    total = 0
    for target in response.get("targets", []):
        for bucket in target.get("aggregateBuckets", []):
            total += int(bucket.get("deniedUnauthenticatedCount", 0))
            total += int(bucket.get("deniedBlockedCount", 0))
            total += int(bucket.get("deniedRateLimitedCount", 0))
            total += int(bucket.get("deniedCapacityLimitedCount", 0))
    return total


def run_steady_poll(base_url: str, polls: int, interval_seconds: float) -> list[float]:
    latencies: list[float] = []
    body = live_timeseries_body()
    post_json(base_url, f"{API_PREFIX}/statistics/timeseries/search", body)
    for _ in range(polls):
        _, _, latency_ms = post_json(base_url, f"{API_PREFIX}/statistics/timeseries/search", body)
        latencies.append(latency_ms)
        time.sleep(interval_seconds)
    return latencies


def round_robin_traffic(
    base_urls: list[str],
    seconds: float,
    interval: float,
    stop_event: threading.Event | None = None,
) -> int:
    combos: list[tuple[str, str]] = []
    for client_id in ENABLED_CLIENTS:
        for service_id in CLIENT_SERVICES.get(client_id, SERVICES):
            combos.append((client_id, service_id))

    deadline = time.monotonic() + seconds
    sent = 0
    index = 0
    while time.monotonic() < deadline and (stop_event is None or not stop_event.is_set()):
        client_id, service_id = combos[index % len(combos)]
        base_url = base_urls[index % len(base_urls)]
        params = {"clientId": client_id, "serviceId": service_id}
        request = urllib.request.Request(
            api_url(base_url, f"{API_PREFIX}/access/check", params),
            method="GET",
        )
        try:
            with urllib.request.urlopen(request, timeout=30):
                pass
        except urllib.error.HTTPError:
            pass
        sent += 1
        index += 1
        time.sleep(interval * random.uniform(0.5, 1.5))
    return sent


def poll_timeseries_all_pods(
    base_urls: list[str],
    pod_labels: list[str],
) -> dict[str, float]:
    body = live_timeseries_body()
    latencies: dict[str, float] = {}
    for base_url, label in zip(base_urls, pod_labels, strict=True):
        status, _, latency_ms = post_json(base_url, f"{API_PREFIX}/statistics/timeseries/search", body)
        if status < 400:
            latencies[label] = latency_ms
    return latencies


def run_traffic_with_inflight_polls(
    base_urls: list[str],
    pod_labels: list[str],
    traffic_seconds: float,
    traffic_interval: float,
    poll_interval: float,
) -> tuple[int, dict[str, list[float]]]:
    latencies_by_pod: dict[str, list[float]] = {label: [] for label in pod_labels}
    stop_event = threading.Event()
    sent_holder: list[int] = []

    def traffic_worker() -> None:
        sent_holder.append(
            round_robin_traffic(base_urls, traffic_seconds, traffic_interval, stop_event)
        )

    thread = threading.Thread(target=traffic_worker, daemon=True)
    thread.start()

    poll_timeseries_all_pods(base_urls, pod_labels)

    deadline = time.monotonic() + traffic_seconds
    while time.monotonic() < deadline:
        for label, latency_ms in poll_timeseries_all_pods(base_urls, pod_labels).items():
            latencies_by_pod[label].append(latency_ms)
        time.sleep(poll_interval)

    stop_event.set()
    thread.join(timeout=30)
    sent = sent_holder[0] if sent_holder else 0
    return sent, latencies_by_pod


def check_inflight_latency_budget(
    pod_labels: list[str],
    latencies_by_pod: dict[str, list[float]],
    p50_budget_ms: float,
    max_budget_ms: float,
) -> list[str]:
    failures: list[str] = []
    for label in pod_labels:
        samples = latencies_by_pod.get(label, [])
        if not samples:
            failures.append(f"pod :{label} no in-flight latency samples")
            continue
        p50 = statistics.median(samples)
        peak = max(samples)
        print(f"pod :{label} in-flight: n={len(samples)} p50={p50:.1f}ms max={peak:.1f}ms")
        if p50 >= p50_budget_ms:
            failures.append(f"pod :{label} in-flight p50 {p50:.1f}ms >= {p50_budget_ms:.0f}ms")
        if peak >= max_budget_ms:
            failures.append(f"pod :{label} in-flight max {peak:.1f}ms >= {max_budget_ms:.0f}ms")
    return failures


def compare_pod_totals(
    pod_labels: list[str],
    granted: list[int],
    denied: list[int],
    rpm: list[float],
) -> list[str]:
    failures: list[str] = []
    if not granted:
        return ["no granted totals collected"]

    max_granted = max(granted)
    min_granted = min(granted)
    if max_granted > 0:
        ratio = min_granted / max_granted
        print(f"granted spread: min={min_granted} max={max_granted} min/max={ratio:.3f}")
        if ratio < 0.85:
            failures.append(
                f"granted counts diverge across pods (min/max={ratio:.3f}); "
                f"values={dict(zip(pod_labels, granted, strict=True))}"
            )
        pod_count = len(granted)
        if pod_count > 1 and abs(ratio - (1 / pod_count)) < 0.08:
            failures.append(
                f"granted counts look like per-pod fractions (~1/{pod_count}): "
                f"{dict(zip(pod_labels, granted, strict=True))}"
            )

    if max(denied) > 0:
        ratio = min(denied) / max(denied)
        print(f"denied spread: min={min(denied)} max={max(denied)} min/max={ratio:.3f}")
        if ratio < 0.85:
            failures.append(f"denied counts diverge across pods: {dict(zip(pod_labels, denied, strict=True))}")

    if max(rpm) > 0:
        ratio = min(rpm) / max(rpm)
        print(f"overview rpm spread: min={min(rpm):.1f} max={max(rpm):.1f} min/max={ratio:.3f}")
        if ratio < 0.85:
            failures.append(f"overview RequestsPerMinute diverges: {dict(zip(pod_labels, rpm, strict=True))}")

    return failures


def main() -> int:
    parser = argparse.ArgumentParser(description="Multi-pod statistics verification")
    parser.add_argument(
        "--base-urls",
        nargs="+",
        default=["http://localhost:5062", "http://localhost:5063", "http://localhost:5064"],
    )
    parser.add_argument("--traffic-seconds", type=float, default=45.0)
    parser.add_argument("--traffic-interval", type=float, default=0.15)
    parser.add_argument("--poll-interval", type=float, default=2.0, help="Timeseries poll interval during traffic")
    parser.add_argument("--p50-budget-ms", type=float, default=100.0)
    parser.add_argument("--max-budget-ms", type=float, default=500.0)
    parser.add_argument("--skip-inflight-latency", action="store_true")
    parser.add_argument("--skip-seed", action="store_true", help="Skip catalog seed (seed_data.py --skip-history)")
    args = parser.parse_args()

    if not args.skip_seed:
        print("== seed catalog (no history) ==")
        seed_cmd = [
            sys.executable,
            "_scripts/seed_data.py",
            "--base-url",
            args.base_urls[0],
            "--skip-history",
        ]
        result = subprocess.run(seed_cmd, cwd=str(REPO_ROOT), text=True)
        if result.returncode != 0:
            print("FAILED: catalog seed failed")
            return 1

    failures: list[str] = []
    pod_labels = [url.rstrip("/").split(":")[-1] for url in args.base_urls]

    print("== health ==")
    for base_url, label in zip(args.base_urls, pod_labels, strict=True):
        try:
            overview = get_json(base_url, f"{API_PREFIX}/statistics/overview")
            print(f"pod :{label} overview rpm={overview.get('requestsPerMinute', 0):.1f}")
        except Exception as error:
            failures.append(f"pod :{label} unreachable: {error}")

    if failures:
        print("FAILED:")
        for failure in failures:
            print(f" - {failure}")
        return 1

    print(f"\n== traffic + in-flight timeseries polls ({args.traffic_seconds:.0f}s) ==")
    if args.skip_inflight_latency:
        sent = round_robin_traffic(args.base_urls, args.traffic_seconds, args.traffic_interval)
        latencies_by_pod: dict[str, list[float]] = {}
        print(f"sent {sent} access checks across {len(args.base_urls)} pods")
    else:
        sent, latencies_by_pod = run_traffic_with_inflight_polls(
            args.base_urls,
            pod_labels,
            args.traffic_seconds,
            args.traffic_interval,
            args.poll_interval,
        )
        print(f"sent {sent} access checks across {len(args.base_urls)} pods")
        failures.extend(
            check_inflight_latency_budget(
                pod_labels,
                latencies_by_pod,
                args.p50_budget_ms,
                args.max_budget_ms,
            )
        )

    time.sleep(3.0)

    print("\n== cross-pod totals (live 30m timeseries) ==")
    body = live_timeseries_body()
    granted: list[int] = []
    denied: list[int] = []
    for base_url, label in zip(args.base_urls, pod_labels, strict=True):
        status, data, latency_ms = post_json(base_url, f"{API_PREFIX}/statistics/timeseries/search", body)
        if status >= 400:
            failures.append(f"pod :{label} timeseries HTTP {status}")
            continue
        g = sum_granted(data)
        d = sum_denied(data)
        granted.append(g)
        denied.append(d)
        print(f"pod :{label} granted={g} denied={d} latency={latency_ms:.1f}ms")

    rpm: list[float] = []
    for base_url, label in zip(args.base_urls, pod_labels, strict=True):
        overview = get_json(base_url, f"{API_PREFIX}/statistics/overview")
        rpm.append(float(overview.get("requestsPerMinute", 0)))
        print(f"pod :{label} overview rpm={rpm[-1]:.1f}")

    failures.extend(compare_pod_totals(pod_labels, granted, denied, rpm))

    if failures:
        print("\nFAILED:")
        for failure in failures:
            print(f" - {failure}")
        return 1

    print("\nOK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
