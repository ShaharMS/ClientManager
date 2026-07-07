# Redis tuning on airgapped Kubernetes

Use this checklist when statistics endpoints are slow but persistence must stay on Redis for all roles.

## Diagnose during a slow dashboard refresh

```bash
# Slow commands (look for KEYS, SCAN, large HGETALL)
redis-cli SLOWLOG GET 20

# Memory pressure / eviction
redis-cli INFO memory

# Persistence stall (AOF fsync on slow disks)
redis-cli CONFIG GET appendfsync
redis-cli INFO persistence
```

| Symptom | Likely cause | Action |
|---------|--------------|--------|
| `KEYS` / long `SCAN` in SLOWLOG | Usage overlay full keyspace scan | Deploy app build with usage counter index (see `RedisDocumentStore`) |
| Spikes every second | AOF `appendfsync always` | Prefer `everysec` if durability policy allows |
| `used_memory` near `maxmemory` | Eviction thrashing | Raise memory limit or tune `maxmemory-policy` |
| High `instantaneous_ops_per_sec` + latency | CPU throttling on Redis pod | Raise CPU requests/limits |
| API pod cross-AZ to Redis | Network RTT stacks on N+1 reads | Co-locate API and Redis; use cluster DNS in same namespace |

## API ↔ Redis connectivity

- Set `Persistence__DefaultRedis__Host` to the Kubernetes Service name (hostname only, port in `Port`).
- Avoid routing Redis traffic through ingress or corporate egress proxies.
- Verify latency: `kubectl exec -it <api-pod> -- sh -c "time wget -qO- http://redis:6379 2>&1 | head -1"` (expect fast TCP; Redis speaks RESP not HTTP).

## Compare ingress vs pod directly

```bash
kubectl port-forward svc/clientmanager-admin-ui 5100:5100
```

If `GET /` is fast via port-forward but slow through ingress, fix ingress/DNS (see `admin-ui-ingress.example.yaml`), not Redis.
