# Implementation Handoff

## Current Pass

- Pass type: Initial implementation
- Authoring agent: @Implement
- Plan step: .github/plans/hot-path-performance-observability-1-baseline-runtime.md
- Branch: main
- Summary: Restored source build/startup, shared JsonFile/Lucene local stores by resolved path, fixed deterministic benchmark routing/output, and captured a rebuilt before artifact. Runtime baseline verification is partially blocked by current hot-path latency causing public API 503s under the 5 second StorageApi timeout.

## Files Changed

| Path | Intent | Verification impact |
|------|--------|---------------------|
| ClientManager.DataAccess/Stores/Implementations/LuceneDocumentStore.cs | Added on-disk index-directory construction while preserving parameterless RAM-directory construction. | StorageApi can compile when Lucene provider construction passes an index path. |
| ClientManager.StorageApi/Utils/Extensions/DocumentStoreFactory.cs | Reused JsonFile and Lucene stores from caller-provided caches keyed by resolved absolute path. | Role bindings targeting the same local path share a single document-store instance. |
| ClientManager.StorageApi/Utils/Extensions/StorageProviderRegistrationExtensions.cs | Added per-registration JsonFile/Lucene caches and Windows-aware path comparer. | Local providers mirror Mongo/Redis connection reuse across storage roles. |
| _scripts/performance_baseline.py | Added `--output`, rerouted unsupported acquire/release actions to access, and guarded graph dispatch behind explicit graph action. | 60 second baseline can write a named artifact and no longer falls into graph reads when graph scenarios are disabled. This file is ignored by `.gitignore` via `_scripts/`; commit with `git add -f` if this repo wants it versioned. |
| .github/plans/hot-path-performance-baseline-before.json | Captured rebuilt source before artifact beside the provisional baseline. | Provides Step 1 measurement evidence, but contains many 503s because current source hot paths exceed the public API StorageApi timeout under load. |

## Verification

| Check | Method | Result | Evidence |
|-------|--------|--------|----------|
| Full solution build | `dotnet build .\ClientManager.slnx` | PASS after clearing stale app locks | Initial build failed only because pre-existing StorageApi/Api/AdminUI processes locked output DLLs. Final build after runtime shutdown succeeded in 81.8s with 31 existing StorageApi XML-doc warnings. |
| Touched-file diagnostics | VS Code diagnostics on edited C# and Python files | PASS | No errors reported for LuceneDocumentStore.cs, DocumentStoreFactory.cs, StorageProviderRegistrationExtensions.cs, or performance_baseline.py. |
| Benchmark script syntax | `python -m py_compile .\_scripts\performance_baseline.py` | PASS | Command completed with no output. |
| StorageApi source startup | `dotnet run --no-launch-profile --project .\ClientManager.StorageApi\ClientManager.StorageApi.csproj` with `Persistence__DefaultJsonFile__DataDirectory=C:\Users\Marcus\source\repos\ClientManager\data` | PASS | StorageApi listened on 5063 and `GET http://localhost:5063/internal/v1/statistics/global-usage` returned 200. |
| Public API source startup | `dotnet run --no-launch-profile --project .\ClientManager.Api\ClientManager.Api.csproj` with `StorageApi__BaseUrl=http://localhost:5063` | PASS with transient circuit-breaker warmup | API listened on 5062; `GET http://localhost:5062/api/v1/statistics/global-usage` returned 200 after restart/warmup. |
| AdminUI source startup | `dotnet run --no-launch-profile --project .\ClientManager.AdminUI\ClientManager.AdminUI.csproj` with `ApiBaseUrl=http://localhost:5062` | PASS | `GET http://localhost:5100/` returned 200. |
| Seed data | `python .\_scripts\seed_data.py --base-url http://localhost:5062` | PASS | Seed reported existing services, pools, global rate limits, and clients; merged 5,726 usage snapshot documents and wrote historical buckets. StorageApi was restarted afterward as the script instructed. |
| Traffic generator | `python .\_scripts\traffic_generator.py --base-url http://localhost:5062 --interval 0.2` | RAN with runtime degradation | Generator reported live traffic and stats, but many operations returned 503 once the public API circuit breaker opened. Traffic was stopped before API/StorageApi/AdminUI shutdown. |
| 60 second baseline artifact | `python .\_scripts\performance_baseline.py --base-url http://localhost:5062 --duration-seconds 60 --data-directory C:\Users\Marcus\source\repos\ClientManager\data --output .\.github\plans\hot-path-performance-baseline-before.json` | PARTIAL | Artifact created. `graph_reads_enabled=false`, graph operations empty, access count 451, acquire count 105. Release count remained 0 because all acquire requests in the artifact returned 503. Runtime unexpected failures: 685 total 503s. |
| Hot-path timeout evidence | Direct StorageApi and public API access-check probes | BLOCKER EVIDENCE | Direct StorageApi `POST /internal/v1/runtime/access/check` took 8084.8 ms and returned 200; public API `POST /api/v1/access/check` returned 503 after 5043.2 ms. This explains the 503-heavy baseline and zero release count. |
| Dashboard UI | Browser at `http://localhost:5100/` | PASS | Dashboard rendered summary cards: 25 clients, 20 services, 10 resource pools, requests/min, and pool acquisition. No in-app error banner was visible. |
| Monitor UI | Fresh browser page at `http://localhost:5100/monitor` | PARTIAL | Monitor heading, controls, chart area, and client breakdown table rendered. Browser recorded 404s for Radzen static assets and screenshot showed degraded styling/nav overlap. |
| Allocations UI | Fresh browser page at `http://localhost:5100/allocations` | PARTIAL | Active Allocations heading, controls, chart area, and allocation detail table rendered. Browser recorded the same Radzen static asset 404s and screenshot showed degraded styling/nav overlap. |
| Runtime shutdown | Stopped traffic generator, then API, StorageApi, AdminUI; checked ports | PASS | `Get-NetTCPConnection` for ports 5062, 5063, and 5100 returned no listeners. |
| Diff hygiene | `git diff --check` | PASS | Command completed with no output. |

## Review Responses

| Finding ID | Disposition | Evidence | Notes |
|------------|-------------|----------|-------|
| N/A | ALREADY SATISFIED | review-packet.md contains no findings for this pass. | No delegated review findings were supplied. |

## Risks And Follow-Ups

- The required rebuilt baseline artifact was produced, but it is not a clean performance baseline: current source StorageApi hot-path work can exceed the public API 5 second timeout, opening the circuit breaker and producing many 503s.
- The plan's verification expectation for nonzero release counts could not be satisfied because no acquire request succeeded during the 60 second artifact run.
- AdminUI pages render data, but Radzen static assets return 404 under this source-run configuration, causing degraded styling and visible nav/control overlap in screenshots. This appears pre-existing and outside this step's touched files.
- `_scripts/performance_baseline.py` is ignored by `.gitignore`; use `git add -f _scripts/performance_baseline.py` if the change should be committed.

## Pass History

| Pass | Commit | Summary |
|------|--------|---------|
| 1 | Uncommitted | Implemented Step 1 source/runtime fixes, captured before artifact, and documented runtime/UI blockers. |
