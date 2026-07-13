# Configuration reference

ClientManager configuration is split across the API host and the Admin UI host. Most operational settings live on the API.

!!! note "Defaults live in code"
    The checked-in `ClientManager.Api/appsettings.json` is minimal. If a section is omitted, **code defaults apply** — `Persistence` defaults to **Redis** on `localhost:6379`.

Environment variables override JSON using `Section__SubSection__Property` (double underscore).

## API host (`ClientManager.Api`)

### `Persistence`

Binds to `PersistenceOptions`. Controls which storage backend each **role** uses.

Supported providers: **MongoDb** and **Redis** only.

| Property | Default | Description |
| --- | --- | --- |
| `DefaultProvider` | `Redis` | Fallback provider for all roles without a `Roles` override |
| `DefaultMongoDb` | — | Shared MongoDB settings |
| `DefaultRedis` | `Host: localhost`, `Port: 6379`, `DatabaseIndex: 0` | Shared Redis settings |
| `Roles` | — | Per-role overrides (`Configuration`, `RateLimiting`, `Rpm`) |

Each `Roles` entry:

| Property | Description |
| --- | --- |
| `Provider` | `MongoDb` or `Redis` |
| `MongoDb` / `Redis` | Provider-specific options for this role only |

**MongoDB options** (`DefaultMongoDb` or per-role `MongoDb`):

| Property | Default | Notes |
| --- | --- | --- |
| `ConnectionString` | *required* | e.g. `mongodb://mongo:27017` |
| `DatabaseName` | `ClientManager` | |
| `UseTls` | `false` | Enables TLS |
| `TlsCertificatePath` / `TlsCertificatePassword` | — | mTLS client cert (PFX) |
| `AllowInsecureTls` | `false` | Dev only |
| `AuthenticationMechanism` | — | e.g. `SCRAM-SHA-256` |
| `ConnectTimeoutSeconds` | `30` | |
| `MaxConnectionPoolSize` | `100` | |
| `RetryWrites` | `true` | |

**Redis options** (`DefaultRedis` or per-role `Redis`):

| Property | Default | Notes |
| --- | --- | --- |
| `Host` | *required* | Hostname only — do **not** embed `host:port` in `Host` |
| `Port` | `6379` | |
| `DatabaseIndex` | `0` | Use different indexes to separate roles |
| `Password` / `User` | — | ACL auth |
| `UseSsl` / `UseTls` | `false` | TLS |
| `TlsCertificatePath` / `TlsCertificatePassword` | — | mTLS |
| `AllowInsecureTls` | `false` | Dev only |
| `GlobalKeyPrefix` | — | e.g. `clientmanager:` |
| `ConnectTimeoutMilliseconds` | `5000` | |
| `ConnectRetry` | `5` | |
| `AbortOnConnectFail` | `false` | When false, startup continues if Redis is temporarily down |
| `SyncTimeoutMilliseconds` | `5000` | |

Example — MongoDB catalog, Redis hot paths:

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
      "Rpm": {
        "Provider": "Redis",
        "Redis": { "Host": "redis", "Port": 6379, "DatabaseIndex": 2 }
      }
    }
  }
}
```

See [Persistence overview](persistence/index.md).

Environment examples:

```text
Persistence__DefaultProvider=MongoDb
Persistence__DefaultMongoDb__ConnectionString=mongodb://mongo:27017
Persistence__Roles__RateLimiting__Provider=Redis
Persistence__Roles__RateLimiting__Redis__Host=redis
```

### `Seed`

Binds to `SeedOptions`.

| Property | Type | Description |
| --- | --- | --- |
| `SeedApiEnabled` | `bool` | When `false`, all `/api/v1/seed` endpoints return HTTP 404 |
| `ClientConfigurations` | `ClientConfiguration[]` | Optional payload shape for export/import (not auto-applied at startup) |
| `Services` | `Service[]` | Service catalog entries |
| `GlobalRateLimits` | `GlobalRateLimit[]` | Global limit rules (`id` = service ID, nested `policy`) |

**Generate from a running instance:** `GET /api/v1/seed` with `SeedApiEnabled: true`. Paste JSON into import requests or keep as reference.

**Runtime import:** `POST` or `PUT /api/v1/seed` — requires `SeedApiEnabled: true`. See [Seed system](core/seed-system.md).

### `Rpm`

Configures the global RPM second-bucket ring and per-replica flush batching.

| Property | Default | Description |
| --- | --- | --- |
| `BucketSizeSeconds` | `1` | Bucket width; must divide evenly into the fixed 5-minute RPM window |
| `Retention` | `00:10:00` | How long buckets are kept; must be ≥ 5 minutes |
| `FlushEventCount` | `100` | Flush after this many buffered events per replica; `1` disables batching |
| `FlushInterval` | `00:00:01` | Timer-based flush interval per replica |

RPM is always computed over a fixed **five-minute window** (`RpmOptions.RpmWindow`).

### `StorageReadCache`

In-memory catalog read-cache TTLs.

| Property | Default | Description |
| --- | --- | --- |
| `CatalogTtl` | `00:00:30` | Configuration catalog reads |
| `HotPathCatalogTtl` | `00:00:01` | Global-limit lookups on access-check path |

### `RateLimiting`

| Property | Default | Description |
| --- | --- | --- |
| `WindowAlignmentAnchor` | `00:00:00` | UTC anchor for fixed-window alignment |

### `Observability`

| Property | Default | Description |
| --- | --- | --- |
| `OtlpEndpoint` | — | Absolute URI for OTLP trace export (e.g. `http://localhost:4317`) |

`appsettings.Development.json` sets `OtlpEndpoint` to `http://localhost:4317`. For Prometheus, see the [Metrics integration guide](metrics-integration-guide.md).

### `Logging`

Standard ASP.NET Core logging levels are written through NLog.

### URLs and hosting

| Setting | Default | Notes |
| --- | --- | --- |
| `ASPNETCORE_URLS` | `http://+:5062` in Docker | Launch profile: `http://localhost:5062` |
| `ASPNETCORE_ENVIRONMENT` | — | `Development` enables permissive tooling defaults |

## Admin UI host (`ClientManager.AdminUI`)

| Property | Default | Description |
| --- | --- | --- |
| `ApiBaseUrl` | `http://localhost:5062` | Base URL for all API calls |

In Docker Compose, `ApiBaseUrl` is set to `http://api:5062`.

## Script environment

`_scripts/configuration.py` centralizes URLs for Python helpers. Override with `--base-url` on seed and traffic scripts.

## Related reading

- [Persistence overview](persistence/index.md)
- [Development and operations](development-and-operations.md)
- [Getting started](getting-started.md)
