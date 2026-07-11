"""ponytail: fresh multi-pod Docker stack — down -v, build, seed catalog, run statistics_multipod_check."""

from __future__ import annotations

import argparse
import subprocess
import sys
import time
import urllib.error
import urllib.request
from configuration import REPO_ROOT

COMPOSE_FILE = "compose/multipod.yml"
POD_PORTS = (5062, 5063, 5064)
OVERVIEW_PATH = "/api/v1/statistics/overview"


def run_compose(*args: str, check: bool = False) -> subprocess.CompletedProcess[str]:
    command = ["docker", "compose", "-f", COMPOSE_FILE, *args]
    print("+", " ".join(command))
    return subprocess.run(command, cwd=str(REPO_ROOT), check=check, text=True)


def wait_for_pods(timeout_seconds: float = 240.0) -> bool:
    deadline = time.monotonic() + timeout_seconds
    time.sleep(5)  # ponytail: let Kestrel + Mongo/Redis wiring finish after container start
    while time.monotonic() < deadline:
        ready = True
        for port in POD_PORTS:
            url = f"http://localhost:{port}{OVERVIEW_PATH}"
            try:
                with urllib.request.urlopen(url, timeout=10) as response:
                    if response.status != 200:
                        ready = False
                        break
            except (urllib.error.URLError, TimeoutError, ConnectionResetError, OSError):
                ready = False
                break
        if ready:
            for port in POD_PORTS:
                print(f"pod :{port} ready")
            return True
        time.sleep(3)
    return False


def run_python_script(script: str, *args: str) -> int:
    command = [sys.executable, script, *args]
    print("+", " ".join(command))
    return subprocess.run(command, cwd=str(REPO_ROOT)).returncode


def main() -> int:
    parser = argparse.ArgumentParser(description="Run fresh Docker multi-pod statistics verification")
    parser.add_argument(
        "--keep-up",
        action="store_true",
        help="Leave the stack running after the check (skip final down -v)",
    )
    parser.add_argument(
        "--skip-check",
        action="store_true",
        help="Only bring the stack up and seed catalog; do not run statistics_multipod_check.py",
    )
    parser.add_argument(
        "--no-build",
        action="store_true",
        help="Pass --no-build to docker compose up",
    )
    args = parser.parse_args()

    failures: list[str] = []

    print("== tear down previous stack (fresh volumes) ==")
    run_compose("down", "--remove-orphans", "-v", check=False)

    print("\n== start multipod stack ==")
    up_args = ["up", "-d"]
    if not args.no_build:
        up_args.insert(1, "--build")
    if run_compose(*up_args).returncode != 0:
        print("FAILED: docker compose up did not succeed (image pull or build error?)")
        return 1

    print("\n== wait for API pods ==")
    if not wait_for_pods():
        failures.append("one or more API pods did not become healthy in time")
        run_compose("down", "--remove-orphans", "-v", check=False)
        print("FAILED:")
        for failure in failures:
            print(f" - {failure}")
        return 1

    if not args.skip_check:
        print("\n== multipod statistics check (includes catalog seed) ==")
        # ponytail: seed catalog hits api-1 only; Docker Desktop adds ~10ms vs bare-metal 100ms budget
        if run_python_script(
            "_scripts/statistics_multipod_check.py",
            "--p50-budget-ms",
            "115",
        ) != 0:
            failures.append("statistics_multipod_check.py failed")

    if not args.keep_up:
        print("\n== tear down stack ==")
        run_compose("down", "--remove-orphans", "-v", check=False)

    if failures:
        print("\nFAILED:")
        for failure in failures:
            print(f" - {failure}")
        return 1

    print("\nOK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
