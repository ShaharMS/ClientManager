# Plan: ClientManager — Step 0: Project Rename

> **Status**: ✅ Completed
> **Prerequisite**: None — this is the first step.
> **Next**: [client-manager-1-foundation.md](client-manager-1-foundation.md)
> **Parent**: [client-manager-overview.md](client-manager-overview.md)

## TL;DR

Rename the `ClientManager` API project to `ClientManager.Api` to establish a clear multi-project naming convention. The solution will contain `ClientManager.Api` (REST API), `ClientManager.DataAccess` (abstractions and database implementations), `ClientManager.Shared` (entity models and enums), and `ClientManager.AdminUI` (Blazor Server admin dashboard). This is done first while the project is still just the default ASP.NET Core scaffold, so no application code needs updating.

## Reference Pattern

Standard .NET multi-project solution naming: `{SolutionName}.{ProjectRole}` — e.g. `ClientManager.Api`, `ClientManager.DataAccess`, `ClientManager.Shared`, `ClientManager.AdminUI`.

## Steps

### 1. Rename the project folder

Rename the folder `ClientManager/` to `ClientManager.Api/`.

```powershell
Rename-Item -Path "ClientManager" -NewName "ClientManager.Api"
```

### 2. Rename the `.csproj` file

Rename `ClientManager.Api/ClientManager.csproj` to `ClientManager.Api/ClientManager.Api.csproj`.

```powershell
Rename-Item -Path "ClientManager.Api/ClientManager.csproj" -NewName "ClientManager.Api.csproj"
```

### 3. Update the solution file

**File: `ClientManager.slnx`**

Update the project reference path from `ClientManager/ClientManager.csproj` to `ClientManager.Api/ClientManager.Api.csproj`.

### 4. Update `launchSettings.json`

**File: `ClientManager.Api/Properties/launchSettings.json`**

Update profile names that reference `ClientManager` to `ClientManager.Api`. The `applicationUrl` values stay the same.

### 5. Rename the `.http` file

Rename `ClientManager.Api/ClientManager.http` to `ClientManager.Api/ClientManager.Api.http`. Update the host address variable name if it references `ClientManager_HostAddress`.

### 6. Delete the `.csproj.user` file

Delete `ClientManager.Api/ClientManager.csproj.user` — it references the old project name and will be regenerated automatically.

## Verification

- `dotnet build` succeeds from the solution root
- The project appears as `ClientManager.Api` in the solution
- `dotnet run --project ClientManager.Api` starts the application
- The WeatherForecast endpoint still responds at the same URL
