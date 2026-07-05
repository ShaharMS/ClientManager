# traffic_generator.py

Generates semi-random live traffic against the public ClientManager API for dashboard and monitor demos.

## Prerequisites

- `ClientManager.Api` running
- Catalog seeded (typically via [seed_data.py](seed-data.md))

## What it simulates

- Access checks (most common)
- Resource acquire / release cycles
- Occasional statistics and list reads
- Varying request rates per client
- Some intentionally failing requests

Press **Ctrl+C** to stop. Stop the generator before shutting down the API so buffered usage can flush.

## Usage

```powershell
python _scripts/traffic_generator.py
```

### Flags

| Flag | Default | Description |
| --- | --- | --- |
| `--base-url` | `http://localhost:5062` | Public API base URL |
| `--interval` | 2.0 | Average seconds between request bursts |

### Example

```powershell
python _scripts/traffic_generator.py --base-url http://localhost:5062 --interval 1.5
```

## Configuration

Client weights, action probabilities, and burst sizes are in `configuration.py` under `scripts.traffic_generator`. Client and service IDs must match the seeded catalog.

## Related

- [seed_data.py](seed-data.md) — prerequisite catalog
