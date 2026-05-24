# ClientManager

ClientManager is a .NET solution for managing client access, resource allocation, rate limits, and usage telemetry. It includes a public API, a storage API, a Blazor-based administration UI, shared domain models, local persistence options, and helper scripts for seeding and generating sample traffic.

## Projects

- `ClientManager.Api` exposes the public HTTP API and Swagger documentation.
- `ClientManager.StorageApi` hosts the persistence-facing API used by the public API.
- `ClientManager.AdminUI` provides the web administration interface.
- `ClientManager.DataAccess` contains repository and storage implementations.
- `ClientManager.Shared` contains shared models, configuration, logging, and concurrency utilities.
- `ClientManager.DataAccess.Tests` contains data-access test coverage.

## Requirements

- .NET 10 SDK
- Python 3 for the helper scripts in `_scripts`

## Running Locally

Start the applications in this order:

1. Storage API: `dotnet run --project ClientManager.StorageApi`
2. Public API: `dotnet run --project ClientManager.Api`
3. Admin UI: `dotnet run --project ClientManager.AdminUI`

Default local URLs:

- Storage API: `http://localhost:5063`
- Public API: `http://localhost:5062`
- Admin UI: `http://localhost:5100`
- Swagger UI: `http://localhost:5062/docs`

To seed local data through the public API:

```powershell
python _scripts/seed_data.py --base-url http://localhost:5062
```

To generate sample traffic for the dashboard:

```powershell
python _scripts/traffic_generator.py --base-url http://localhost:5062 --interval 2.0
```

Stop the traffic generator before stopping the API hosts.

## Persistence

The local JSON file and Lucene persistence backends are intended for local or single-host use. Multi-instance or production deployments should use a centralized persistence backend configured through the solution's persistence settings.

## License

This project is licensed under the GNU General Public License v3.0. See [LICENSE](LICENSE) for details.