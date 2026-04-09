# Plan: Harden API/Storage Split Reliability

## Status: 🔲 Not started

## Overview

The current API/storage split lands the broad architectural move from the realized split plan, but the active diff shows several places where behavior was moved without preserving the original reliability guarantees. The riskiest areas are the runtime hot path, HTTP proxy semantics, and read-model consistency: pieces that used to work because they lived in one process now depend on a new internal transport boundary, yet error mapping, cache invalidation, and status-code parity are not consistently handled.

The target state is not a bigger rewrite. It is a correctness pass that makes the split behave like the original single-host system where it must, while still keeping `ClientManager.Api` free of `ClientManager.DataAccess`. The plan follows the existing move-first split strategy from the realized plan, but adds a stricter parity and validation pass before treating the split as production-safe.

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 1 | [api-storage-split-reliability-1-runtime-parity.md](.github/plans/api-storage-split-reliability-1-runtime-parity.md) | Restore semantic parity for access checks, resource allocation, and rate limiting across the new internal boundary. |
| 2 | [api-storage-split-reliability-2-transport-contracts.md](.github/plans/api-storage-split-reliability-2-transport-contracts.md) | Fix public-to-storage HTTP client behavior so status codes, not-found handling, and outage behavior are deliberate instead of accidental. |
| 3 | [api-storage-split-reliability-3-read-models-rollout.md](.github/plans/api-storage-split-reliability-3-read-models-rollout.md) | Harden statistics, caching, configuration wiring, and rollout validation so the split is actually operable under load. |

## Key Decisions

- **Parity over novelty** — The reliability pass should preserve the old external behavior first; do not accept semantic drift just because a method now happens to call HTTP.
- **Single owner remains** — `ClientManager.StorageApi` stays the only host that references `ClientManager.DataAccess`; fixes should not reintroduce storage abstractions into `ClientManager.Api`.
- **Typed client discipline** — Internal clients may stay thin, but they must map storage responses to the same public API outcomes the original in-process services produced.
- **Runtime hot path first** — Access checks and resource allocation are higher risk than catalog CRUD because they own counters, capacity, and rate-limit correctness.
- **Rollout skepticism** — Do not treat “builds locally” as sufficient verification; browser checks, failure-path checks, and traffic-driven validation are required before declaring the split reliable.