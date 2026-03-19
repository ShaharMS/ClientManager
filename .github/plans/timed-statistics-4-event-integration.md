# Plan: Timed Statistics — Step 4: Event Integration

> **Status**: 🔲 Not started
> **Prerequisite**: [timed-statistics-3-collection.md](timed-statistics-3-collection.md)
> **Next**: [timed-statistics-5-background-service.md](timed-statistics-5-background-service.md)
> **Parent**: [timed-statistics-overview.md](timed-statistics-overview.md)

## TL;DR

Wire `IUsageRecorder` calls into `AccessControlService` and `ResourceAllocationService` so that every access check and every resource allocation event is recorded in the in-memory buffer. This is the integration point that feeds data into the time-series pipeline.

## Reference Pattern

In [ClientManager.Api/Services/AccessControlService.cs](ClientManager.Api/Services/AccessControlService.cs):
- Already records OTel metrics via `ClientManagerMetrics` (e.g., `_metrics.AccessGranted.Add(1, ...)`)
- Usage recorder calls should be placed alongside these existing metric calls
- Service receives dependencies via constructor injection

In [ClientManager.Api/Services/ResourceAllocationService.cs](ClientManager.Api/Services/ResourceAllocationService.cs):
- Already records OTel metrics via `ClientManagerMetrics` for acquire/release/denied events
- Same pattern: add `IUsageRecorder` calls alongside existing metric calls

## Steps

### 1. Add `IUsageRecorder` to `AccessControlService`

Edit [ClientManager.Api/Services/AccessControlService.cs](ClientManager.Api/Services/AccessControlService.cs):

**Constructor**: Add `IUsageRecorder usageRecorder` parameter and store as `_usageRecorder` field.

**`CheckAccessAsync` method**: Add recorder calls at these exact points:

- After each `_metrics.AccessDenied.Add(...)` call (for ClientDisabled, NotAllowed, GlobalRateLimited, RateLimited denials):
  ```csharp
  _usageRecorder.RecordServiceRequest(clientId, serviceId, UsageEventType.Denied);
  ```

- After the `_metrics.AccessGranted.Add(...)` call (on successful access):
  ```csharp
  _usageRecorder.RecordServiceRequest(clientId, serviceId, UsageEventType.Granted);
  ```

There are 4 denial points in the method (ClientDisabled, NotAllowed, GlobalRateLimited, RateLimited) and 1 grant point. Each gets a corresponding recorder call.

**Note**: Do NOT record in `GetClientAccessibilityAsync` — that's a read-only report, not an actual access event.

### 2. Add `IUsageRecorder` to `ResourceAllocationService`

Edit [ClientManager.Api/Services/ResourceAllocationService.cs](ClientManager.Api/Services/ResourceAllocationService.cs):

**Constructor**: Add `IUsageRecorder usageRecorder` parameter and store as `_usageRecorder` field.

**`AcquireAsync` method**:

- After each denial path (client disabled, pool slot limit exceeded, rate limit exceeded, system capacity full):
  ```csharp
  _usageRecorder.RecordAllocationEvent(clientId, resourcePoolId, UsageEventType.Denied);
  ```

- After successful acquisition (allocation created):
  ```csharp
  _usageRecorder.RecordAllocationEvent(clientId, resourcePoolId, UsageEventType.Granted);
  ```

**`ReleaseAsync` and `CleanupExpiredAllocationsAsync`**: No recording needed — releases are not "events" for time-series purposes. The granted/denied pattern captures demand over time, which is what we want.

## Verification

- Solution compiles without errors
- Making an access check via `POST /api/access/check` increments the appropriate buffer counter (verify by inspecting `UsageBuffer` state in a debug session or test)
- Making a resource acquire via `POST /api/resources/acquire` increments the buffer
- Denied requests (rate limit, access denied, etc.) record `Denied` events
- Successful requests record `Granted` events
- `GetClientAccessibilityAsync` does NOT record events
- `ReleaseAsync` does NOT record events
