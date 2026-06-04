# Plan: Code Slim-Down — Step 6: Merged API Controllers Consolidation

> **Status**: ✅ Completed
> **Prerequisite**: [code-slimdown-5-api-services.md](code-slimdown-5-api-services.md)
> **Next**: [code-slimdown-7-api-exceptions-instrumentation.md](code-slimdown-7-api-exceptions-instrumentation.md)
> **Parent**: [code-slimdown-overview.md](code-slimdown-overview.md)

## TL;DR

Consolidate the API's controllers now that they all sit on in-process services: extract a generic CRUD controller base for the catalog endpoints, merge the three near-identical ClientConfiguration settings controllers, and split the oversized `StatisticsController`. Public routes, `[ProducesResponseType]` declarations, and the mandatory XML docs are preserved verbatim — only the implementation structure shrinks.

## Iteration Bootstrap

- **Iteration slug**: `code-slimdown`
- **Required evidence**: `dotnet build ClientManager.Api` clean; Swagger still lists every route with identical paths + response codes; XML docs still surface in Swagger; `git diff --stat` net deletions.
- **UI artifacts to verify**: All AdminUI CRUD pages function identically; no route 404s.
- **Commit-splitting guidance**: (a) generic CRUD controller base + catalog controllers, (b) merge ClientConfiguration settings controllers, (c) split StatisticsController.

## Reference Pattern

In the API catalog controllers (`ServicesController`, `ResourcePoolsController`, `GlobalRateLimitsController`):
- Each repeats GET-all / GET-by-id / POST / PUT / DELETE delegating to its catalog service with identical `[ProducesResponseType]` sets — ideal for a generic base.

In the ClientConfiguration settings controllers (the three rate-limit/quota/access settings controllers under `ClientManager.Api/Controllers/`):
- Near-identical action shapes differing only by the settings sub-resource — merge candidates.

In [ClientManager.Api/Controllers/StatisticsController.cs](ClientManager.Api/Controllers/StatisticsController.cs):
- One large controller mixing several read concerns — split by concern.

## Steps

### 1. Extract a generic CRUD controller base

Create `CrudControllerBase<TEntity, TKey>` (or composition via a shared `ControllerBase` partial) providing the standard list/get/create/update/delete actions delegating to an injected catalog service, with `[ProducesResponseType]` declared once on the base actions. Reduce `ServicesController`, `ResourcePoolsController`, and `GlobalRateLimitsController` to subclasses that set the route prefix and entity binding. **Every controller and action must keep its `/// <summary>` XML docs and `[ProducesResponseType]` codes** per repo conventions — move the docs onto the base actions or keep per-subclass docs; do not drop them.

### 2. Merge the ClientConfiguration settings controllers

Combine the three settings controllers into one controller (or one base + thin route-specific subclasses) keyed by the settings sub-resource, preserving every public route path and response code. Keep XML docs on each action. Confirm Swagger output is byte-equivalent on routes/verbs/response codes.

### 3. Split `StatisticsController`

If `StatisticsController` exceeds ~200 lines or mixes concerns, split it into focused controllers (e.g., summary vs. time-series vs. per-client) sharing the same route base. Preserve routes, response codes, and XML docs. This may slightly increase file count but reduces per-file size and improves clarity.

## Verification

- `dotnet build ClientManager.Api` compiles cleanly.
- Swagger UI lists every previously-existing route with identical paths, verbs, and `[ProducesResponseType]` codes; XML summaries still render.
- `git diff --stat` shows net controller-layer deletions.
- Grep confirms no controller or action is missing its `/// <summary>` doc.
- **UI: Exercise full CRUD on `/services`, `/resourcepools`, `/ratelimits`, and the client settings pages — create, edit, delete — and confirm success with no error toasts.**
- **UI: Open Monitor/Dashboard statistics views and confirm the split StatisticsController still feeds them. Screenshot the Dashboard.**
