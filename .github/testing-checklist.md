# UI Testing Checklist — Lucene Backend (RAMDirectory)

> **Tested on:** 2026-03-26
> **Backend:** Lucene.NET with RAMDirectory (in-memory)
> **Traffic:** Continuous via `traffic_generator.py --interval 2.0`

## Dashboard (`/`)
- [x] Page loads without errors
- [x] Summary cards show correct counts (25 Clients, 20 Services, 10 Resource Pools)
- [x] Requests/min counter is updating with traffic
- [x] Usage Over Time chart renders
- [x] Usage Per Client chart renders
- [x] Client Overview table loads with data
- [x] Client Overview search works (typed "globex" → filtered to partner-globex)

## Clients — List (`/clients`)
- [x] Page loads, table shows all 25 clients
- [x] Text search filters clients by ID/name (typed "acme" → only partner-acme)
- [x] Enabled filter dropdown shows only enabled clients
- [x] Disabled filter dropdown shows only disabled clients (partner-globex only)
- [x] Clear filter returns to all clients
- [x] Pagination works (page 2 shows remaining clients)
- [ ] Sorting by columns works *(not explicitly tested)*

## Clients — Create (`/clients/new`)
- [x] Create form loads with Client ID, Name, Enabled toggle
- [x] Can fill in fields and create a new client ("test-client-ui" / "UI Test Client")
- [x] Redirects back to list after creation

## Clients — Edit (`/clients/{id}`)
- [x] Edit form loads with existing data (Client ID disabled)
- [x] Can modify fields and save (name → "UI Test Client - Modified")
- [x] Changes persist (visible in list)

## Clients — Delete
- [x] Delete button removes client from list (confirmed "No records to display")

## Services — List (`/services`)
- [x] Page loads, table shows all 20 services
- [x] Text search filters services ("billing" → billing-service only)
- [x] Enabled/Disabled filter works (Disabled → search-service only)
- [x] Pagination works

## Services — Create (`/services/new`)
- [x] Create form loads with Service ID, Name, Enabled toggle
- [x] Can create a new service ("test-delete-svc" / "Test Delete Service")

## Services — Edit (`/services/{id}`)
- [x] Edit form loads with existing data (Service ID disabled)
- [x] Can modify and save (name → "Test Service Modified")

## Services — Delete
- [x] Delete button removes service (confirmed "No records to display" after search)

## Resource Pools — List (`/resource-pools`)
- [x] Page loads, table shows all 10 pools (ID, Name, Max Slots, Allocation TTL)
- [x] Text search filters pools ("video" → video-transcode only)

## Resource Pools — Create (`/resource-pools/new`)
- [x] Create form loads (Pool ID, Name, Max Slots, Allocation TTL in seconds)
- [x] Can create a new pool ("test-pool" / "Test Pool" / 25 slots / 600s TTL)

## Resource Pools — Edit (`/resource-pools/{id}`)
- [x] Edit form loads with existing data (Pool ID disabled, Name/Max Slots/TTL editable)
- [x] Can modify and save (name → "Test Pool Modified", slots → 50)

## Resource Pools — Delete
- [x] Delete button removes pool

## Rate Limits — List (`/rate-limits`)
- [x] Page loads, table shows rate limits (ID, Service, Strategy, Max Requests, Window)
- [x] Text search filters rate limits ("billing" → grl-billing only)
- [x] TargetType filter (Service) shows only service-type GRLs — WORKS (dropdown opens, "Service" option visible)
- [x] TargetType filter (Resource Pool) shows only pool-type GRLs — WORKS (api-gateway-slots, worker-threads, video-transcode)
- [x] Clear filter returns to all rate limits
- [x] Pagination works (3 pages)

## Rate Limits — Create (`/rate-limits/new`)
- [x] Create form loads (Rate Limit ID, Service, Strategy dropdown, Max Requests, Window)
- [x] Can create a new rate limit ("test-rl-xyz" targeting "test-svc-for-rl")
- [x] 409 Conflict correctly prevents duplicate target (notification-service already has a rate limit)

## Rate Limits — Edit (`/rate-limits/{id}`)
- [x] Edit form loads with existing data (Rate Limit ID disabled)
- [x] Can modify and save (Max Requests 500 → 1000)

## Rate Limits — Delete
- [x] Delete button removes rate limit

## Quotas — List (`/quotas`)
- [x] Page loads, table shows quota data (ID, Resource Pool, Strategy, Max Requests, Window)
- [x] Text search filters quotas ("video" → grl-video only)
- [x] Shows only ResourcePool-type rate limits (9 entries)

## Quotas — Create (`/quotas/new`)
- [x] Create form loads (Quota ID, Resource Pool, Strategy, Max Requests, Window)
- [x] Can create a new quota ("test-quota-xyz" targeting "test-pool-for-quota")

## Quotas — Edit (`/quotas/{id}`)
- [x] Edit form loads with existing data (Quota ID disabled)
- [x] Can modify and save (Max Requests 100 → 200)

## Quotas — Delete
- [x] Delete button removes quota

## Monitor (`/monitor`)
- [x] Page loads without errors
- [x] "All Services - Usage" chart renders (logarithmic scale, Total vs Cap)
- [x] Service dropdown shows all 20 services + "All Services"
- [x] Client dropdown available
- [x] Settings button present
- [x] "Client Breakdown" table loads with data (Client, Service, Req, Denied, Cap, Utilization bar, Status)
- [x] Status badges show "Healthy" and "Denied" correctly
- [x] Pagination works (5 pages for client breakdown)
- [x] "All Services" summary table shows 20 services with Current/Cap/Utilization/Status
- [x] All services show "Available" status

## Active Allocations (`/allocations`)
- [x] Page loads without errors
- [x] "All Pools - Slot Usage" chart renders (Total vs Max Slots)
- [x] Pool and Client dropdown filters present
- [x] Settings button present
- [x] "Client Allocation Detail" table shows live data (Client, Pool, Active, Max, Utilization bar, Denied, Status)
- [x] Status badges: "Available" (green), "Contention" (orange), "At Capacity" (red at 100%)
- [x] Pagination works (3 pages)
- [x] "All Resource Pools" summary table shows 10 pools with Active/Max/Available/Utilization/Status
- [x] Utilization bars colored appropriately (green < 50%, yellow ~50-80%, orange/red > 80%)

## Settings (`/settings`)
- [x] Page loads with 4 settings sections
- [x] Dark Mode toggle works (switches entire UI to dark theme)
- [x] Default Time Range dropdown (current: "Last hour")
- [x] Default Polling Interval dropdown (current: "10 seconds")
- [x] Default Axis Scale dropdown (current: "Logarithmic")

## Navigation
- [x] All sidebar links work and navigate to correct pages
- [x] Back links on editor pages return to list pages
- [x] Active nav item highlighted in sidebar

## Storage Statistics Performance Checks
- Run `python _scripts/performance_baseline.py --base-url http://localhost:5062 --duration-seconds 60 --include-graph-reads` while `traffic_generator.py` is running so long-range graph reads share the same workload as normal access and resource operations.
- Tail the newest Api log instead of loading the whole file:
	`Get-ChildItem .\ClientManager.Api\bin\Debug\net9.0\logs\clientmanager-*.log | Sort-Object LastWriteTime -Descending | Select-Object -First 1 | ForEach-Object { Get-Content $_.FullName -Tail 200 | Select-String 'historical-usage|client-usage-breakdown|Storage API unavailable|503' }`
- Tail the newest StorageApi log with the same bounded pattern:
	`Get-ChildItem .\ClientManager.StorageApi\bin\Debug\net9.0\logs\clientmanager-storageapi-*.log | Sort-Object LastWriteTime -Descending | Select-Object -First 1 | ForEach-Object { Get-Content $_.FullName -Tail 200 | Select-String 'historical-usage|client-usage-breakdown|Storage API unavailable|503' }`
- A healthy run should show long-range `historical-usage` and `client-usage-breakdown` requests completing without a nearby burst of `Storage API unavailable` or runtime `503` entries.
