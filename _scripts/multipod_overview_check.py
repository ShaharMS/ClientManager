"""
Cross-pod overview consistency check for the lean multipod compose stack.

Seeds catalog data against pod :5062, then polls GET /api/v2/statistics/overview on
each API replica and verifies HTTP 200 plus stable client/service counts.
"""

from __future__ import annotations

import argparse
import json
import statistics
import subprocess
import sys
import time
import urllib.error
import urllib.request

from configuration import CONFIGURATION, REPO_ROOT

GLOBAL_SETTINGS = CONFIGURATION["global"]
API_PREFIX = GLOBAL_SETTINGS["api"]["prefix_with_leading_slash"]
DEFAULT_PORTS = (5062, 5063, 5064)
OVERVIEW_PATH = f"{API_PREFIX}/statistics/overview"
EXPECTED_CLIENTS = len(GLOBAL_SETTINGS["catalogs"]["clients"])
EXPECTED_SERVICES = len(GLOBAL_SETTINGS["catalogs"]["services"])


def get_json(base_url: str, path: str) -> tuple[int, dict | None, float]:
    url = f"{base_url.rstrip('/')}{path}"
    start = time.perf_counter()
    try:
        with urllib.request.urlopen(url, timeout=15) as response:
            payload = response.read()
            latency_ms = (time.perf_counter() - start) * 1000
            data = json.loads(payload) if payload else None
            return response.status, data, latency_ms
    except urllib.error.HTTPError as error:
        latency_ms = (time.perf_counter() - start) * 1000
        return error.code, None, latency_ms
    except Exception:
        latency_ms = (time.perf_counter() - start) * 1000
        return 0, None, latency_ms


def seed_catalog(base_url: str) -> None:
    command = [sys.executable, "_scripts/seed_data.py", "--base-url", base_url]
    print("+", " ".join(command))
    result = subprocess.run(command, cwd=str(REPO_ROOT))
    if result.returncode != 0:
        raise RuntimeError("seed_data.py failed")


def poll_overview(base_urls: dict[str, str], p50_budget_ms: float) -> dict[str, float]:
    medians: dict[str, float] = {}
    for label, base_url in base_urls.items():
        samples: list[float] = []
        data: dict | None = None
        for _ in range(5):
            status, data, latency_ms = get_json(base_url, OVERVIEW_PATH)
            if status != 200 or not isinstance(data, dict):
                raise RuntimeError(f"pod :{label} overview HTTP {status}")
            samples.append(latency_ms)
        median_ms = statistics.median(samples)
        medians[label] = median_ms
        clients = data.get("totalClients")
        services = data.get("totalServices")
        print(
            f"  pod :{label} overview p50={median_ms:.1f}ms "
            f"(clients={clients} services={services} rpm={data.get('requestsPerMinute')})"
        )
        if clients != EXPECTED_CLIENTS or services != EXPECTED_SERVICES:
            raise RuntimeError(
                f"pod :{label} returned clients={clients}, services={services}; "
                f"expected {EXPECTED_CLIENTS}, {EXPECTED_SERVICES}"
            )
        if median_ms > p50_budget_ms:
            raise RuntimeError(
                f"pod :{label} overview p50 {median_ms:.1f}ms exceeds budget {p50_budget_ms}ms"
            )
    return medians


def main() -> int:
    parser = argparse.ArgumentParser(description="Verify multipod overview consistency")
    parser.add_argument("--ports", default=",".join(str(port) for port in DEFAULT_PORTS))
    parser.add_argument("--p50-budget-ms", type=float, default=115.0)
    parser.add_argument("--skip-seed", action="store_true")
    args = parser.parse_args()

    ports = [int(part.strip()) for part in args.ports.split(",") if part.strip()]
    base_urls = {str(port): f"http://localhost:{port}" for port in ports}

    try:
        if not args.skip_seed:
            print("== seed catalog on primary pod ==")
            seed_catalog(base_urls[str(ports[0])])
            time.sleep(2)

        print("\n== cross-pod overview ==")
        poll_overview(base_urls, args.p50_budget_ms)
    except RuntimeError as error:
        print(f"\nFAILED: {error}")
        return 1

    print("\nOK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
