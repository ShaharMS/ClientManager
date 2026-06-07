"""
Generates semi-random live traffic against the public ClientManager API.

Before running this script, start ClientManager.Api.

Simulates realistic client behavior:
  - Access checks (most common)
  - Resource acquire / release cycles
  - Occasional stats & list reads
  - Varying request rates per client (busy vs. quiet)
  - Some requests that should fail (disabled client, wrong service, etc.)

Usage:
    python traffic_generator.py [--base-url http://localhost:5062] [--interval 2.0]
    Ctrl+C to stop.
"""

import argparse
import json
import random
import sys
import time
import urllib.request
import urllib.error
from datetime import datetime

from configuration import CONFIGURATION

GLOBAL_SETTINGS = CONFIGURATION["global"]
TRAFFIC_SETTINGS = CONFIGURATION["scripts"]["traffic_generator"]

BASE_URL = GLOBAL_SETTINGS["api"]["base_url"]
INTERVAL = TRAFFIC_SETTINGS["defaults"]["interval_seconds"]
API_PREFIX = GLOBAL_SETTINGS["api"]["prefix_with_leading_slash"]
READ_SEARCH_QUERY = GLOBAL_SETTINGS["queries"]["search_body"]
CLIENT_SUMMARIES_PAGE_SIZE = GLOBAL_SETTINGS["queries"]["client_summaries_page_size"]
VALID_ACCESS_COMBINATION_PROBABILITY = TRAFFIC_SETTINGS["probabilities"]["valid_access_combination"]
DETAILED_READ_PROBABILITY = TRAFFIC_SETTINGS["probabilities"]["detailed_read"]
BURST_SIZES = TRAFFIC_SETTINGS["burst"]["sizes"]
BURST_WEIGHTS = TRAFFIC_SETTINGS["burst"]["weights"]
ACTION_TYPES = TRAFFIC_SETTINGS["actions"]["types"]
ACTION_WEIGHTS = TRAFFIC_SETTINGS["actions"]["weights"]
STATS_EVERY_ITERATIONS = TRAFFIC_SETTINGS["timing"]["stats_every_iterations"]
MINIMUM_SLEEP_SECONDS = TRAFFIC_SETTINGS["timing"]["minimum_sleep_seconds"]
SLEEP_JITTER_MULTIPLIER = TRAFFIC_SETTINGS["timing"]["sleep_jitter_multiplier"]

# ── Known IDs (must match seed_data.py) ───────────────────────────────────

ENABLED_CLIENTS = GLOBAL_SETTINGS["catalogs"]["enabled_client_ids"]
DISABLED_CLIENTS = GLOBAL_SETTINGS["catalogs"]["disabled_client_ids"]
ALL_CLIENTS = GLOBAL_SETTINGS["catalogs"]["all_client_ids"]

SERVICES = GLOBAL_SETTINGS["catalogs"]["service_ids"]
RESOURCE_POOLS = GLOBAL_SETTINGS["catalogs"]["resource_pool_ids"]

# Client -> services they have access to (for realistic traffic)
CLIENT_SERVICES = GLOBAL_SETTINGS["catalogs"]["client_services"]

# Client -> pools they can use
CLIENT_POOLS = GLOBAL_SETTINGS["catalogs"]["client_pools"]

# Relative traffic weight per client (higher = more requests)
CLIENT_WEIGHT = TRAFFIC_SETTINGS["client_weights"]

# Track active allocations so we can release them
active_allocations: list[dict] = []

# Stats
stats = {"access_checks": 0, "acquires": 0, "releases": 0, "reads": 0, "errors": 0, "total": 0}


def api(method: str, path: str, body=None):
    url = f"{BASE_URL.rstrip('/')}/{path.lstrip('/')}"
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
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
    except urllib.error.HTTPError as e:
        ms = (time.time() - start_time) * 1000
        payload = e.read()
        if not payload:
            return e.code, None, ms

        try:
            return e.code, json.loads(payload), ms
        except json.JSONDecodeError:
            return e.code, payload.decode(errors="ignore"), ms
    except Exception as e:
        ms = (time.time() - start_time) * 1000
        return 0, str(e), ms


def do_access_check():
    """Simulate a client checking access to a service."""
    # 85% chance: pick a valid client+service combo; 15% chance: pick something that might fail
    if random.random() < VALID_ACCESS_COMBINATION_PROBABILITY:
        client = random.choices(ENABLED_CLIENTS, weights=[CLIENT_WEIGHT[c] for c in ENABLED_CLIENTS])[0]
        service = random.choice(CLIENT_SERVICES[client])
    else:
        client = random.choice(ALL_CLIENTS)
        service = random.choice(SERVICES)

    status, resp, ms = api("POST", f"{API_PREFIX}/access/check", {"clientId": client, "serviceId": service})
    stats["access_checks"] += 1
    return f"ACCESS  {client} -> {service}: {status} ({ms:.1f}MS)"


def do_acquire():
    """Acquire a resource slot."""
    client = random.choices(ENABLED_CLIENTS, weights=[CLIENT_WEIGHT[c] for c in ENABLED_CLIENTS])[0]
    pools = CLIENT_POOLS.get(client, [])
    if not pools:
        return None

    pool = random.choice(pools)
    status, resp, ms = api("POST", f"{API_PREFIX}/resources/acquire", {"clientId": client, "resourcePoolId": pool})
    stats["acquires"] += 1

    if status == 200 and isinstance(resp, dict) and "allocationId" in resp:
        active_allocations.append({"allocationId": resp["allocationId"], "client": client, "pool": pool})
        return f"ACQUIRE {client} -> {pool}: OK (alloc={resp['allocationId'][:8]}..., active={len(active_allocations)}) ({ms:.1f}MS)"
    return f"ACQUIRE {client} -> {pool}: {status} ({ms:.1f}MS)"


def do_release():
    """Release a previously acquired resource."""
    if not active_allocations:
        return None

    alloc = active_allocations.pop(random.randrange(len(active_allocations)))
    status, _, ms = api("POST", f"{API_PREFIX}/resources/release", {"allocationId": alloc["allocationId"]})
    stats["releases"] += 1
    return f"RELEASE {alloc['client']} <- {alloc['pool']}: {status} (active={len(active_allocations)}) ({ms:.1f}MS)"


def do_read():
    """Hit a read-only endpoint (list clients, services, stats, etc.)."""
    choices = [
        ("GET", f"{API_PREFIX}/statistics/overview", None),
        ("GET", f"{API_PREFIX}/statistics/global-usage", None),
        ("GET", f"{API_PREFIX}/statistics/client-summaries?pageSize={CLIENT_SUMMARIES_PAGE_SIZE}", None),
        ("POST", f"{API_PREFIX}/statistics/clients/search", READ_SEARCH_QUERY),
        ("POST", f"{API_PREFIX}/statistics/services/search", READ_SEARCH_QUERY),
        ("POST", f"{API_PREFIX}/statistics/resource-pools/search", READ_SEARCH_QUERY),
        ("POST", f"{API_PREFIX}/clients/search", READ_SEARCH_QUERY),
        ("POST", f"{API_PREFIX}/services/search", READ_SEARCH_QUERY),
        ("POST", f"{API_PREFIX}/resource-pools/search", READ_SEARCH_QUERY),
        ("POST", f"{API_PREFIX}/global-rate-limits/search", READ_SEARCH_QUERY),
    ]

    # Also occasionally look up a specific client or their accessibility
    if random.random() < DETAILED_READ_PROBABILITY:
        client = random.choice(ENABLED_CLIENTS)
        choices.extend([
            ("GET", f"{API_PREFIX}/clients/{client}", None),
            ("GET", f"{API_PREFIX}/access/{client}", None),
            ("GET", f"{API_PREFIX}/statistics/clients/{client}", None),
        ])

    method, path, body = random.choice(choices)
    status, _, ms = api(method, path, body)
    stats["reads"] += 1
    return f"READ    {method} {path}: {status} ({ms:.1f}MS)"


def print_stats():
    elapsed = time.time() - stats.get("_start", time.time())
    mins = elapsed / 60
    rpm = stats["total"] / mins if mins > 0 else 0
    print(f"\n  ┌─ Stats: {stats['total']} total ({rpm:.0f} req/min) | "
          f"checks={stats['access_checks']} acquires={stats['acquires']} "
          f"releases={stats['releases']} reads={stats['reads']} | "
          f"active_allocs={len(active_allocations)}")


def run():
    stats["_start"] = time.time()
    iteration = 0

    print(f"Traffic generator running against {BASE_URL}")
    print(f"Average interval: {INTERVAL}s between bursts")
    print("Press Ctrl+C to stop.\n")

    while True:
        iteration += 1

        # Each burst does 1–5 actions
        burst_size = random.choices(BURST_SIZES, weights=BURST_WEIGHTS)[0]
        timestamp = datetime.now().strftime("%H:%M:%S")

        for _ in range(burst_size):
            # Weight the action types
            action = random.choices(ACTION_TYPES, weights=ACTION_WEIGHTS)[0]

            result = None
            if action == "access_check":
                result = do_access_check()
            elif action == "acquire":
                result = do_acquire()
            elif action == "release":
                result = do_release()
            elif action == "read":
                result = do_read()

            if result:
                stats["total"] += 1
                print(f"  [{timestamp}] {result}")

        # Print periodic stats
        if iteration % STATS_EVERY_ITERATIONS == 0:
            print_stats()

        # Jittered sleep
        sleep = max(MINIMUM_SLEEP_SECONDS, random.gauss(INTERVAL, INTERVAL * SLEEP_JITTER_MULTIPLIER))
        time.sleep(sleep)


def main():
    global BASE_URL, INTERVAL

    parser = argparse.ArgumentParser(description="Generate random traffic against the public ClientManager API")
    parser.add_argument("--base-url", default=BASE_URL, help="Public API base URL")
    parser.add_argument("--interval", type=float, default=INTERVAL, help="Average seconds between bursts")
    args = parser.parse_args()

    BASE_URL = args.base_url.rstrip("/")
    INTERVAL = args.interval

    try:
        run()
    except KeyboardInterrupt:
        print_stats()
        print("  └─ Stopped.\n")


if __name__ == "__main__":
    main()
