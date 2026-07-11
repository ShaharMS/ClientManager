"""ponytail: stress cross-pod granted totals under split vs single-writer traffic."""

from __future__ import annotations

import json
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timedelta, timezone

PORTS = [5062, 5063, 5064]
CLIENTS = ["platform-core", "mobile-app", "web-dashboard"]
SERVICES = ["auth-service", "billing-service", "notification-service"]


def access(port: int, client: str, service: str) -> None:
    params = urllib.parse.urlencode({"clientId": client, "serviceId": service})
    url = f"http://localhost:{port}/api/v1/access/check?{params}"
    try:
        with urllib.request.urlopen(url, timeout=10):
            pass
    except urllib.error.HTTPError:
        pass


def sum_granted(port: int) -> int:
    now = datetime.now(timezone.utc)
    body = {
        "searchCategory": "ServiceRequests",
        "fromUtc": (now - timedelta(minutes=30)).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "toUtc": now.strftime("%Y-%m-%dT%H:%M:%SZ"),
        "bucketCount": 12,
    }
    request = urllib.request.Request(
        f"http://localhost:{port}/api/v1/statistics/timeseries/search",
        data=json.dumps(body).encode(),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=120) as response:
        data = json.loads(response.read())

    total = 0
    for target in data.get("targets", []):
        for bucket in target.get("aggregateBuckets", []):
            total += int(bucket.get("grantedCount", 0))
    return total


def report(label: str) -> None:
    print(label)
    values = {port: sum_granted(port) for port in PORTS}
    for port, granted in values.items():
        print(f"  pod :{port} granted={granted}")
    if values:
        max_v = max(values.values())
        min_v = min(values.values())
        if max_v > 0:
            print(f"  min/max={min_v / max_v:.3f}")


def main() -> None:
    print("== heavy round-robin (180s) ==")
    deadline = time.monotonic() + 180
    sent = 0
    index = 0
    while time.monotonic() < deadline:
        access(PORTS[index % len(PORTS)], CLIENTS[index % len(CLIENTS)], SERVICES[index % len(SERVICES)])
        sent += 1
        index += 1
    print(f"sent {sent} checks")
    time.sleep(4)
    report("totals after split traffic")

    print("\n== single-writer to :5062 only (120s) ==")
    deadline = time.monotonic() + 120
    sent = 0
    while time.monotonic() < deadline:
        access(5062, CLIENTS[sent % len(CLIENTS)], SERVICES[sent % len(SERVICES)])
        sent += 1
    print(f"sent {sent} checks to pod1 only")
    time.sleep(4)
    report("totals after single-writer (all pods should match if overlay is cluster-wide)")


if __name__ == "__main__":
    main()
