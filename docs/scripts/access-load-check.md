# access_load_check.py

Sustained load test against `GET /api/v1/access/check` to verify rate-limit storage keeps up at high RPM.

## Defaults

| Setting | Value |
| --- | --- |
| Target RPM | 18,000 |
| Duration | 60 seconds |
| Concurrency | 64 |
| Client / service | `platform-core` → `cache-service` (from seeded catalog) |

## Usage

```powershell
# Seed catalog first
python _scripts/seed_data.py

# Run load (API must be running)
python _scripts/access_load_check.py
```

The script fails if achieved RPM falls below 90% of the target. Expect many `429` responses when limits are tight; the goal is throughput and latency, not all grants.

## Related

- [seed_data.py](seed-data.md)
- [performance_baseline.py](performance-baseline.md)
