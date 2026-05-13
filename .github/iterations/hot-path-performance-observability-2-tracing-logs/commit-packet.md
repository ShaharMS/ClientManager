# Commit Packet

## Commit Intent

- Pass type: Initial implementation
- Plan step: .github/plans/hot-path-performance-observability-2-tracing-logs.md
- Scope: Distributed tracing, hot-path histograms, and structured timing logs
- Reason this is one commit: The changed files implement one cohesive observability pass across Api, StorageApi, rate-limit strategies, and document-store operations for the Step 2 tracing/logs plan.
- Verification disposition: Commit after the implementation pass verified `dotnet build .\ClientManager.slnx`, workspace diagnostics with no errors, startup without an OTLP endpoint, Api and StorageApi `/prometheus/otel` HTTP 200 responses, clean public access/acquire/release probes, AdminUI `/monitor` and `/allocations` smoke checks, and `git diff --check`.
- Remaining risk: OTLP export against a real collector was not run; new telemetry exposed known JSON-file lock-wait and counter contention that remains planned for later steps.

## Candidate Files

| Path | Include | Reason |
|------|---------|--------|
| ClientManager.Api/ClientManager.Api.csproj | Yes | Adds OTLP exporter and HttpClient instrumentation package references for Api tracing. |
| ClientManager.Api/Program.cs | Yes | Configures Api OpenTelemetry resources, tracing, HttpClient/AspNetCore instrumentation, ActivitySources, and optional OTLP export. |
| ClientManager.Api/Services/InternalClients/Implementations/RuntimeStateClient.cs | Yes | Adds public-to-storage spans, hot-path duration metrics, result/status tags, and structured timing logs for access/acquire/release calls. |
| ClientManager.Api/Utils/Instrumentation/ClientManagerMetrics.cs | Yes | Adds Api ActivitySource and hot-path/storage-client duration histograms. |
| ClientManager.DataAccess/Stores/Implementations/JsonFileDocumentStore.cs | Yes | Tags current Activity with JSON write-lock wait timing for storage operations. |
| ClientManager.DataAccess/Stores/Implementations/LuceneDocumentStore.cs | Yes | Tags current Activity with Lucene write-lock wait timing for storage operations. |
| ClientManager.StorageApi/ClientManager.StorageApi.csproj | Yes | Adds OTLP exporter and HttpClient instrumentation package references for StorageApi tracing. |
| ClientManager.StorageApi/Program.cs | Yes | Configures StorageApi OpenTelemetry resources, tracing, HttpClient/AspNetCore instrumentation, ActivitySources, and optional OTLP export. |
| ClientManager.StorageApi/Services/Implementations/AccessControlService.cs | Yes | Adds access-check parent/child spans, duration histogram recording, and structured completion/denial/error timing logs. |
| ClientManager.StorageApi/Services/Implementations/RateLimiting/RateLimitService.cs | Yes | Adds spans and timing logs around client/global checks, strategy dispatch, and global-limit reads. |
| ClientManager.StorageApi/Services/Implementations/RateLimiting/Strategies/ApproximateSlidingWindowStrategy.cs | Yes | Wraps sliding-window evaluate/peek work with shared strategy tracing and metrics. |
| ClientManager.StorageApi/Services/Implementations/RateLimiting/Strategies/FixedWindowStrategy.cs | Yes | Wraps fixed-window evaluate/peek work with shared strategy tracing and metrics. |
| ClientManager.StorageApi/Services/Implementations/RateLimiting/Strategies/TokenBucketStrategy.cs | Yes | Wraps token-bucket evaluate/peek work with shared strategy tracing and metrics. |
| ClientManager.StorageApi/Services/Implementations/RateLimiting/Strategies/RateLimitStrategyInstrumentation.cs | Yes | Adds shared rate-limit strategy instrumentation helper for spans and histograms. |
| ClientManager.StorageApi/Services/Implementations/ResourceAllocationService.cs | Yes | Adds resource acquire/release parent/child spans, duration histograms, and structured timing logs. |
| ClientManager.StorageApi/Utils/Extensions/StorageProviderRegistrationExtensions.cs | Yes | Wraps keyed document-store registrations with instrumentation. |
| ClientManager.StorageApi/Utils/Instrumentation/StorageApiMetrics.cs | Yes | Adds StorageApi ActivitySource plus hot-path, strategy, and document-store histograms. |
| ClientManager.StorageApi/Utils/Instrumentation/InstrumentedDocumentStore.cs | Yes | Adds document-store operation spans, metrics, structured timing logs, and lock-wait reporting. |
| .github/iterations/hot-path-performance-observability-2-tracing-logs/run-ledger.md | Yes | Keeps the Step 2 ledger aligned with the completed initial implementation pass. |
| .github/iterations/hot-path-performance-observability-2-tracing-logs/implementation-handoff.md | Yes | Preserves implementation summary, verification evidence, and remaining risks. |
| .github/iterations/hot-path-performance-observability-2-tracing-logs/review-packet.md | Yes | Preserves the empty pending review packet for the next loop phase. |
| .github/iterations/hot-path-performance-observability-2-tracing-logs/commit-packet.md | Yes | Records this Inscribe commit grouping and gitflow decision. |
| .github/iterations/hot-path-performance-observability-2-tracing-logs/decision-log.md | Yes | Preserves the current empty decision/waiver record. |
| .github/iterations/hot-path-performance-observability-2-tracing-logs/timeline.md | Yes | Appends this Inscribe transition. |
| .github/iterations/hot-path-performance-observability-2-tracing-logs/execution-report.md | Yes | Preserves the in-progress execution report for @Iterate closeout. |
| .github/agent-progress/hot-path-performance-observability-2-tracing-logs.md | Yes | Updates the related iteration resume note for the selected plan step. |

## Gitflow Decision

- Starting branch: feature/hot-path-performance-observability-1-baseline-runtime
- Target branch: feature/hot-path-performance-observability-1-baseline-runtime
- Branch action: Stayed on the existing feature branch; no branch switch was needed because the current branch already satisfies gitflow for this feature-sized Step 2 implementation pass.

## Commit Message

```text
feat(observability): trace hot-path storage operations

Plan: .github/plans/hot-path-performance-observability-2-tracing-logs.md
Pass: initial implementation

Adds Api and StorageApi ActivitySource tracing, hot-path histograms,
structured timing logs, rate-limit strategy instrumentation, document-store
operation spans, and lock-wait tags for the access/acquire/release paths.

OTLP collector export remains unverified; the new telemetry preserves the
known JSON-file lock-wait and counter-contention risk for later steps.
```

## Result

- Commit hash: Reported by @Inscribe final response because this commit cannot contain its own Git object hash without a follow-up dirty-file loop.
- Push result: Reported by @Inscribe final response after checking `origin`.
- Workspace status after commit: Reported by @Inscribe final response.
- Remaining uncommitted files: Reported by @Inscribe final response.
- Follow-up needed: Run OTLP export against a real collector when available, and continue with the planned storage-counter/contention remediation in later steps.

## Commit History

| Pass | Commit | Branch | Notes |
|------|--------|--------|-------|
| Initial implementation | Reported by @Inscribe final response | feature/hot-path-performance-observability-1-baseline-runtime | Adds Step 2 tracing, histograms, document-store instrumentation, and structured timing logs while preserving OTLP and JSON-file contention follow-ups. |
