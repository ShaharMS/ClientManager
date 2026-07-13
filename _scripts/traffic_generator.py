"""
Generates semi-random live traffic against the public ClientManager API.

Before running this script, start ClientManager.Api and seed catalog data.

Simulates:
  - Access checks (primary)
  - Statistics overview and catalog list reads

Usage:
    python traffic_generator.py [--base-url http://localhost:5062] [--interval 2.0]
    Ctrl+C to stop.
"""

from __future__ import annotations

import argparse
import json
import random
import sys
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
READ_SEARCH_QUERY = GLOBAL_SETTINGS["queries"]["search_body"]
VALID_ACCESS_COMBINATION_PROBABILITY = TRAFFIC_SETTINGS["probabilities"]["valid_access_combination"]
DETAILED_READ_PROBABILITY = TRAFFIC_SETTINGS["probabilities"]["detailed_read"]
BURST_SIZES = TRAFFIC_SETTINGS["burst"]["sizes"]
BURST_WEIGHTS = TRAFFIC_SETTINGS["burst"]["weights"]
ACTION_TYPES = TRAFFIC_SETTINGS["actions"]["types"]
ACTION_WEIGHTS = TRAFFIC_SETTINGS["actions"]["weights"]
STATS_EVERY_ITERATIONS = TRAFFIC_SETTINGS["timing"]["stats_every_iterations"]
MINIMUM_SLEEP_SECONDS = TRAFFIC_SETTINGS["timing"]["minimum_sleep_seconds"]
SLEEP_JITTER_MULTIPLIER = TRAFFIC_SETTINGS["timing"]["sleep_jitter_multiplier"]

ENABLED_CLIENTS = GLOBAL_SETTINGS["catalogs"]["enabled_client_ids"]
DISABLED_CLIENTS = GLOBAL_SETTINGS["catalogs"]["disabled_client_ids"]
ALL_CLIENTS = GLOBAL_SETTINGS["catalogs"]["all_client_ids"]
SERVICES = GLOBAL_SETTINGS["catalogs"]["service_ids"]
CLIENT_SERVICES = GLOBAL_SETTINGS["catalogs"]["client_services"]
CLIENT_WEIGHT = TRAFFIC_SETTINGS["client_weights"]

stats = {"access_checks": 0, "reads": 0, "errors": 0, "total": 0}


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


def do_access_check():
    if random.random() < VALID_ACCESS_COMBINATION_PROBABILITY:
        client = random.choices(ENABLED_CLIENTS, weights=[CLIENT_WEIGHT[c] for c in ENABLED_CLIENTS])[0]
        service = random.choice(CLIENT_SERVICES[client])
    else:
        client = random.choice(ALL_CLIENTS)
        service = random.choice(SERVICES)

    status, _, ms = api("GET", f"{API_PREFIX}/access/check", {"clientId": client, "serviceId": service})
    stats["access_checks"] += 1
    if status == 0 or status >= 500:
        stats["errors"] += 1
    return f"ACCESS  {client} -> {service}: {status} ({ms:.1f}MS)"


def do_read():
    choices = [
        ("GET", f"{API_PREFIX}/statistics/overview", None, None),
        ("POST", f"{API_PREFIX}/clients/search", None, READ_SEARCH_QUERY),
        ("POST", f"{API_PREFIX}/services/search", None, READ_SEARCH_QUERY),
        ("POST", f"{API_PREFIX}/global-rate-limits/search", None, READ_SEARCH_QUERY),
    ]
    if random.random() < DETAILED_READ_PROBABILITY:
        client = random.choice(ENABLED_CLIENTS)
        choices.append(("GET", f"{API_PREFIX}/clients/{client}", None, None))

    method, path, params, body = random.choice(choices)
    status, _, ms = api(method, path, params, body)
    stats["reads"] += 1
    if status == 0 or status >= 500:
        stats["errors"] += 1
    return f"READ    {method} {path}: {status} ({ms:.1f}MS)"


def print_stats():
    elapsed = time.time() - stats.get("_start", time.time())
    mins = elapsed / 60
    rpm = stats["total"] / mins if mins > 0 else 0
    print(
        f"\n  Stats: {stats['total']} total ({rpm:.0f} req/min) | "
        f"checks={stats['access_checks']} reads={stats['reads']} errors={stats['errors']}"
    )


def run():
    stats["_start"] = time.time()
    iteration = 0

    print(f"Traffic generator running against {BASE_URL}")
    print(f"Average interval: {INTERVAL}s between bursts")
    print("Press Ctrl+C to stop.\n")

    while True:
        iteration += 1
        burst_size = random.choices(BURST_SIZES, weights=BURST_WEIGHTS)[0]
        actions = random.choices(ACTION_TYPES, weights=ACTION_WEIGHTS, k=burst_size)

        for action in actions:
            stats["total"] += 1
            if action == "access_check":
                line = do_access_check()
            else:
                line = do_read()
            if line:
                print(line)

        if iteration % STATS_EVERY_ITERATIONS == 0:
            print_stats()

        sleep_seconds = INTERVAL * (1 + random.uniform(-SLEEP_JITTER_MULTIPLIER, SLEEP_JITTER_MULTIPLIER))
        time.sleep(max(MINIMUM_SLEEP_SECONDS, sleep_seconds))


def main() -> int:
    global BASE_URL, INTERVAL

    parser = argparse.ArgumentParser(description="Generate live API traffic")
    parser.add_argument("--base-url", default=BASE_URL)
    parser.add_argument("--interval", type=float, default=INTERVAL)
    args = parser.parse_args()
    BASE_URL = args.base_url.rstrip("/")
    INTERVAL = args.interval

    try:
        run()
    except KeyboardInterrupt:
        print_stats()
        print("\nStopped.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
