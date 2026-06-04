# Decision Log — code-slimdown

## Accepted Waivers

None.

## Open Decisions

None.

## Notes

- Step 1 / logger collapse: chose single method per level `X(string message, object? extraData = null, Exception? exception = null)` and updated existing exception call sites (which passed the exception positionally as the second argument) to keep the exception bound to the `exception` parameter rather than `extraData`. This realizes the plan's "one method per level" intent without the silent behavior change the plan assumed would not occur.
- Step 3 / relocation target (user-confirmed, interactive): chose **Option A** — duplicate the in-process domain service layer into `ClientManager.Api` under the `ClientManager.Api.Services.Storage.*` namespace, rather than introducing a new `ClientManager.Core` library. Rationale: the API already owns HTTP-adapter services with identical names/interfaces (`IAccessControlService`, `ServiceCatalogService`, `StatisticsService`, the catalog trio, `ResourceAllocationService`), and `StorageApi` must keep compiling/running through Step 3 (deletions land in Step 4). A distinct `Services.Storage` namespace avoids the collisions; StorageApi keeps its own copy intact (temporary duplication removed in Step 4). Namespace rewrite rule on copied files: `ClientManager.StorageApi.Services` → `ClientManager.Api.Services.Storage`, `ClientManager.StorageApi.Models` → `ClientManager.Api.Services.Storage.Models`, `ClientManager.StorageApi.Utils` → `ClientManager.Api.Services.Storage.Utils`.
- Step 4 / hot-path fail-open: removed `HotPathFailOpenFilter`, `FailOpenOnErrorAttribute`, and `HotPathResilienceOptions` with the transport layer. Hot-path access-check and resource acquire/release now surface real in-process storage errors via `ErrorHandlingMiddleware` (including `StorageApiProblemException` → declared status codes) instead of silently granting access or allocations when a remote StorageApi was unavailable. Acceptable because there is no remote storage hop; metric/activity names under `ClientManager.StorageApi` are intentionally preserved for dashboard continuity.
