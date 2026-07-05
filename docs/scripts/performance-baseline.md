# performance_baseline.py

Runs a deterministic load profile against the public ClientManager API and reports latency plus JSON storage sizes for before/after comparisons.

## Prerequisites

- `ClientManager.Api` running
- Catalog seeded via [seed_data.py](seed-data.md)

## Usage

```powershell
python _scripts/performance_baseline.py
```

Defaults approximate 1M requests/day over a configurable duration. The script mixes access checks, acquire/release, and statistics reads (including graph-range queries).

### Common flags

See `python _scripts/performance_baseline.py --help` for the full list. Key options are defined in `configuration.py` under `scripts.performance_baseline`:

- Request rate / duration
- Virtual client count
- Graph range presets for dashboard/monitor endpoints

## Output

Reports request counts, error rate, latency percentiles, and on-disk sizes for `UsageSnapshots.json` and `_counters.json` under the API data directory.

## Related

- [traffic_generator.py](traffic-generator.md) — non-deterministic demo traffic
- [Metrics integration guide](../metrics-integration-guide.md)
