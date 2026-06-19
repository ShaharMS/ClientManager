# Configuration reference

ClientManager configuration is split across the API host and the Admin UI host. Most operational settings live on the API.

!!! note "Defaults live in code"
    The checked-in `ClientManager.Api/appsettings.json` is minimal. If a section is omitted, **code defaults apply** — especially for `Persistence`, which defaults to **JsonFile** with data in `./data`.

Environment variables override JSON using the standard ASP.NET Core convention: `Section__SubSection__Property` (double underscore).

## API host (`ClientManager.Api`)

### `Persistence`

Binds to `PersistenceOptions`. Controls which storage backend each **role** uses.

| Property | Default | Description |
| --- | --- | --- |
| `DefaultProvider` | `JsonFile` | Fallback provider for all roles without a `Roles` override |
| `DefaultMongoDb` | — | Shared MongoDB settings when `DefaultProvider` or a role uses `MongoDb` |
| `DefaultRedis` | — | Shared Redis settings when `DefaultProvider` or a role uses `Redis` |
| `DefaultJsonFile` | `DataDirectory: "./data"` | Shared JsonFile settings |
| `DefaultLucene` | `IndexDirectory: "./lucene-index"` | Shared Lucene settings |
| `Roles` | — | Per-role overrides (`Configuration`, `RateLimiting`, `Allocations`, `Statistics`) |

Each `Roles` entry:

| Property | Description |
| --- | --- |
| `Provider` | `JsonFile`, `MongoDb`, `Redis`, or `Lucene` |
| `MongoDb` / `Redis` / `JsonFile` / `Lucene` | Provider-specific options for this role only |

**MongoDB options** (`DefaultMongoDb` or per-role `MongoDb`):

| Property | Default | Notes |
| --- | --- | --- |
| `ConnectionString` | *required* | e.g. `mongodb://mongo:27017` |
| `DatabaseName` | `ClientManager` | |
| `UseTls` | `false` | Enables TLS; applies certificate settings |
| `TlsCertificatePath` / `TlsCertificatePassword` | — | mTLS client cert (PFX) |
| `AllowInsecureTls` | `false` | Dev only |
| `AuthenticationMechanism` | — | e.g. `SCRAM-SHA-256`, `MONGODB-X509` |
| `ConnectTimeoutSeconds` | `30` | |
| `MaxConnectionPoolSize` | `100` | |
| `RetryWrites` | `true` | |

**Redis options** (`DefaultRedis` or per-role `Redis`):

| Property | Default | Notes |
| --- | --- | --- |
| `Host` | *required* | Hostname only — do **not** embed `host:port` in `Host` |
| `Port` | `6379` | |
| `DatabaseIndex` | `0` | Use different indexes to separate roles on one server |
| `Password` / `User` | — | ACL auth |
| `UseSsl` | `false` | Toggles Redis SSL flag |
| `UseTls` | `false` | Enables TLS behavior and implies SSL |
| `TlsCertificatePath` / `TlsCertificatePassword` | — | mTLS |
| `AllowInsecureTls` | `false` | Dev only |
| `GlobalKeyPrefix` | — | e.g. `clientmanager:` |
| `ConnectTimeoutMilliseconds` | `5000` | |
| `ConnectRetry` | `5` | |
| `AbortOnConnectFail` | `false` | When false, startup continues if Redis is temporarily down |
| `SyncTimeoutMilliseconds` | `5000` | |

**JsonFile options**:

| Property | Default |
| --- | --- |
| `DataDirectory` | `./data` |
| `PrettyPrint` | `true` |

**Lucene options**:

| Property | Default |
| --- | --- |
| `IndexDirectory` | `./lucene-index` |

Example — mixed Redis/Mongo (common production layout):

```json
{
  "Persistence": {
    "DefaultProvider": "MongoDb",
    "DefaultMongoDb": {
      "ConnectionString": "mongodb://mongo:27017",
      "DatabaseName": "ClientManager"
    },
    "Roles": {
      "RateLimiting": {
        "Provider": "Redis",
        "Redis": { "Host": "redis", "Port": 6379, "DatabaseIndex": 1 }
      },
      "Allocations": {
        "Provider": "Redis",
        "Redis": { "Host": "redis", "Port": 6379, "DatabaseIndex": 2 }
      }
    }
  }
}
```

See the [Persistence guide](persistence-guide.md) for topology guidance.

Environment examples:

```text
Persistence__DefaultProvider=MongoDb
Persistence__DefaultMongoDb__ConnectionString=mongodb://mongo:27017
Persistence__Roles__RateLimiting__Provider=Redis
Persistence__Roles__RateLimiting__Redis__Host=redis
```

### `Seed`

Binds to `SeedOptions`. When any list is non-empty, `DataSeedService` runs at startup and creates missing catalog entries (idempotent — existing IDs are skipped).

| Property | Type | Description |
| --- | --- | --- |
| `ClientConfigurations` | `ClientConfiguration[]` | Clients to ensure exist |
| `Services` | `Service[]` | Service catalog entries |
| `ResourcePools` | `ResourcePool[]` | Pool definitions |
| `GlobalRateLimits` | `GlobalRateLimit[]` | Global limit rules |

Alternative to `Seed` config: run `python _scripts/seed_data.py` after startup.

### `StorageReadCache`

| Property | Default | Description |
| --- | --- | --- |
| `CatalogTtl` | `00:00:30` | Cache lifetime for client/service/pool/global-limit reads on the hot path; also bounds cross-pod catalog staleness |
| `StatisticsTtl` | `00:00:05` | Cache lifetime for statistics and exporter reads |

Catalog writes invalidate the local pod's cache immediately. Other pods refresh on the next read after `CatalogTtl`.

### `BackgroundWorkers`

| Property | Default | Description |
| --- | --- | --- |
| `RequireLeaderLock` | `true` | When true, rollup and allocation cleanup skip a cycle if the storage-backed leader lease cannot be acquired |
| `LeaderLeaseDuration` | `00:00:30` | Lease TTL for leader election on the shared `RateLimiting` storage role |

### `UsageTracking`

Controls usage-buffer flush cadence and snapshot retention.

| Property | Default | Description |
| --- | --- | --- |
| `SecondFlushInterval` | `00:00:01` | Fast loop — near-real-time dashboards |
| `FlushInterval` | `00:05:00` | Slow rollup/pruning loop |
| `SecondRetention` | `00:05:00` | Per-second bucket retention |
| `FiveMinuteRetention` | `1.00:00:00` | Five-minute bucket retention |
| `HourlyRetention` | `7.00:00:00` | Hourly bucket retention |
| `DailyRetention` | `90.00:00:00` | Daily bucket retention |

### `Observability`

| Property | Default | Description |
| --- | --- | --- |
| `OtlpEndpoint` | — | Absolute URI for OTLP trace export (e.g. `http://localhost:4317`). When empty, traces stay in-process only. |

`appsettings.Development.json` sets `OtlpEndpoint` to `http://localhost:4317` for local Jaeger via `launch_observability_ui.py`. For Prometheus scrape jobs, metric names, and Grafana setup, see the [Metrics integration guide](metrics-integration-guide.md).

### `ApiVersioning`

| Property | Default | Description |
| --- | --- | --- |
| `DefaultVersion` | `1.0` | URL segment version (`/api/v1/...`) |

### `Logging`

Standard ASP.NET Core logging levels. The API also supports:

| Property | Description |
| --- | --- |
| `Logging:Elasticsearch:Uri` | Optional Elasticsearch sink target (NLog) |

NLog configuration is in `nlog.config` beside the API project.

### URLs and hosting

| Setting | Default | Notes |
| --- | --- | --- |
| `ASPNETCORE_URLS` | `http://+:5062` in Docker | Launch profile: `http://localhost:5062` |
| `ASPNETCORE_ENVIRONMENT` | — | `Development` enables permissive HTTPS dev cert handling in related tooling |

## Admin UI host (`ClientManager.AdminUI`)

| Property | Default | Description |
| --- | --- | --- |
| `ApiBaseUrl` | `http://localhost:5062` | Base URL for all API calls |
| `Logging:LogLevel` | `Information` | Standard ASP.NET logging |

In Docker Compose, `ApiBaseUrl` is set to `http://api:5062`.

## Script environment

`_scripts/configuration.py` centralizes URLs and seed catalogs for Python helpers:

| Setting | Value |
| --- | --- |
| API base URL | `http://localhost:5062` |
| API prefix | `/api/v1` |
| Storage data dir env var | `CLIENTMANAGER_STORAGE_DATA_DIR` |

Override the API URL with `--base-url` on seed and traffic scripts.

## Related reading

- [Persistence guide](persistence-guide.md) — what each role stores and recommended topologies
- [Development and operations](development-and-operations.md) — observability stack and Docker
- [Getting started](getting-started.md) — first run and seed commands
