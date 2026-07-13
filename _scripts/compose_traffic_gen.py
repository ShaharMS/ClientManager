#!/usr/bin/env python3
"""Weighted multipod access-check load using the full seeded client/service catalog."""

from __future__ import annotations

import os
import random
import threading
import time
import urllib.parse
import urllib.request

from configuration import CONFIGURATION

GLOBAL_SETTINGS = CONFIGURATION["global"]
TRAFFIC_SETTINGS = CONFIGURATION["scripts"]["traffic_generator"]
API_PREFIX = GLOBAL_SETTINGS["api"]["prefix_with_leading_slash"]
VALID_ACCESS_COMBINATION_PROBABILITY = TRAFFIC_SETTINGS["probabilities"]["valid_access_combination"]
ENABLED_CLIENTS = GLOBAL_SETTINGS["catalogs"]["enabled_client_ids"]
ALL_CLIENTS = GLOBAL_SETTINGS["catalogs"]["all_client_ids"]
SERVICES = GLOBAL_SETTINGS["catalogs"]["service_ids"]
CLIENT_SERVICES = GLOBAL_SETTINGS["catalogs"]["client_services"]
CLIENT_WEIGHT = TRAFFIC_SETTINGS["client_weights"]


def parse_targets() -> list[str]:
    raw = os.environ.get("TRAFFIC_TARGETS", "http://api-1:5062")
    return [part.strip().rstrip("/") for part in raw.split(",") if part.strip()]


def parse_weights(targets: list[str]) -> list[int]:
    raw = os.environ.get("TRAFFIC_WEIGHTS", "").strip()
    if not raw:
        return [1] * len(targets)

    by_host: dict[str, int] = {}
    for piece in raw.split(","):
        name, _, weight = piece.partition(":")
        if not weight:
            continue
        by_host[name.strip()] = int(weight.strip())

    weights: list[int] = []
    for target in targets:
        host = urllib.parse.urlparse(target).hostname or target
        short = host.split(".")[0]
        weights.append(by_host.get(short, by_host.get(host, 1)))
    return weights


def pick_target(targets: list[str], weights: list[int]) -> str:
    return random.choices(targets, weights=weights, k=1)[0]


def pick_client_service() -> tuple[str, str]:
    if random.random() < VALID_ACCESS_COMBINATION_PROBABILITY:
        client = random.choices(ENABLED_CLIENTS, weights=[CLIENT_WEIGHT[c] for c in ENABLED_CLIENTS])[0]
        service = random.choice(CLIENT_SERVICES[client])
    else:
        client = random.choice(ALL_CLIENTS)
        service = random.choice(SERVICES)
    return client, service


def worker(targets: list[str], weights: list[int], interval: float, stop_at: float | None) -> None:
    while stop_at is None or time.time() < stop_at:
        base = pick_target(targets, weights)
        client, service = pick_client_service()
        url = f"{base}{API_PREFIX}/access/check?{urllib.parse.urlencode({'clientId': client, 'serviceId': service})}"
        try:
            with urllib.request.urlopen(url, timeout=5) as response:
                response.read()
        except Exception:
            pass
        time.sleep(interval)


def main() -> None:
    targets = parse_targets()
    weights = parse_weights(targets)
    rps = max(float(os.environ.get("TRAFFIC_RPS", "20")), 0.1)
    duration = int(os.environ.get("TRAFFIC_DURATION", "0"))
    stop_at = time.time() + duration if duration > 0 else None

    workers = max(1, min(64, int(rps)))
    interval = workers / rps

    print(
        f"traffic-gen: targets={targets} weights={weights} workers={workers} "
        f"interval={interval:.3f}s clients={len(ENABLED_CLIENTS)} services={len(SERVICES)}"
    )

    threads = [
        threading.Thread(target=worker, args=(targets, weights, interval, stop_at), daemon=True)
        for _ in range(workers)
    ]
    for thread in threads:
        thread.start()

    if stop_at is not None:
        while time.time() < stop_at:
            time.sleep(1)
    else:
        threading.Event().wait()


if __name__ == "__main__":
    main()
