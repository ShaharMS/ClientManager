# Plan: Split Global Rate Limits into Rate Limits & Quotas

## Status: 🔲 Not started

## Overview

The current AdminUI has a single "Global Rate Limits" page that manages rate limits for both Services and Resource Pools in one table. This plan splits it into two domain-specific pages:

- **Rate Limits** (`/rate-limits`) — placed below "Services" in the sidebar, scoped to `TargetType = Service`, with service-specific descriptions and controls.
- **Quotas** (`/quotas`) — placed below "Resource Pools" in the sidebar, scoped to `TargetType = ResourcePool`, with resource-pool-specific descriptions and controls.

The existing `GlobalRateLimitsController` API and `GlobalRateLimitApiService` already support `?targetType=` filtering, so no backend changes are needed. This is purely an AdminUI restructuring.

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 1 | [ratelimit-split-1-pages.md](ratelimit-split-1-pages.md) | Create Rate Limits and Quotas list + editor pages, update nav, remove old pages |

**Continued by**: [dashboard-drilldowns-overview.md](dashboard-drilldowns-overview.md) — Dashboard stat card arrows, Monitor page, and Active Allocations redesign

## Key Decisions

- **No API changes required** — The existing `GlobalRateLimitsController` already supports `?targetType=Service` and `?targetType=ResourcePool` filtering, and the `GlobalRateLimitApiService` already has `GetByTargetTypeAsync`. The editor pages pre-fill and lock the `TargetType` field.
- **Remove the old unified page** — The combined `GlobalRateLimits/` folder and its routes (`/global-rate-limits`) are deleted. No redirect is needed since this is an internal admin tool.
- **Route paths** — `/rate-limits` for services, `/quotas` for resource pools. Short and distinct from the existing `/services` and `/resource-pools` routes.
- **Editor pages set TargetType automatically** — The Rate Limits editor hardcodes `TargetType = Service`; the Quotas editor hardcodes `TargetType = ResourcePool`. The dropdown is removed from both — no ambiguity.
- **Sidebar ordering** — Three sections separated by soft dividers: (Dashboard, Clients) | (Services, Rate Limits) | (Resource Pools, Quotas, Active Allocations). Each rate limiting concept is grouped with its parent domain. Dividers use `opacity: 0.5` on the border color for a subtle, non-heavy visual separation.
