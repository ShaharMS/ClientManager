"""
Generates semi-random live traffic against the public ClientManager API.

Before running this script, start ClientManager.StorageApi first and then ClientManager.Api.

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

BASE_URL = "http://localhost:5062"
INTERVAL = 2.0  # average seconds between bursts
API_PREFIX = "/api/v1"
READ_SEARCH_QUERY = {"take": 100}

# ── Known IDs (must match seed_data.py) ───────────────────────────────────

ENABLED_CLIENTS = [
    "platform-core", "mobile-app", "web-dashboard", "partner-acme",
    "iot-gateway", "cicd-pipeline", "admin-tool",
    "data-warehouse", "crm-integration", "marketing-platform",
    "payment-gateway", "support-portal", "inventory-mgr", "logistics-api",
    "ml-training", "content-cdn", "partner-wayne", "partner-stark",
    "partner-umbrella", "hr-system", "compliance-bot", "chatbot-ai",
    "reporting-svc", "developer-sandbox",
]
DISABLED_CLIENTS = ["partner-globex"]
ALL_CLIENTS = ENABLED_CLIENTS + DISABLED_CLIENTS

SERVICES = [
    "auth-service", "billing-service", "notification-service", "analytics-service",
    "storage-service", "search-service", "email-service", "sms-service",
    "cache-service", "logging-service", "config-service", "scheduler-service",
    "geo-service", "media-service", "pdf-service", "audit-service",
    "webhook-service", "translate-service", "ml-service", "queue-service",
]
RESOURCE_POOLS = [
    "db-connections", "worker-threads", "file-upload-slots", "gpu-compute",
    "report-workers", "video-transcode", "api-gateway-slots", "sandbox-envs",
    "pdf-render-slots", "ml-inference-slots",
]

# Client -> services they have access to (for realistic traffic)
CLIENT_SERVICES = {
    "platform-core": ["auth-service", "billing-service", "notification-service", "analytics-service", "storage-service", "email-service", "cache-service", "logging-service", "config-service", "audit-service", "queue-service"],
    "mobile-app": ["auth-service", "billing-service", "notification-service", "analytics-service", "storage-service", "geo-service", "media-service"],
    "web-dashboard": ["auth-service", "analytics-service", "storage-service", "cache-service", "config-service"],
    "partner-acme": ["auth-service", "billing-service"],
    "iot-gateway": ["auth-service", "analytics-service", "logging-service"],
    "cicd-pipeline": ["auth-service", "storage-service", "analytics-service", "scheduler-service"],
    "admin-tool": ["auth-service", "billing-service", "notification-service", "analytics-service", "storage-service", "audit-service", "config-service"],
    "data-warehouse": ["auth-service", "analytics-service", "storage-service", "queue-service", "logging-service"],
    "crm-integration": ["auth-service", "billing-service", "notification-service", "email-service", "webhook-service"],
    "marketing-platform": ["auth-service", "email-service", "sms-service", "analytics-service", "translate-service"],
    "payment-gateway": ["auth-service", "billing-service", "notification-service", "audit-service", "webhook-service"],
    "support-portal": ["auth-service", "notification-service", "storage-service", "translate-service"],
    "inventory-mgr": ["auth-service", "cache-service", "analytics-service", "queue-service"],
    "logistics-api": ["auth-service", "geo-service", "notification-service", "webhook-service"],
    "ml-training": ["auth-service", "storage-service", "ml-service", "analytics-service", "logging-service"],
    "content-cdn": ["auth-service", "storage-service", "media-service", "cache-service"],
    "partner-wayne": ["auth-service", "billing-service", "analytics-service"],
    "partner-stark": ["auth-service", "billing-service", "storage-service", "ml-service"],
    "partner-umbrella": ["auth-service", "billing-service", "analytics-service", "audit-service"],
    "hr-system": ["auth-service", "notification-service", "email-service", "pdf-service"],
    "compliance-bot": ["auth-service", "audit-service", "logging-service", "config-service"],
    "chatbot-ai": ["auth-service", "ml-service", "translate-service", "cache-service", "logging-service"],
    "reporting-svc": ["auth-service", "analytics-service", "pdf-service", "email-service", "storage-service"],
    "developer-sandbox": ["auth-service", "storage-service", "cache-service"],
}

# Client -> pools they can use
CLIENT_POOLS = {
    "platform-core": ["db-connections", "worker-threads", "file-upload-slots", "api-gateway-slots"],
    "mobile-app": ["db-connections", "file-upload-slots", "api-gateway-slots"],
    "web-dashboard": ["db-connections"],
    "partner-acme": ["db-connections"],
    "iot-gateway": ["worker-threads"],
    "cicd-pipeline": ["db-connections", "worker-threads", "file-upload-slots", "sandbox-envs"],
    "admin-tool": ["db-connections"],
    "data-warehouse": ["db-connections", "worker-threads", "gpu-compute"],
    "crm-integration": ["db-connections", "api-gateway-slots"],
    "marketing-platform": ["report-workers", "pdf-render-slots"],
    "payment-gateway": ["db-connections", "api-gateway-slots"],
    "support-portal": ["db-connections", "file-upload-slots"],
    "inventory-mgr": ["db-connections", "worker-threads"],
    "logistics-api": ["api-gateway-slots"],
    "ml-training": ["gpu-compute", "worker-threads", "ml-inference-slots"],
    "content-cdn": ["api-gateway-slots", "video-transcode"],
    "partner-wayne": ["db-connections"],
    "partner-stark": ["db-connections", "gpu-compute"],
    "partner-umbrella": ["db-connections"],
    "hr-system": ["db-connections", "pdf-render-slots"],
    "compliance-bot": ["db-connections"],
    "chatbot-ai": ["ml-inference-slots", "api-gateway-slots"],
    "reporting-svc": ["report-workers", "pdf-render-slots", "db-connections"],
    "developer-sandbox": ["sandbox-envs", "db-connections"],
}

# Relative traffic weight per client (higher = more requests)
CLIENT_WEIGHT = {
    "platform-core": 5,
    "mobile-app": 4,
    "web-dashboard": 3,
    "content-cdn": 4,
    "payment-gateway": 3,
    "data-warehouse": 3,
    "chatbot-ai": 3,
    "marketing-platform": 2,
    "crm-integration": 2,
    "inventory-mgr": 2,
    "ml-training": 2,
    "logistics-api": 2,
    "support-portal": 2,
    "cicd-pipeline": 2,
    "iot-gateway": 2,
    "admin-tool": 1,
    "reporting-svc": 1,
    "hr-system": 1,
    "partner-acme": 1,
    "partner-wayne": 1,
    "partner-stark": 1,
    "partner-umbrella": 1,
    "compliance-bot": 1,
    "developer-sandbox": 1,
}

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
    if random.random() < 0.85:
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
        ("GET", f"{API_PREFIX}/statistics/client-summaries?pageSize=100", None),
        ("POST", f"{API_PREFIX}/statistics/clients/search", READ_SEARCH_QUERY),
        ("POST", f"{API_PREFIX}/statistics/services/search", READ_SEARCH_QUERY),
        ("POST", f"{API_PREFIX}/statistics/resource-pools/search", READ_SEARCH_QUERY),
        ("POST", f"{API_PREFIX}/clients/search", READ_SEARCH_QUERY),
        ("POST", f"{API_PREFIX}/services/search", READ_SEARCH_QUERY),
        ("POST", f"{API_PREFIX}/resource-pools/search", READ_SEARCH_QUERY),
        ("POST", f"{API_PREFIX}/global-rate-limits/search", READ_SEARCH_QUERY),
    ]

    # Also occasionally look up a specific client or their accessibility
    if random.random() < 0.4:
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
        burst_size = random.choices([1, 2, 3, 4, 5], weights=[20, 35, 25, 15, 5])[0]
        timestamp = datetime.now().strftime("%H:%M:%S")

        for _ in range(burst_size):
            # Weight the action types
            action = random.choices(
                ["access_check", "acquire", "release", "read"],
                weights=[50, 15, 10, 25]
            )[0]

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
        if iteration % 15 == 0:
            print_stats()

        # Jittered sleep
        sleep = max(0.2, random.gauss(INTERVAL, INTERVAL * 0.4))
        time.sleep(sleep)


def main():
    global BASE_URL, INTERVAL

    parser = argparse.ArgumentParser(description="Generate random traffic against the public ClientManager API")
    parser.add_argument("--base-url", default=BASE_URL, help="Public API base URL (start StorageApi first)")
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
