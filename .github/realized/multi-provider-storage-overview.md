# Plan: Multi-Provider Storage Architecture

## Status: ✅ All steps completed

## Overview

Currently, the entire application uses a **single `IDocumentStore`** instance for all persistence concerns — client configurations, rate-limit counters, resource allocations, and usage statistics all go through the same backend. The `PersistenceOptions` configuration selects one provider globally (`JsonFile`, `MongoDb`, or `Redis`) with flat, minimal connection properties that lack real-world settings like TLS certificates, authentication mechanisms, or timeouts.

This plan restructures the storage layer to support **per-domain provider selection** — so you can, for example, use Redis for rate-limit state (fast atomic counters) and MongoDB for statistics (durable time-series) — while also replacing the flat configuration with **proper per-platform options classes** that expose the full set of connection and security settings each platform supports.

The current `IDocumentStore` → `*Database` → Service layer is preserved. The change is at the DI wiring level: instead of one `IDocumentStore` singleton, there are multiple keyed instances, one per storage role. Each `*Database` implementation resolves the store for its role.

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 1 | [multi-provider-storage-1-config-models.md](multi-provider-storage-1-config-models.md) | Create per-platform options classes and storage role configuration model |
| 2 | [multi-provider-storage-2-di-wiring.md](multi-provider-storage-2-di-wiring.md) | Register multiple keyed `IDocumentStore` instances via .NET 8 keyed services |
| 3 | [multi-provider-storage-3-database-migration.md](multi-provider-storage-3-database-migration.md) | Update all `*Database` implementations to resolve their role-specific store |
| 4 | [multi-provider-storage-4-validation-config.md](multi-provider-storage-4-validation-config.md) | Add startup validation, config examples, and startup logging |

## Key Decisions

- **Storage roles** — Four roles: `Configuration` (clients, services, pools, global rate limits), `RateLimiting` (counter state), `Allocations` (resource allocation lifecycle + counters), `Statistics` (usage snapshots). Granularity is chosen to match the user's examples ("Redis for rate limits, Mongo for statistics") while keeping the number of roles manageable.
- **.NET 8 keyed services over factory pattern** — Using `[FromKeyedServices]` / `IKeyedServiceProvider` is idiomatic .NET 8+ DI and avoids a custom `IDocumentStoreFactory`. The `StorageRole` enum is the key.
- **Per-platform options as standalone classes** — `MongoDbStoreOptions`, `RedisStoreOptions`, `JsonFileStoreOptions` are independent classes (not nested) so they can be referenced from the `StorageRoleBinding` that wires a role to a platform. Each class exposes the full connection/security surface for its platform.
- **Configuration lives in `ClientManager.Api`** — Options classes stay in `ClientManager.Api/Models/Configuration/` per existing convention. The `DataAccess` project remains infrastructure-only with no config knowledge.
