"""ponytail: assert dashboard statistics latency stays within budget on a running API."""

from __future__ import annotations

import argparse
import json
import statistics
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone

DEFAULT_BASE_URL = "http://localhost:5062/api/v1"


def post_search(base_url: str, body: dict) -> tuple[int, float]:
    url = f"{base_url.rstrip('/')}/statistics/timeseries/search"
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
            response.read()
            return response.status, (time.perf_counter() - start) * 1000
    except urllib.error.HTTPError as error:
        error.read()
        return error.code, (time.perf_counter() - start) * 1000


def live_body(**overrides) -> dict:
    now = datetime.now(timezone.utc)
    body = {
        "searchCategory": "ServiceRequests",
        "fromUtc": (now - timedelta(minutes=30)).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "toUtc": now.strftime("%Y-%m-%dT%H:%M:%SZ"),
        "bucketCount": 12,
    }
    body.update(overrides)
    return body


def run_steady_poll(base_url: str, polls: int, interval_seconds: float) -> list[float]:
    latencies: list[float] = []
    body = live_body()
    post_search(base_url, body)  # ponytail: warmup cold closed-base cache before measuring steady polls
    for _ in range(polls):
        _, latency_ms = post_search(base_url, body)
        latencies.append(latency_ms)
        time.sleep(interval_seconds)
    return latencies


def main() -> int:
    parser = argparse.ArgumentParser(description="Check statistics timeseries latency budgets")
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL)
    parser.add_argument("--polls", type=int, default=15)
    parser.add_argument("--interval", type=float, default=2.0)
    args = parser.parse_args()

    failures: list[str] = []
    now = datetime.now(timezone.utc)

    steady = run_steady_poll(args.base_url, args.polls, args.interval)
    steady_p50 = statistics.median(steady)
    steady_max = max(steady)
    print(f"steady all-services 30m: p50={steady_p50:.1f}ms max={steady_max:.1f}ms")
    if steady_p50 >= 100:
        failures.append(f"steady p50 {steady_p50:.1f}ms >= 100ms")
    if steady_max >= 500:
        failures.append(f"steady max {steady_max:.1f}ms >= 500ms")

    scenarios = [
        ("cold all-services 30m", live_body()),
        ("one service 30m", live_body(targetIds=["auth-service"])),
        ("one service one client", live_body(targetIds=["auth-service"], clientIds=["platform-core"])),
        (
            "all services 7d",
            live_body(
                fromUtc=(now - timedelta(days=7)).strftime("%Y-%m-%dT%H:%M:%SZ"),
                bucketCount=20,
            ),
        ),
        (
            "historical 30m",
            {
                "searchCategory": "ServiceRequests",
                "fromUtc": (now - timedelta(hours=2)).strftime("%Y-%m-%dT%H:%M:%SZ"),
                "toUtc": (now - timedelta(hours=1)).strftime("%Y-%m-%dT%H:%M:%SZ"),
                "bucketCount": 12,
            },
        ),
        (
            "pool allocations 30m",
            {
                "searchCategory": "ResourcePoolAllocations",
                "fromUtc": (now - timedelta(minutes=30)).strftime("%Y-%m-%dT%H:%M:%SZ"),
                "toUtc": now.strftime("%Y-%m-%dT%H:%M:%SZ"),
                "bucketCount": 12,
            },
        ),
    ]

    for label, body in scenarios:
        status, latency_ms = post_search(args.base_url, body)
        print(f"{label}: status={status} latency={latency_ms:.1f}ms")
        if status >= 400:
            failures.append(f"{label} returned HTTP {status}")

    if failures:
        print("FAILED:")
        for failure in failures:
            print(f" - {failure}")
        return 1

    print("OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
