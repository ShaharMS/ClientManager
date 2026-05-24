# .NET 10 Upgrade Plan

## Goal

Upgrade the ClientManager solution from .NET 9 to .NET 10, refresh package references to concrete compatible versions, and add a mock project file that inventories all package dependencies and versions used across the solution.

## Local Hypothesis

The upgrade is primarily controlled by the six project files. Changing each `TargetFramework` from `net9.0` to `net10.0` and replacing wildcard package versions with current resolved versions should be enough for restore/build to expose any remaining incompatibilities.

## Cheap Disconfirming Checks

- `dotnet restore ClientManager.slnx` after project edits.
- `dotnet build ClientManager.slnx --no-restore` after restore succeeds.
- Run the available data access test executable/project if build succeeds.
- Run each host in local order: StorageApi, Api, AdminUI.
- Run seed and traffic scripts against the public API.
- Check the Admin UI in a browser after all hosts are running.

## Scope

- `ClientManager.Shared/ClientManager.Shared.csproj`
- `ClientManager.DataAccess/ClientManager.DataAccess.csproj`
- `ClientManager.DataAccess.Tests/ClientManager.DataAccess.Tests.csproj`
- `ClientManager.Api/ClientManager.Api.csproj`
- `ClientManager.StorageApi/ClientManager.StorageApi.csproj`
- `ClientManager.AdminUI/ClientManager.AdminUI.csproj`
- New dependency inventory mock csproj at the repository root.

## Notes

- Prefer concrete package versions over wildcard ranges for repeatable restore behavior.
- Keep API and UI project boundaries unchanged.
- Validate with solution restore/build first, then narrower runtime/test checks as needed.
- Treat `dotnet run` failures and browser/runtime startup failures as blockers, even when `dotnet build` succeeds.
