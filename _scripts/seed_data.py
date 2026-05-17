"""
Seeds the public ClientManager API with realistic mock data.

Before running this script, start ClientManager.StorageApi first and then ClientManager.Api.

Creates:
    - 20 services
    - 10 resource pools
    - 25 client configurations with varied access patterns
    - Global rate limits for each service and resource pool
    - Historical usage snapshots for dashboards and statistics queries

Usage:
    python seed_data.py [--base-url http://localhost:5062]
"""

import argparse
import json
import math
import os
import random
from collections import defaultdict
from datetime import datetime, timedelta, timezone
from pathlib import Path
import urllib.error
import urllib.request

from __configuration import CONFIGURATION, default_history_data_dir as resolve_default_history_data_dir

GLOBAL_SETTINGS = CONFIGURATION["global"]
SEED_SETTINGS = CONFIGURATION["scripts"]["seed_data"]
SEED_HISTORY_SETTINGS = SEED_SETTINGS["history"]
SEED_HISTORY_DEFAULTS = SEED_HISTORY_SETTINGS["defaults"]
SEED_BUSINESS_CYCLE = SEED_HISTORY_SETTINGS["business_cycle"]
SEED_RANDOM_MULTIPLIER = SEED_HISTORY_SETTINGS["random_multiplier"]
SEED_SERVICE_BUCKET_SETTINGS = SEED_HISTORY_SETTINGS["service_bucket"]
SEED_POOL_BUCKET_SETTINGS = SEED_HISTORY_SETTINGS["pool_bucket"]
ALWAYS_ON_CLIENT_IDS = set(SEED_BUSINESS_CYCLE["always_on_clients"]["ids"])
BATCH_CLIENT_IDS = set(SEED_BUSINESS_CYCLE["batch_clients"]["ids"])

BASE_URL = GLOBAL_SETTINGS["api"]["base_url"]
API_PREFIX = GLOBAL_SETTINGS["api"]["prefix_with_leading_slash"]
DEFAULT_HISTORY_DAYS = SEED_SETTINGS["defaults"]["history_days"]
DEFAULT_HISTORY_SEED = SEED_SETTINGS["defaults"]["history_seed"]
USAGE_SNAPSHOTS_COLLECTION = GLOBAL_SETTINGS["data"]["usage_snapshots_collection"]
SERVICES = GLOBAL_SETTINGS["catalogs"]["services"]
RESOURCE_POOLS = GLOBAL_SETTINGS["catalogs"]["resource_pools"]
GLOBAL_RATE_LIMITS = GLOBAL_SETTINGS["catalogs"]["global_rate_limits"]
CLIENTS = GLOBAL_SETTINGS["catalogs"]["clients"]
CLIENT_BASE_LOAD = GLOBAL_SETTINGS["catalogs"]["client_base_load"]
TARGET_LOAD_MULTIPLIER = GLOBAL_SETTINGS["catalogs"]["target_load_multiplier"]
HISTORY_BUCKET_ROUNDING_MINUTES = SEED_HISTORY_SETTINGS["bucket_rounding_minutes"]
HISTORY_GENERATION_END_OFFSET_MINUTES = SEED_HISTORY_SETTINGS["generation_end_offset_minutes"]
DEFAULT_SERVICE_CAP_PER_MINUTE = SEED_HISTORY_DEFAULTS["service_cap_per_minute"]
DEFAULT_POOL_SLOTS = SEED_HISTORY_DEFAULTS["pool_slots"]
DEFAULT_CLIENT_BASE_LOAD = SEED_HISTORY_DEFAULTS["client_base_load"]
DEFAULT_TARGET_LOAD_MULTIPLIER = SEED_HISTORY_DEFAULTS["target_load_multiplier"]
DEFAULT_POOL_GLOBAL_CAP_MULTIPLIER = SEED_HISTORY_DEFAULTS["pool_global_cap_multiplier"]
DEFAULT_POOL_MINUTE_CAP_MULTIPLIER = SEED_HISTORY_DEFAULTS["pool_minute_cap_multiplier"]


def load_json_collection(data_dir: Path, collection: str):
    path = data_dir / f"{collection}.json"
    if not path.exists():
        return {}

    with path.open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def save_json_collection(data_dir: Path, collection: str, documents):
    data_dir.mkdir(parents=True, exist_ok=True)
    path = data_dir / f"{collection}.json"
    temp_path = path.with_suffix(".json.tmp")

    with temp_path.open("w", encoding="utf-8") as handle:
        json.dump(documents, handle, indent=2)
        handle.write("\n")

    os.replace(temp_path, path)


def utc_now_rounded_to_five_minutes():
    now = datetime.now(timezone.utc).replace(second=0, microsecond=0)
    return now - timedelta(minutes=now.minute % HISTORY_BUCKET_ROUNDING_MINUTES)


def iso_z(value: datetime):
    return value.astimezone(timezone.utc).replace(tzinfo=None).isoformat(timespec="seconds") + "Z"


def segment_start(timestamp: datetime, granularity: str):
    timestamp = timestamp.astimezone(timezone.utc)
    if granularity == "Second":
        return timestamp.replace(minute=0, second=0, microsecond=0)
    if granularity == "FiveMinute":
        return timestamp.replace(hour=0, minute=0, second=0, microsecond=0)
    if granularity == "Hour":
        monday = timestamp.date() - timedelta(days=timestamp.weekday())
        return datetime(monday.year, monday.month, monday.day, tzinfo=timezone.utc)
    if granularity == "Day":
        return datetime(timestamp.year, timestamp.month, 1, tzinfo=timezone.utc)
    raise ValueError(f"Unsupported granularity: {granularity}")


def history_windows(history_days: int, end: datetime):
    days = max(1, history_days)
    windows = []
    for window in SEED_HISTORY_SETTINGS["windows"]:
        window_days = days if window["max_days"] is None else min(days, window["max_days"])
        windows.append((window["granularity"], timedelta(**window["step"]), window_days))

    for granularity, step, window_days in windows:
        start = end - timedelta(days=window_days)
        if granularity == "FiveMinute":
            start = start.replace(minute=start.minute - (start.minute % HISTORY_BUCKET_ROUNDING_MINUTES), second=0, microsecond=0)
        elif granularity == "Hour":
            start = start.replace(minute=0, second=0, microsecond=0)
        else:
            start = start.replace(hour=0, minute=0, second=0, microsecond=0)

        current = start
        while current < end:
            yield granularity, step, current
            current += step


def max_requests_per_minute_by_target():
    return {limit["targetId"]: limit["maxRequests"] for limit in GLOBAL_RATE_LIMITS}


def service_cap_per_minute(client, service_id, global_caps):
    caps = []

    if not client.get("exemptFromGlobalLimits"):
        caps.append(global_caps.get(service_id, DEFAULT_SERVICE_CAP_PER_MINUTE))

    client_global = client.get("globalRateLimit")
    if client_global:
        caps.append(client_global["maxRequests"])

    service_config = client.get("services", {}).get(service_id, {})
    service_limit = service_config.get("rateLimit")
    if service_limit:
        caps.append(service_limit["maxRequests"])

    return max(1, min(caps) if caps else global_caps.get(service_id, DEFAULT_SERVICE_CAP_PER_MINUTE))


def resource_pool_cap(client, pool_id):
    pool = next((item for item in RESOURCE_POOLS if item["id"] == pool_id), None)
    pool_slots = pool["maxSlots"] if pool else DEFAULT_POOL_SLOTS
    client_slots = client.get("resourcePools", {}).get(pool_id, {}).get("maxSlots", pool_slots)
    return max(1, min(pool_slots, client_slots))


def client_target_pairs():
    for client in CLIENTS:
        if not client.get("isEnabled", True):
            continue

        for service_id, config in client.get("services", {}).items():
            if config.get("isAllowed", True):
                yield client, "Service", service_id

        for pool_id in client.get("resourcePools", {}):
            yield client, "ResourcePool", pool_id


def business_cycle_factor(timestamp: datetime, client_id: str, target_type: str):
    hour = timestamp.hour + timestamp.minute / 60
    weekday = timestamp.weekday()
    weekend_factor = SEED_BUSINESS_CYCLE["weekend_factor"] if weekday >= 5 else 1.0

    morning_peak = math.exp(-((hour - SEED_BUSINESS_CYCLE["morning_peak_hour"]) ** 2) / SEED_BUSINESS_CYCLE["morning_peak_divisor"])
    afternoon_peak = SEED_BUSINESS_CYCLE["afternoon_peak_multiplier"] * math.exp(
        -((hour - SEED_BUSINESS_CYCLE["afternoon_peak_hour"]) ** 2) / SEED_BUSINESS_CYCLE["afternoon_peak_divisor"]
    )
    night_trickle = SEED_BUSINESS_CYCLE["night_trickle_base"] + SEED_BUSINESS_CYCLE["night_trickle_amplitude"] * math.cos(
        (hour / 24) * 2 * math.pi
    )
    human_factor = night_trickle + morning_peak + afternoon_peak

    if client_id in ALWAYS_ON_CLIENT_IDS:
        always_on = SEED_BUSINESS_CYCLE["always_on_clients"]
        human_factor = always_on["base"] + always_on["amplitude"] * math.sin(
            ((hour - always_on["hour_offset"]) / 24) * 2 * math.pi
        )
        weekend_factor = always_on["weekend_factor"]
    elif client_id in BATCH_CLIENT_IDS:
        batch = SEED_BUSINESS_CYCLE["batch_clients"]
        nightly_batch = batch["nightly_batch_multiplier"] * math.exp(
            -((hour - batch["nightly_batch_hour"]) ** 2) / batch["nightly_batch_divisor"]
        )
        human_factor = batch["base"] + nightly_batch + batch["morning_peak_multiplier"] * morning_peak
        weekend_factor = batch["weekend_factor"]
    elif client_id.startswith("partner-"):
        human_factor *= SEED_BUSINESS_CYCLE["partner_multiplier"]
        weekend_factor *= SEED_BUSINESS_CYCLE["partner_weekend_multiplier"]

    if target_type == "ResourcePool":
        human_factor *= SEED_BUSINESS_CYCLE["resource_pool_multiplier"]

    day_of_year = timestamp.timetuple().tm_yday
    seasonal = 1.0 + SEED_BUSINESS_CYCLE["seasonal_amplitude"] * math.sin(
        ((day_of_year - SEED_BUSINESS_CYCLE["seasonal_day_offset"]) / SEED_BUSINESS_CYCLE["seasonal_period_days"])
        * 2
        * math.pi
    )
    return max(SEED_BUSINESS_CYCLE["minimum_factor"], human_factor * weekend_factor * seasonal)


def random_multiplier(rng: random.Random):
    return max(
        SEED_RANDOM_MULTIPLIER["minimum"],
        min(
            SEED_RANDOM_MULTIPLIER["maximum"],
            rng.lognormvariate(SEED_RANDOM_MULTIPLIER["lognormal_mean"], SEED_RANDOM_MULTIPLIER["lognormal_sigma"]),
        ),
    )


def generated_service_bucket(rng, client, target_id, timestamp, step, global_caps):
    cap_per_minute = service_cap_per_minute(client, target_id, global_caps)
    minutes = max(1, int(step.total_seconds() // 60))
    bucket_cap = cap_per_minute * minutes
    base_load = CLIENT_BASE_LOAD.get(client["id"], DEFAULT_CLIENT_BASE_LOAD)
    target_load = TARGET_LOAD_MULTIPLIER.get(target_id, DEFAULT_TARGET_LOAD_MULTIPLIER)
    cycle = business_cycle_factor(timestamp, client["id"], "Service")
    utilization = min(
        SEED_SERVICE_BUCKET_SETTINGS["maximum_utilization"],
        base_load * target_load * cycle * random_multiplier(rng),
    )
    demand = int(bucket_cap * utilization)

    if rng.random() < SEED_SERVICE_BUCKET_SETTINGS["spike_probability"]:
        demand = int(
            bucket_cap
            * rng.uniform(
                SEED_SERVICE_BUCKET_SETTINGS["spike_multiplier_minimum"],
                SEED_SERVICE_BUCKET_SETTINGS["spike_multiplier_maximum"],
            )
        )

    granted = min(bucket_cap, max(0, demand))
    denied = max(0, demand - bucket_cap)

    if granted > 0 and rng.random() < SEED_SERVICE_BUCKET_SETTINGS["denial_probability"]:
        denied += rng.randint(1, max(1, int(granted * SEED_SERVICE_BUCKET_SETTINGS["denial_ratio"])))

    return {
        "Timestamp": iso_z(timestamp),
        "GrantedCount": granted,
        "DeniedCount": denied,
        "ReleasedCount": 0,
        "ActiveCount": 0,
    }


def generated_pool_bucket(rng, client, target_id, timestamp, step, global_caps):
    slots = resource_pool_cap(client, target_id)
    minute_cap = min(
        global_caps.get(target_id, slots * DEFAULT_POOL_GLOBAL_CAP_MULTIPLIER),
        slots * DEFAULT_POOL_MINUTE_CAP_MULTIPLIER,
    )
    minutes = max(1, int(step.total_seconds() // 60))
    bucket_cap = max(slots, minute_cap * minutes)
    base_load = CLIENT_BASE_LOAD.get(client["id"], DEFAULT_CLIENT_BASE_LOAD)
    cycle = business_cycle_factor(timestamp, client["id"], "ResourcePool")
    utilization = min(SEED_POOL_BUCKET_SETTINGS["maximum_utilization"], base_load * cycle * random_multiplier(rng))

    active = min(
        slots,
        max(
            0,
            int(
                round(
                    slots
                    * utilization
                    * rng.uniform(
                        SEED_POOL_BUCKET_SETTINGS["active_multiplier_minimum"],
                        SEED_POOL_BUCKET_SETTINGS["active_multiplier_maximum"],
                    )
                )
            ),
        ),
    )
    demand = int(
        bucket_cap
        * utilization
        * rng.uniform(
            SEED_POOL_BUCKET_SETTINGS["demand_ratio_minimum"],
            SEED_POOL_BUCKET_SETTINGS["demand_ratio_maximum"],
        )
    )
    granted = min(bucket_cap, max(0, demand))
    denied = max(0, demand - bucket_cap)

    if active >= slots and rng.random() < SEED_POOL_BUCKET_SETTINGS["full_pool_denial_probability"]:
        denied += rng.randint(1, max(1, slots // SEED_POOL_BUCKET_SETTINGS["full_pool_denial_divisor"]))

    released = min(
        granted + active,
        max(
            0,
            int(
                granted
                * rng.uniform(
                    SEED_POOL_BUCKET_SETTINGS["release_multiplier_minimum"],
                    SEED_POOL_BUCKET_SETTINGS["release_multiplier_maximum"],
                )
            ),
        ),
    )

    return {
        "Timestamp": iso_z(timestamp),
        "GrantedCount": granted,
        "DeniedCount": denied,
        "ReleasedCount": released,
        "ActiveCount": active,
    }


def build_usage_snapshots(history_days: int, seed: int):
    rng = random.Random(seed)
    end = utc_now_rounded_to_five_minutes() - timedelta(minutes=HISTORY_GENERATION_END_OFFSET_MINUTES)
    global_caps = max_requests_per_minute_by_target()
    snapshots = defaultdict(list)

    pairs = list(client_target_pairs())
    for granularity, step, timestamp in history_windows(history_days, end):
        for client, target_type, target_id in pairs:
            if target_type == "Service":
                bucket = generated_service_bucket(rng, client, target_id, timestamp, step, global_caps)
            else:
                bucket = generated_pool_bucket(rng, client, target_id, timestamp, step, global_caps)

            if bucket["GrantedCount"] == 0 and bucket["DeniedCount"] == 0 and bucket["ActiveCount"] == 0:
                continue

            segment = segment_start(timestamp, granularity)
            snapshot_id = f"{client['id']}:{target_type}:{target_id}:{granularity}:{segment:%Y%m%d%H}"
            snapshots[(snapshot_id, client["id"], target_type, target_id, granularity, segment)].append(bucket)

    return {
        snapshot_id: {
            "Id": snapshot_id,
            "ClientId": client_id,
            "TargetId": target_id,
            "TargetType": target_type,
            "Granularity": granularity,
            "SegmentStart": iso_z(segment),
            "Buckets": sorted(buckets, key=lambda bucket: bucket["Timestamp"]),
        }
        for (snapshot_id, client_id, target_type, target_id, granularity, segment), buckets in snapshots.items()
    }


def merge_usage_snapshots(existing, generated, replace_history: bool):
    if replace_history:
        existing = {
            key: value
            for key, value in existing.items()
            if value.get("Granularity") not in {"FiveMinute", "Hour", "Day"}
        }

    for snapshot_id, snapshot in generated.items():
        if snapshot_id not in existing:
            existing[snapshot_id] = snapshot
            continue

        current = existing[snapshot_id]
        buckets = {bucket["Timestamp"]: bucket for bucket in current.get("Buckets", [])}
        buckets.update({bucket["Timestamp"]: bucket for bucket in snapshot["Buckets"]})
        current.update(snapshot)
        current["Buckets"] = [buckets[key] for key in sorted(buckets)]

    return existing


def seed_historical_usage(data_dir: Path, history_days: int, seed: int, replace_history: bool):
    print(f"\nHistorical Usage ({history_days} days):")
    existing = load_json_collection(data_dir, USAGE_SNAPSHOTS_COLLECTION)
    generated = build_usage_snapshots(history_days, seed)
    merged = merge_usage_snapshots(existing, generated, replace_history)
    save_json_collection(data_dir, USAGE_SNAPSHOTS_COLLECTION, merged)

    bucket_count = sum(len(snapshot.get("Buckets", [])) for snapshot in generated.values())
    action = "replaced" if replace_history else "merged"
    print(f"  [{action}] {len(generated):,} usage snapshot documents")
    print(f"  [wrote] {bucket_count:,} historical buckets to {data_dir / (USAGE_SNAPSHOTS_COLLECTION + '.json')}")
    print("  [note] Restart StorageApi after file-backed historical seeding so in-memory caches/providers reload it.")


def api(method: str, path: str, body=None):
    """Make an API call. Returns (status_code, response_body_or_None)."""
    url = f"{BASE_URL.rstrip('/')}/{path.lstrip('/')}"
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req) as resp:
            payload = resp.read()
            if not payload:
                return resp.status, None

            try:
                return resp.status, json.loads(payload)
            except json.JSONDecodeError:
                return resp.status, payload.decode(errors="ignore")
    except urllib.error.HTTPError as error:
        payload = error.read()
        if not payload:
            return error.code, None

        try:
            return error.code, json.loads(payload)
        except json.JSONDecodeError:
            return error.code, payload.decode(errors="ignore")


def verify_public_api():
    status, response = api("GET", f"{API_PREFIX}/statistics/overview")
    if status != 200:
        raise RuntimeError(
            f"Expected the public ClientManager API at {BASE_URL}, but "
            f"GET {API_PREFIX}/statistics/overview returned HTTP {status}: {response}. "
            "Start ClientManager.StorageApi first, then ClientManager.Api."
        )


def seed_list(label: str, endpoint: str, items: list):
    for item in items:
        existing_status, existing_response = api("GET", f"{endpoint}/{item['id']}")
        if existing_status == 200:
            print(f"  [exists] {label} {item['id']}")
            continue

        if existing_status != 404:
            raise RuntimeError(
                f"Failed to check {label} {item['id']} via GET {endpoint}/{item['id']}: "
                f"HTTP {existing_status} {existing_response}"
            )

        status, response = api("POST", endpoint, item)
        if status == 201:
            tag = "created"
        elif status == 409:
            tag = "exists"
        else:
            raise RuntimeError(
                f"Failed to seed {label} {item['id']} via POST {endpoint}: "
                f"HTTP {status} {response}"
            )

        print(f"  [{tag}] {label} {item['id']}")


def main():
    global BASE_URL

    parser = argparse.ArgumentParser(description="Seed the public ClientManager API with mock data")
    parser.add_argument("--base-url", default=BASE_URL, help="Public API base URL (start StorageApi first)")
    parser.add_argument(
        "--skip-history",
        action="store_true",
        help="Only seed catalog data through the API; do not write historical usage snapshots",
    )
    parser.add_argument(
        "--history-days",
        type=int,
        default=DEFAULT_HISTORY_DAYS,
        help=f"Number of days of historical daily usage to generate (default: {DEFAULT_HISTORY_DAYS})",
    )
    parser.add_argument(
        "--history-seed",
        type=int,
        default=DEFAULT_HISTORY_SEED,
        help="Random seed for deterministic historical usage generation",
    )
    parser.add_argument(
        "--history-data-dir",
        type=Path,
        default=resolve_default_history_data_dir(),
        help="Directory containing UsageSnapshots.json for the JSON file storage provider",
    )
    parser.add_argument(
        "--replace-history",
        action="store_true",
        help="Replace existing FiveMinute/Hour/Day usage snapshots instead of merging generated buckets",
    )
    args = parser.parse_args()

    if args.history_days < 1:
        parser.error("--history-days must be at least 1")

    BASE_URL = args.base_url.rstrip("/")

    print(f"Seeding API at {BASE_URL}\n")
    verify_public_api()

    print("Services:")
    seed_list("Service", f"{API_PREFIX}/services", SERVICES)

    print("\nResource Pools:")
    seed_list("Pool", f"{API_PREFIX}/resource-pools", RESOURCE_POOLS)

    print("\nGlobal Rate Limits:")
    seed_list("GRL", f"{API_PREFIX}/global-rate-limits", GLOBAL_RATE_LIMITS)

    print("\nClients:")
    seed_list("Client", f"{API_PREFIX}/clients", CLIENTS)

    if not args.skip_history:
        seed_historical_usage(
            args.history_data_dir,
            args.history_days,
            args.history_seed,
            args.replace_history,
        )

    print("\n✓ Seeding complete.")


if __name__ == "__main__":
    main()