# Plan: Split Public API from Storage Service

## Status: ✅ All steps completed

## Overview

`ClientManager.Api` currently references `ClientManager.DataAccess` directly and wires document stores, repositories, stateful domain services, and background jobs into the same host. That keeps everything in-process, but it also makes the public API responsible for storage concurrency, cache ownership, and background persistence behavior. Under multi-instance load, that coupling makes it harder to scale safely because every API node can hit storage, maintain its own caches, and compete on the same counters and allocation state.

The target state is a two-app backend: `ClientManager.Api` stays the public-facing API and thin orchestration layer, while a new internal-only `ClientManager.StorageApi` becomes the single application boundary that talks to `ClientManager.DataAccess`. This follows the existing controller/service patterns already used in `ClientManager.Api` and the typed `HttpClient` wrapper pattern used in `ClientManager.AdminUI`, but it moves coarse-grained commands and queries across the app boundary instead of pushing repository-shaped remote calls over the network.

The split should primarily move existing logic to the correct host rather than create second copies of the same behavior. Any model, request, response, enum, or other type referenced by more than one project must have a single canonical definition in `ClientManager.Shared`; if an internal-only transport contract is needed, add it there once rather than duplicating host-local DTOs.

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 1 | [api-storage-split-1-foundation.md](api-storage-split-1-foundation.md) | Add the new internal storage-facing app, the shared contract seam, and typed clients without changing behavior yet. |
| 2 | [api-storage-split-2-configuration-split.md](api-storage-split-2-configuration-split.md) | Move configuration CRUD and catalog queries behind the new internal app and make the public API proxy them. |
| 3 | [api-storage-split-3-runtime-state.md](api-storage-split-3-runtime-state.md) | Move concurrency-sensitive runtime operations and hosted jobs close to storage behind coarse-grained internal commands. |
| 4 | [api-storage-split-4-read-models-cleanup.md](api-storage-split-4-read-models-cleanup.md) | Move statistics and exporter read models, then remove every remaining `ClientManager.DataAccess` dependency from the public API. |
| 5 | [api-storage-split-5-caching-rollout.md](api-storage-split-5-caching-rollout.md) | Add cache ownership, resiliency, deployment, and validation steps so the split is safe to roll out under load. |

## Key Decisions

- **Internal service name** — Add a new internal-only ASP.NET Core app named `ClientManager.StorageApi` and make it the only host that references `ClientManager.DataAccess`.
- **Boundary shape** — Move coarse-grained commands and queries such as access checks, allocation acquire/release, configuration CRUD/search, and statistics reads; do not replace in-process repositories with chatty remote repository calls.
- **Shared contract ownership** — Any type used by more than one project lives in `ClientManager.Shared`. Reuse existing `Models/Requests`, `Models/Responses`, `Models/Search`, `Models/Entities`, and `Models/Enums` first; add new internal contracts there only when no existing shared type fits.
- **Move-first simplification** — Prefer relocating existing controllers, services, strategies, and helpers into `ClientManager.StorageApi` with minimal edits. Do not keep duplicate business logic in both hosts longer than a migration step requires.
- **Transport** — Start with HTTP/JSON plus typed `HttpClient` wrappers because that matches the repo’s existing ASP.NET and client-service patterns. Measure the extra hop before considering gRPC.
- **Cache ownership** — Put authoritative configuration and read-model caching in `ClientManager.StorageApi`; avoid adding new cache layers in `ClientManager.Api` unless measurement shows a clear need.
- **Production storage prerequisite** — Treat the `JsonFile` provider as development-only for this split. Multi-instance `ClientManager.StorageApi` deployments need a shared backend such as Redis or MongoDB so counters and allocation state stay correct.
