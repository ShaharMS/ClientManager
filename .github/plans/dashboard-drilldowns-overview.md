# Plan: Dashboard Drilldowns — Stat Card Arrows, Monitor Page, Allocations Redesign

## Status: 🔲 Not started

## Overview

The Dribbble video reference for the AdminUI redesign showed arrows on the stat cards at the top of the dashboard — clickable affordances that drill down into the relevant screen. This plan implements those arrows and adds a brand-new **Monitor** page for real-time request analytics. The three stat card types work as follows:

- **Clients / Services / Resource Pools** cards: arrow navigates to the corresponding list page.
- **Requests / min** card: arrow opens a new **Monitor** page (`/monitor`) that shows per-client per-service request analytics — current request rates, rate limit hits, baseline/cap, in both a time-series graph and a breakdown table.
- **Pool Acquisition** card: arrow navigates to the existing **Active Allocations** page, which is enhanced with similar per-client per-pool breakdowns — a real-time utilization chart and a detailed table showing slot usage, denied attempts, and capacity.

The Monitor page and enhanced Allocations page depend on the historical usage data infrastructure from the `timed-statistics` plan (specifically the `GET /api/statistics/historical-usage` endpoint and the buffer-fed real-time data).

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 1 | [dashboard-drilldowns-1-card-arrows.md](dashboard-drilldowns-1-card-arrows.md) | Add clickable arrow affordances to all 5 dashboard stat cards |
| 2 | [dashboard-drilldowns-2-monitor-page.md](dashboard-drilldowns-2-monitor-page.md) | Create the Monitor page with per-client per-service request analytics |
| 3 | [dashboard-drilldowns-3-allocations-redesign.md](dashboard-drilldowns-3-allocations-redesign.md) | Enhance Active Allocations with per-client per-pool real-time breakdowns |

## Key Decisions

- **Arrow styling** — A small arrow icon (`arrow_forward` or `north_east`) in the top-right corner of each stat card, styled as a subtle circular button. On hover it highlights. Matches the Dribbble reference.
- **Monitor as a new nav item** — Added to sidebar Section 2 (Services group), after Rate Limits and before the divider. Uses `monitoring` icon. Also reachable via the Requests/min stat card arrow.
- **Monitor page structure** — Top: filter dropdowns (service, client). Middle: line chart showing requests/min over time with rate limit cap line, plus a stacked area for denied requests. Bottom: data table with columns for Client, Service, Current Req/min, Rate Limit Cap, Remaining, Denied (last 5 min), and a status badge.
- **Active Allocations redesign** — Keeps the existing per-pool utilization table but adds: a stacked bar chart of per-client slot usage per pool, and a new detail table showing client-level allocation counts, denied attempts, and slot limits. Auto-refreshes every 10 seconds (existing behavior preserved).
- **Data source** — Both pages consume the `GET /api/statistics/historical-usage` endpoint (from the timed-statistics plan) with `FiveMinute` granularity for recent data, plus the existing `usage-timeseries`, `client-usage-breakdown`, and `global-usage` endpoints for live aggregation.
- **No new API endpoints** — The timed-statistics plan's endpoints provide all needed data. The Monitor and Allocations pages compose views client-side from existing endpoints.
