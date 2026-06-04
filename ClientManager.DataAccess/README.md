# ClientManager.DataAccess

## Topology Notes

- `ClientManager.Api` is the only host that should reference this project.
- Local startup order is `Api` (`http://localhost:5062`) -> `AdminUI` (`http://localhost:5100`).
- `JsonFile` and `Lucene` persistence backends are intended for local or single-host use only.
- Do not treat file-backed storage as a supported multi-instance production topology.
- Shared or production deployments should configure a centralized persistence backend via the `Persistence` role bindings.
- If the local catalogs restart empty, reseed through the public API with `python _scripts/seed_data.py --base-url http://localhost:5062`.
