# Kubernetes (airgapped)

Guides for running ClientManager on restricted / airgapped clusters where all persistence roles stay on Redis.

| Guide | Purpose |
| --- | --- |
| [Admin UI ingress example](admin-ui-ingress.example.yaml) | WebSocket upgrade, sticky sessions, probes for Blazor Server |
| [Redis tuning](redis-tuning-airgapped.md) | SLOWLOG, memory, AOF, and API↔Redis latency during slow dashboard loads |

## Slow load triage

1. **~30s `GET /` HTML** — compare ingress vs `kubectl port-forward` to the UI pod. AdminUI does not call Redis for the document; fix ingress/DNS/pod saturation first.
2. **WebSocket `/_blazor` stuck on 101** — apply ingress annotations from the example manifest; confirm frames in DevTools.
3. **~30s skeletons / charts stuck** — API statistics + Redis; use Redis tuning checklist and API logs (`counter_get_by_prefix`, `RequestTrackingMiddleware` slow requests).

See also [Development and operations — Logging](../development-and-operations.md#logging) for correlating browser TTFB with pod logs.
