"""
Seeds the public ClientManager API with demo catalog data.

Before running this script, start ClientManager.Api.

Creates:
    - Services, global rate limits, and client configurations from configuration.py

Usage:
    python seed_data.py [--base-url http://localhost:5062]
"""

from __future__ import annotations

import argparse
import json
import urllib.error
import urllib.parse
import urllib.request

from configuration import CONFIGURATION

GLOBAL_SETTINGS = CONFIGURATION["global"]
BASE_URL = GLOBAL_SETTINGS["api"]["base_url"]
API_PREFIX = GLOBAL_SETTINGS["api"]["prefix_with_leading_slash"]
SERVICES = GLOBAL_SETTINGS["catalogs"]["services"]
GLOBAL_RATE_LIMITS = GLOBAL_SETTINGS["catalogs"]["global_rate_limits"]
CLIENTS = GLOBAL_SETTINGS["catalogs"]["clients"]


def api_post(endpoint: str, payload: dict) -> tuple[int, str]:
    url = f"{BASE_URL.rstrip('/')}{endpoint}"
    body = json.dumps(payload).encode("utf-8")
    request = urllib.request.Request(
        url,
        data=body,
        method="POST",
        headers={"Content-Type": "application/json"},
    )
    try:
        with urllib.request.urlopen(request) as response:
            return response.status, response.read().decode("utf-8", errors="replace")
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode("utf-8", errors="replace")


def api_put(endpoint: str, payload: dict) -> tuple[int, str]:
    url = f"{BASE_URL.rstrip('/')}{endpoint}"
    request = urllib.request.Request(
        url,
        data=json.dumps(payload).encode("utf-8"),
        method="PUT",
        headers={"Content-Type": "application/json"},
    )
    try:
        with urllib.request.urlopen(request) as response:
            return response.status, response.read().decode("utf-8", errors="replace")
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode("utf-8", errors="replace")


def verify_public_api() -> None:
    url = f"{BASE_URL.rstrip('/')}{API_PREFIX}/statistics/overview"
    with urllib.request.urlopen(url, timeout=10) as response:
        if response.status != 200:
            raise RuntimeError(f"API health check failed: HTTP {response.status}")


def seed_list(label: str, endpoint: str, items: list[dict]) -> None:
    for item in items:
        status, response = api_post(endpoint, item)
        if status == 201:
            tag = "created"
        elif status == 409:
            item_id = urllib.parse.quote(item["id"], safe="")
            status, response = api_put(f"{endpoint}/{item_id}", item)
            if status != 200:
                raise RuntimeError(
                    f"Failed to update {label} {item['id']} via PUT {endpoint}/{item_id}: "
                    f"HTTP {status} {response}"
                )
            tag = "updated"
        else:
            raise RuntimeError(
                f"Failed to seed {label} {item['id']} via POST {endpoint}: HTTP {status} {response}"
            )
        print(f"  [{tag}] {label} {item['id']}")


def main() -> int:
    global BASE_URL

    parser = argparse.ArgumentParser(description="Seed the public ClientManager API with catalog data")
    parser.add_argument("--base-url", default=BASE_URL, help="Public API base URL")
    args = parser.parse_args()
    BASE_URL = args.base_url.rstrip("/")

    print(f"Seeding API at {BASE_URL}\n")
    verify_public_api()

    print("Services:")
    seed_list("Service", f"{API_PREFIX}/services", SERVICES)

    print("\nGlobal Rate Limits:")
    seed_list("GRL", f"{API_PREFIX}/global-rate-limits", GLOBAL_RATE_LIMITS)

    print("\nClients:")
    seed_list("Client", f"{API_PREFIX}/clients", CLIENTS)

    print("\nSeeding complete.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
