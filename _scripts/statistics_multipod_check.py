"""ponytail: verify statistics latency and cross-pod consistency across API replicas."""

from __future__ import annotations

import argparse
import json
import random
import statistics
import subprocess
import sys
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


def steady_poll(base_url: str, polls: int, interval_seconds: float) -> list[float]:
    latencies: list[float] = []
    body = live_timeseries_body()
    post_json(base_url, f"{API_PREFIX}/statistics/timeseries/search", body)
    for _ in range(polls):
        _, _, latency_ms = post_json(base_url, f"{API_PREFIX}/statistics/timeseries/search", body)
        latencies.append(latency_ms)
        time.sleep(interval_seconds)
    return latencies


def round_robin_traffic(base_urls: list[str], seconds: float, interval: float) -> int:
    """Send access checks evenly across pods; returns request count."""
    combos: list[tuple[str, str]] = []
    for client_id in ENABLED_CLIENTS:
        for service_id in CLIENT_SERVICES.get(client_id, SERVICES):
            combos.append((client_id, service_id))

    deadline = time.monotonic() + seconds
    sent = 0
    index = 0
    while time.monotonic() < deadline:
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


def run_latency_script(base_url: str) -> bool:
    cmd = [sys.executable, "_scripts/statistics_latency_check.py", "--base-url", f"{base_url}{API_PREFIX}", "--polls", "8", "--interval", "1.0"]
    result = subprocess.run(cmd, capture_output=True, text=True, cwd=str(REPO_ROOT))
    print(result.stdout.rstrip())
    if result.stderr.strip():
        print(result.stderr.rstrip(), file=sys.stderr)
    return result.returncode == 0


def compare_pod_totals(pod_labels: list[str], granted: list[int], denied: list[int], rpm: list[float]) -> list[str]:
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
        expected_third = abs(ratio - (1 / pod_count)) < 0.08
        if expected_third and pod_count > 1:
            failures.append(
                f"granted counts look like per-pod fractions (~1/{pod_count}): "
                f"{dict(zip(pod_labels, granted, strict=True))}"
            )

    if max(denied) > 0:
        min_denied = min(denied)
        max_denied = max(denied)
        ratio = min_denied / max_denied if max_denied else 1.0
        print(f"denied spread: min={min_denied} max={max_denied} min/max={ratio:.3f}")
        if ratio < 0.85:
            failures.append(f"denied counts diverge across pods: {dict(zip(pod_labels, denied, strict=True))}")

    if max(rpm) > 0:
        min_rpm = min(rpm)
        max_rpm = max(rpm)
        ratio = min_rpm / max_rpm
        print(f"overview rpm spread: min={min_rpm:.1f} max={max_rpm:.1f} min/max={ratio:.3f}")
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
    parser.add_argument("--skip-latency-script", action="store_true")
    args = parser.parse_args()

    failures: list[str] = []
    pod_labels = [url.rstrip("/").split(":")[-1] for url in args.base_urls]

    print("== health ==")
    for base_url, label in zip(args.base_urls, pod_labels, strict=True):
        try:
            overview = get_json(base_url, f"{API_PREFIX}/statistics/overview")
            print(f"pod :{label} overview rpm={overview.get('requestsPerMinute', 0):.1f}")
        except Exception as error:  # ponytail: fail fast on unreachable pod
            failures.append(f"pod :{label} unreachable: {error}")

    if failures:
        print("FAILED:")
        for failure in failures:
            print(f" - {failure}")
        return 1

    print(f"\n== round-robin traffic ({args.traffic_seconds:.0f}s) ==")
    sent = round_robin_traffic(args.base_urls, args.traffic_seconds, args.traffic_interval)
    print(f"sent {sent} access checks across {len(args.base_urls)} pods")
    time.sleep(3.0)  # ponytail: let flush + overlay dedupe window pass

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

    if not args.skip_latency_script:
        print("\n== per-pod latency budget ==")
        for base_url, label in zip(args.base_urls, pod_labels, strict=True):
            print(f"\n--- pod :{label} ---")
            if not run_latency_script(base_url):
                failures.append(f"pod :{label} latency budget failed")

    if failures:
        print("\nFAILED:")
        for failure in failures:
            print(f" - {failure}")
        return 1

    print("\nOK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
