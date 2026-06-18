#!/usr/bin/env python3
"""Compare statistics API RPM against generated traffic across a load-balanced endpoint."""

from __future__ import annotations

import argparse
import asyncio
import time
from dataclasses import dataclass

import aiohttp

from configuration import SHARED_API_SETTINGS


def default_base_url() -> str:
    return SHARED_API_SETTINGS["base_url"]


@dataclass
class SmokeResult:
    requests_sent: int
    elapsed_seconds: float
    reported_rpm: float

    @property
    def actual_rpm(self) -> float:
        minutes = self.elapsed_seconds / 60
        return self.requests_sent / minutes if minutes > 0 else 0


async def send_access_checks(
    session: aiohttp.ClientSession,
    base_url: str,
    client_id: str,
    service_id: str,
    count: int,
    concurrency: int,
) -> int:
    semaphore = asyncio.Semaphore(concurrency)
    completed = 0

    async def one_request() -> None:
        nonlocal completed
        async with semaphore:
            url = f"{base_url}/api/v1/access/{client_id}/services/{service_id}/check"
            async with session.get(url) as response:
                await response.read()
                completed += 1

    await asyncio.gather(*(one_request() for _ in range(count)))
    return completed


async def read_reported_rpm(session: aiohttp.ClientSession, base_url: str) -> float:
    url = f"{base_url}/api/v1/statistics/global-usage"
    async with session.get(url) as response:
        response.raise_for_status()
        payload = await response.json()
        return float(payload.get("requestsPerMinute", 0))


async def run_smoke(base_url: str, client_id: str, service_id: str, count: int, concurrency: int) -> SmokeResult:
    timeout = aiohttp.ClientTimeout(total=120)
    async with aiohttp.ClientSession(timeout=timeout) as session:
        start = time.perf_counter()
        sent = await send_access_checks(session, base_url, client_id, service_id, count, concurrency)
        await asyncio.sleep(2)
        reported = await read_reported_rpm(session, base_url)
        elapsed = time.perf_counter() - start
        return SmokeResult(requests_sent=sent, elapsed_seconds=elapsed, reported_rpm=reported)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-url", default=default_base_url())
    parser.add_argument("--client-id", default="demo-client")
    parser.add_argument("--service-id", default="demo-service")
    parser.add_argument("--count", type=int, default=120)
    parser.add_argument("--concurrency", type=int, default=20)
    parser.add_argument("--tolerance", type=float, default=0.35, help="Allowed relative RPM error")
    args = parser.parse_args()

    result = asyncio.run(
        run_smoke(args.base_url, args.client_id, args.service_id, args.count, args.concurrency)
    )

    actual = result.actual_rpm
    reported = result.reported_rpm
    error = abs(reported - actual) / actual if actual > 0 else 0

    print(f"Requests sent: {result.requests_sent}")
    print(f"Elapsed: {result.elapsed_seconds:.1f}s")
    print(f"Actual RPM: {actual:.1f}")
    print(f"Reported RPM: {reported:.1f}")
    print(f"Relative error: {error * 100:.1f}%")

    if error > args.tolerance:
        raise SystemExit(
            f"RPM mismatch exceeds tolerance ({args.tolerance * 100:.0f}%): "
            f"actual={actual:.1f}, reported={reported:.1f}"
        )


if __name__ == "__main__":
    main()
