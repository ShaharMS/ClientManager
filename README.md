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

Download the container images the repository uses for local development and for
production packaging and self-contained project distribution:

```powershell
python _scripts/download_images.py --download-dependencies
python _scripts/download_images.py --build-projects --build-version 1.0.1-alpha
```

Use `--list` to preview the exact pulls and builds without running Docker. The
`--download-dependencies` action downloads the external images used by the
project and exports them into `_scripts/.downloaded_images/`. The
`--build-projects` action builds flattened, version-tagged ClientManager images
so they can be published as standalone project images. Use `--build-version`
to control the project image tag and tar naming, `--dependency-image-override`
to point external dependency images at alternate registries, and
`--package-source` to use alternate NuGet feeds during the Dockerized restore.

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