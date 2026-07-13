"""
Sustained access-check load test for rate-limit storage throughput.

Default target is 18,000 RPM (300 RPS) for 60 seconds against GET /api/v1/access/check.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import statistics
import threading
import time
import urllib.error
import urllib.parse
import urllib.request

from configuration import CONFIGURATION

GLOBAL_SETTINGS = CONFIGURATION["global"]
LOAD_SETTINGS = CONFIGURATION["scripts"]["access_load"]
API_PREFIX = GLOBAL_SETTINGS["api"]["prefix_with_leading_slash"]

DEFAULT_BASE_URL = GLOBAL_SETTINGS["api"]["base_url"]
DEFAULT_TARGET_RPM = LOAD_SETTINGS["defaults"]["target_rpm"]
DEFAULT_DURATION_SECONDS = LOAD_SETTINGS["defaults"]["duration_seconds"]
DEFAULT_CONCURRENCY = LOAD_SETTINGS["defaults"]["concurrency"]
DEFAULT_CLIENT_ID = LOAD_SETTINGS["defaults"]["client_id"]
DEFAULT_SERVICE_ID = LOAD_SETTINGS["defaults"]["service_id"]


def access_check(base_url: str, client_id: str, service_id: str) -> tuple[int, float]:
    url = (
        f"{base_url.rstrip('/')}{API_PREFIX}/access/check?"
        f"{urllib.parse.urlencode({'clientId': client_id, 'serviceId': service_id})}"
    )
    request = urllib.request.Request(url, method="GET")
    start = time.perf_counter()
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            response.read()
            return response.status, (time.perf_counter() - start) * 1000
    except urllib.error.HTTPError as error:
        error.read()
        return error.code, (time.perf_counter() - start) * 1000
    except Exception:
        return 0, (time.perf_counter() - start) * 1000


def percentile(values: list[float], ratio: float) -> float:
    if not values:
        return 0.0
    ordered = sorted(values)
    index = min(len(ordered) - 1, int(ratio * len(ordered)))
    return ordered[index]


def main() -> int:
    parser = argparse.ArgumentParser(description="Run sustained access-check load")
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL)
    parser.add_argument("--target-rpm", type=int, default=DEFAULT_TARGET_RPM)
    parser.add_argument("--duration-seconds", type=float, default=DEFAULT_DURATION_SECONDS)
    parser.add_argument("--concurrency", type=int, default=DEFAULT_CONCURRENCY)
    parser.add_argument("--client-id", default=DEFAULT_CLIENT_ID)
    parser.add_argument("--service-id", default=DEFAULT_SERVICE_ID)
    args = parser.parse_args()

    base_url = args.base_url.rstrip("/")
    target_rps = args.target_rpm / 60.0
    target_requests = max(1, int(target_rps * args.duration_seconds))
    interval = args.duration_seconds / target_requests

    latencies: list[float] = []
    status_counts: dict[int, int] = {}
    lock = threading.Lock()
    request_counter = 0

    print(
        f"Access load: target {args.target_rpm:,} RPM ({target_rps:.1f} RPS) "
        f"for {args.duration_seconds:.0f}s against {base_url}"
    )
    print(f"client={args.client_id} service={args.service_id} concurrency={args.concurrency}")

    def worker():
        nonlocal request_counter
        while True:
            with lock:
                if request_counter >= target_requests:
                    return
                request_index = request_counter
                request_counter += 1
            scheduled_at = start + request_index * interval
            delay = scheduled_at - time.perf_counter()
            if delay > 0:
                time.sleep(delay)
            status, latency_ms = access_check(base_url, args.client_id, args.service_id)
            with lock:
                latencies.append(latency_ms)
                status_counts[status] = status_counts.get(status, 0) + 1

    start = time.perf_counter()
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.concurrency) as executor:
        futures = [executor.submit(worker) for _ in range(args.concurrency)]
        concurrent.futures.wait(futures)

    elapsed = max(time.perf_counter() - start, 0.001)
    achieved_rpm = len(latencies) / elapsed * 60
    ok = status_counts.get(200, 0)
    limited = status_counts.get(429, 0)
    errors = sum(count for code, count in status_counts.items() if code not in (200, 429))

    print("\n== results ==")
    print(f"completed_requests: {len(latencies)}")
    print(f"elapsed_seconds: {elapsed:.2f}")
    print(f"achieved_rpm: {achieved_rpm:.0f}")
    print(f"target_rpm: {args.target_rpm}")
    print(f"status_counts: {status_counts}")
    print(f"granted_200: {ok}")
    print(f"rate_limited_429: {limited}")
    print(f"other_errors: {errors}")
    if latencies:
        print(f"p50_ms: {percentile(latencies, 0.50):.1f}")
        print(f"p95_ms: {percentile(latencies, 0.95):.1f}")
        print(f"p99_ms: {percentile(latencies, 0.99):.1f}")
        print(f"max_ms: {max(latencies):.1f}")
        print(f"mean_ms: {statistics.mean(latencies):.1f}")

    if len(latencies) != target_requests:
        print(f"\nFAILED: completed {len(latencies)} of {target_requests} scheduled requests")
        return 1
    if errors:
        print(f"\nFAILED: {errors} requests returned neither 200 nor 429")
        return 1
    if ok == 0:
        print("\nFAILED: no request was granted; verify the catalog was seeded for this client/service")
        return 1
    if achieved_rpm < args.target_rpm * 0.90:
        print(f"\nFAILED: achieved RPM {achieved_rpm:.0f} is below 90% of target {args.target_rpm}")
        return 1

    print("\nOK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
