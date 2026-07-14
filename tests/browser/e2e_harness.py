"""
Persistent browser E2E harness for ClientManager Admin UI.

Checks dashboard, core nav pages, service CRUD visibility, and English/Hebrew RTL.

Prerequisites:
    pip install -r tests/browser/requirements.txt
    python -m playwright install chromium
    ClientManager.Api on :5062 with seeded catalog
    ClientManager.AdminUI on :5100

Usage:
    python tests/browser/e2e_harness.py
    python tests/browser/e2e_harness.py --admin-url http://localhost:5100 --api-url http://localhost:5062
"""

from __future__ import annotations

import argparse
import json
import time
import urllib.error
import urllib.request
import uuid

from playwright.sync_api import TimeoutError as PlaywrightTimeoutError
from playwright.sync_api import sync_playwright


def api_delete(base_url: str, path: str) -> None:
    request = urllib.request.Request(f"{base_url.rstrip('/')}/{path.lstrip('/')}", method="DELETE")
    try:
        urllib.request.urlopen(request, timeout=10)
    except urllib.error.HTTPError:
        pass


def api_get_json(base_url: str, path: str) -> tuple[int, dict | None]:
    try:
        with urllib.request.urlopen(
            f"{base_url.rstrip('/')}/{path.lstrip('/')}",
            timeout=15,
        ) as response:
            payload = response.read()
            return response.status, json.loads(payload) if payload else None
    except urllib.error.HTTPError as error:
        return error.code, None


def wait_for_url(url: str, timeout_seconds: float = 120.0) -> None:
    deadline = time.monotonic() + timeout_seconds
    while time.monotonic() < deadline:
        try:
            with urllib.request.urlopen(url, timeout=5) as response:
                if response.status == 200:
                    return
        except (urllib.error.URLError, TimeoutError, ConnectionResetError, OSError):
            time.sleep(2)
    raise RuntimeError(f"Timed out waiting for {url}")


def assert_nav(page, href: str, heading_fragment: str, nav_label: str | None = None) -> None:
    label = nav_label or heading_fragment
    link = page.locator("a.nav-link", has_text=label)
    link.wait_for(state="visible")
    link.click()
    expected_path = f"/{href}" if href else "/"
    page.wait_for_url(lambda url: url.path == expected_path, timeout=20_000)
    page.wait_for_selector(f"h1:has-text('{heading_fragment}')", timeout=20_000)


def set_culture(page, admin_url: str, culture: str) -> None:
    is_rtl = culture == "he-IL"
    page.goto(admin_url)
    page.wait_for_load_state("networkidle")
    page.evaluate(
        """async (culture) => {
            localStorage.setItem('cm-preferences', JSON.stringify({ culture, theme: 'light' }));
            const mod = await import('/js/preferences.js');
            mod.setCultureCookie(culture);
        }""",
        culture,
    )
    page.reload(wait_until="networkidle")
    expected_dir = "rtl" if is_rtl else "ltr"
    page.wait_for_function(
        "(dir) => document.documentElement.getAttribute('dir') === dir",
        arg=expected_dir,
        timeout=5_000,
    )


def main() -> int:
    parser = argparse.ArgumentParser(description="Run Admin UI browser E2E checks")
    parser.add_argument("--admin-url", default="http://localhost:5100")
    parser.add_argument("--api-url", default="http://localhost:5062")
    parser.add_argument("--headed", action="store_false", dest="headless", default=True)
    args = parser.parse_args()

    admin_url = args.admin_url.rstrip("/")
    api_url = args.api_url.rstrip("/")
    service_id = f"e2e-svc-{uuid.uuid4().hex[:8]}"
    failures: list[str] = []

    print(f"Waiting for API {api_url} and Admin UI {admin_url}")
    wait_for_url(f"{api_url}/api/v2/statistics/overview")
    wait_for_url(admin_url)

    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=args.headless)
        page = browser.new_page()

        try:
            print("== English dashboard ==")
            page.goto(admin_url)
            page.wait_for_selector(".cm-sidebar")
            page.wait_for_selector(".cm-stat-card", timeout=20_000)
            page.wait_for_selector("h1:has-text('Welcome')", timeout=20_000)
            if page.locator("html").get_attribute("dir") != "ltr":
                failures.append("expected dir=ltr on English dashboard")

            print("== Core nav pages ==")
            assert_nav(page, "clients", "Clients")
            assert_nav(page, "services", "Services")
            assert_nav(page, "rate-limits", "Rate Limits")
            assert_nav(page, "", "Welcome", "Dashboard")

            print("== Service CRUD ==")
            page.goto(f"{admin_url}/services/new")
            page.wait_for_selector(".cm-editor", timeout=20_000)
            fields = page.locator(".cm-editor__field input")
            fields.nth(0).fill(service_id)
            fields.nth(0).press("Tab")
            fields.nth(1).fill("E2E Service")
            fields.nth(1).press("Tab")
            page.get_by_role("button", name="Save").click()
            page.wait_for_url("**/services", timeout=20_000)
            page.wait_for_selector(f"text={service_id}", timeout=20_000)

            page.goto(f"{admin_url}/services/{service_id}")
            page.wait_for_selector(".cm-editor", timeout=20_000)
            edit_fields = page.locator(".cm-editor__field input")
            edit_fields.nth(1).fill("E2E Service Updated")
            edit_fields.nth(1).press("Tab")
            page.get_by_role("button", name="Save").click()
            page.wait_for_url("**/services", timeout=20_000)
            page.wait_for_selector("text=E2E Service Updated", timeout=20_000)
            status, updated = api_get_json(api_url, f"api/v2/services/{service_id}")
            if status != 200 or updated is None or updated.get("name") != "E2E Service Updated":
                failures.append("UI service edit was not persisted through the API")

            search = page.locator(".cm-list-page__search-field input")
            search.fill(service_id)
            row = page.locator("tr", has_text=service_id)
            row.locator('button[title*="Delete"]').click()
            page.get_by_role("button", name="Delete").last.click()
            row.wait_for(state="detached", timeout=20_000)
            status, _ = api_get_json(api_url, f"api/v2/services/{service_id}")
            if status != 404:
                failures.append(f"UI service delete returned API status {status}")

            print("== Hebrew RTL ==")
            set_culture(page, admin_url, "he-IL")
            if page.locator("html").get_attribute("dir") != "rtl":
                failures.append("expected dir=rtl after Hebrew selection")
            if page.locator("html").get_attribute("lang") != "he-IL":
                failures.append("expected lang=he-IL after Hebrew selection")
            hebrew_heading = page.locator("h1").inner_text()
            if hebrew_heading == "Welcome" or not any(ord(char) > 127 for char in hebrew_heading):
                failures.append("Hebrew culture did not localize the dashboard heading")

            print("== English restore ==")
            set_culture(page, admin_url, "en")
            if page.locator("html").get_attribute("dir") != "ltr":
                failures.append("expected dir=ltr after switching back to English")
            page.wait_for_selector("h1:has-text('Welcome')", timeout=20_000)

            page.goto(f"{admin_url}/settings")
            page.wait_for_selector(".cm-settings .rz-dropdown", timeout=10_000)
        except (PlaywrightTimeoutError, AssertionError, RuntimeError) as error:
            failures.append(str(error))
        finally:
            browser.close()

    api_delete(api_url, f"api/v2/services/{service_id}")

    if failures:
        print("\nFAILED:")
        for failure in failures:
            print(f" - {failure}")
        return 1

    print("\nOK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
