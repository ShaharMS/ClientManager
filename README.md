# ClientManager

ClientManager is a .NET-based sample application for managing clients, service configurations, resource pools, allocations, rate limits, and usage data. The solution is split into API, storage, data access, shared model, test, and administrative UI projects so each layer can be developed and hosted independently.

## Projects

- `ClientManager.AdminUI` provides the Blazor administrative interface.
- `ClientManager.Api` exposes the public application API.
- `ClientManager.StorageApi` owns persistence-facing API operations.
- `ClientManager.DataAccess` contains repository and storage abstractions.
- `ClientManager.Shared` contains shared models, configuration, logging, and concurrency helpers.
- `ClientManager.DataAccess.Tests` contains data access test coverage.

## Requirements

- .NET SDK 10.0 or later
- Python 3 for the optional helper scripts in `_scripts`

## Getting Started

Restore and build the solution:

```powershell
dotnet restore ClientManager.slnx
dotnet build ClientManager.slnx
```

For local manual testing, start the applications in this order:

1. `ClientManager.StorageApi`
2. `ClientManager.Api`
3. `ClientManager.AdminUI`

Then optionally seed demo data through the public API:

```powershell
python _scripts/seed_data.py --base-url http://localhost:5062
```

## Repository Layout

```text
ClientManager.AdminUI/       Administrative UI
ClientManager.Api/           Public API host
ClientManager.StorageApi/    Storage API host
ClientManager.DataAccess/    Persistence layer
ClientManager.Shared/        Shared contracts and utilities
_scripts/                    Local development scripts
data/                        Local development data files
```

## License

This project is licensed under the GNU General Public License v3.0. See `LICENSE` for details.