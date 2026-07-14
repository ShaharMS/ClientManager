# seed_data.py

Seeds the public ClientManager API with demo **catalog** data (services, global rate limits, clients).

For instance copy / migration, prefer the seed API — see [Seed system](../core/seed-system.md) (`GET` / `POST` / `PUT` `/api/v2/seed`).

## Prerequisites

- `ClientManager.Api` running (default `http://localhost:5062`)

## What it creates

| Resource | Count (default catalog) |
| --- | --- |
| Services | 6 |
| Client configurations | 5 |
| Global rate limits | One per service |

Catalog definitions live in `_scripts/configuration.py`.

## Usage

```powershell
python _scripts/seed_data.py
```

| Flag | Default | Description |
| --- | --- | --- |
| `--base-url` | `http://localhost:5062` | Public API base URL |

## Related

- [traffic_generator.py](traffic-generator.md) — live access-check traffic on top of seeded data
- [access_load_check.py](access-load-check.md) — sustained 18k-RPM access-check load test
