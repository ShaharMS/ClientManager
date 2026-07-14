# performance_baseline.py

Runs a deterministic load profile against the public ClientManager API and reports latency and response statuses.

## Prerequisites

- `ClientManager.Api` running
- Catalog seeded via [seed_data.py](seed-data.md)

## Usage

```powershell
python _scripts/performance_baseline.py
```

Defaults approximate 1M requests/day over a configurable duration. The script mixes access checks and dashboard overview reads.

### Common flags

See `python _scripts/performance_baseline.py --help` for the full list. Key options are defined in `configuration.py` under `scripts.performance_baseline`:

- Request rate / duration
- Virtual client count

## Output

Reports request counts, status counts, achieved rate, and latency percentiles.

## Related

- [traffic_generator.py](traffic-generator.md) — non-deterministic demo traffic
- [Observability guides](../observability/index.md)
