# Plan: ClientManager — Step 12: Admin UI Foundation

> **Status**: ✅ Completed
> **Prerequisite**: [client-manager-11-startup-config.md](client-manager-11-startup-config.md)
> **Next**: [client-manager-13-admin-ui-pages.md](client-manager-13-admin-ui-pages.md)
> **Parent**: [client-manager-overview.md](client-manager-overview.md)

## TL;DR

Create the `ClientManager.AdminUI` Blazor Server project — a separate executable that provides a browser-based admin dashboard for managing clients, services, resource pools, global rate limits, and monitoring resource allocations. This step sets up the project, layout, navigation, and typed HTTP client services that call the `ClientManager.Api` REST endpoints. No pages yet — those come in step 13.

## Reference Pattern

Standard .NET 9 Blazor Web App with interactive server render mode. The admin UI is a separate project/executable in the same solution, communicating with the API exclusively via HTTP.

Project structure follows the default Blazor Web App template:
```
ClientManager.AdminUI/
  Components/
    App.razor
    Routes.razor
    _Imports.razor
    Layout/
      MainLayout.razor
      NavMenu.razor
    Pages/
  Services/
  Program.cs
  appsettings.json
```

## Steps

### 1. Create the Blazor Server project

Create a new Blazor Web App project at `ClientManager.AdminUI/ClientManager.AdminUI.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\ClientManager.Shared\ClientManager.Shared.csproj" />
  </ItemGroup>

</Project>
```

> **Note**: References `ClientManager.Shared` for shared entity types (`ClientConfiguration`, `Service`, `ResourcePool`, `GlobalRateLimit`, enums). Does NOT reference `ClientManager.Api` — the admin UI communicates with the API only via HTTP.

Add the project to `ClientManager.slnx`:
```xml
<Project Path="ClientManager.AdminUI/ClientManager.AdminUI.csproj" />
```

### 2. Create `Program.cs`

**File: `ClientManager.AdminUI/Program.cs`**

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient("ClientManagerApi", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7001");
});

builder.Services.AddScoped<ClientApiService>();
builder.Services.AddScoped<ServiceApiService>();
builder.Services.AddScoped<ResourcePoolApiService>();
builder.Services.AddScoped<GlobalRateLimitApiService>();
builder.Services.AddScoped<StatisticsApiService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
```

### 3. Create `appsettings.json`

**File: `ClientManager.AdminUI/appsettings.json`**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ApiBaseUrl": "https://localhost:7001"
}
```

**File: `ClientManager.AdminUI/appsettings.Development.json`**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ApiBaseUrl": "https://localhost:7001"
}
```

> **Note**: The `ApiBaseUrl` should match the HTTPS URL from `ClientManager.Api/Properties/launchSettings.json`. Adjust if the API uses a different port.

### 4. Create Blazor root components

**File: `ClientManager.AdminUI/Components/App.razor`**

```razor
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <base href="/" />
    <link rel="stylesheet" href="css/bootstrap/bootstrap.min.css" />
    <link rel="stylesheet" href="css/app.css" />
    <link rel="icon" type="image/png" href="favicon.png" />
    <HeadOutlet @rendermode="InteractiveServer" />
</head>
<body>
    <Routes @rendermode="InteractiveServer" />
    <script src="_framework/blazor.web.js"></script>
</body>
</html>
```

**File: `ClientManager.AdminUI/Components/Routes.razor`**

```razor
<Router AppAssembly="typeof(Program).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)" />
        <FocusOnNavigate RouteData="routeData" Selector="h1" />
    </Found>
</Router>
```

**File: `ClientManager.AdminUI/Components/_Imports.razor`**

```razor
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.JSInterop
@using ClientManager.AdminUI
@using ClientManager.AdminUI.Components
@using ClientManager.AdminUI.Services
@using ClientManager.Shared.Models.Entities
@using ClientManager.Shared.Models.Enums
```

### 5. Create the layout

**File: `ClientManager.AdminUI/Components/Layout/MainLayout.razor`**

```razor
@inherits LayoutComponentBase

<div class="d-flex vh-100">
    <NavMenu />
    <main class="flex-grow-1 p-4 overflow-auto">
        @Body
    </main>
</div>
```

**File: `ClientManager.AdminUI/Components/Layout/NavMenu.razor`**

A sidebar navigation with links to each admin section:

```razor
<nav class="bg-dark text-white p-3" style="width: 250px; min-height: 100vh;">
    <h5 class="text-white mb-4">ClientManager</h5>
    <ul class="nav flex-column">
        <li class="nav-item">
            <NavLink class="nav-link text-white" href="" Match="NavLinkMatch.All">
                Dashboard
            </NavLink>
        </li>
        <li class="nav-item">
            <NavLink class="nav-link text-white" href="clients">
                Clients
            </NavLink>
        </li>
        <li class="nav-item">
            <NavLink class="nav-link text-white" href="services">
                Services
            </NavLink>
        </li>
        <li class="nav-item">
            <NavLink class="nav-link text-white" href="resource-pools">
                Resource Pools
            </NavLink>
        </li>
        <li class="nav-item">
            <NavLink class="nav-link text-white" href="global-rate-limits">
                Global Rate Limits
            </NavLink>
        </li>
        <li class="nav-item">
            <NavLink class="nav-link text-white" href="allocations">
                Active Allocations
            </NavLink>
        </li>
    </ul>
</nav>
```

### 6. Create typed HTTP client services

Each service wraps `HttpClient` calls to the API. They use `IHttpClientFactory` for proper `HttpClient` lifecycle management.

**File: `ClientManager.AdminUI/Services/ClientApiService.cs`**

```csharp
using System.Net.Http.Json;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.AdminUI.Services;

public class ClientApiService
{
    private readonly HttpClient _httpClient;

    public ClientApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ClientManagerApi");
    }

    public async Task<List<ClientConfiguration>> GetAllAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<ClientConfiguration>>("api/clients") ?? [];
    }

    public async Task<ClientConfiguration?> GetByIdAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<ClientConfiguration>($"api/clients/{id}");
    }

    public async Task CreateAsync(ClientConfiguration config)
    {
        var response = await _httpClient.PostAsJsonAsync("api/clients", config);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync(string id, ClientConfiguration config)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/clients/{id}", config);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string id)
    {
        var response = await _httpClient.DeleteAsync($"api/clients/{id}");
        response.EnsureSuccessStatusCode();
    }
}
```

**File: `ClientManager.AdminUI/Services/ServiceApiService.cs`**

Same pattern for `Service` entities. Base route: `api/services`.

```csharp
using System.Net.Http.Json;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.AdminUI.Services;

public class ServiceApiService
{
    private readonly HttpClient _httpClient;

    public ServiceApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ClientManagerApi");
    }

    public async Task<List<Service>> GetAllAsync() { ... }
    public async Task<Service?> GetByIdAsync(string id) { ... }
    public async Task CreateAsync(Service service) { ... }
    public async Task UpdateAsync(string id, Service service) { ... }
    public async Task DeleteAsync(string id) { ... }
}
```

**File: `ClientManager.AdminUI/Services/ResourcePoolApiService.cs`**

Same pattern for `ResourcePool` entities. Base route: `api/resource-pools`.

**File: `ClientManager.AdminUI/Services/GlobalRateLimitApiService.cs`**

Same pattern for `GlobalRateLimit` entities. Base route: `api/global-rate-limits`. Add a method for filtering by target type:

```csharp
public async Task<List<GlobalRateLimit>> GetByTargetTypeAsync(GlobalRateLimitTarget targetType)
{
    return await _httpClient.GetFromJsonAsync<List<GlobalRateLimit>>(
        $"api/global-rate-limits?targetType={targetType}") ?? [];
}
```

**File: `ClientManager.AdminUI/Services/StatisticsApiService.cs`**

Calls the statistics endpoints. Defines local response types for statistics data since these types live in `ClientManager.Api` which is not referenced:

```csharp
using System.Net.Http.Json;

namespace ClientManager.AdminUI.Services;

public class StatisticsApiService
{
    private readonly HttpClient _httpClient;

    public StatisticsApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ClientManagerApi");
    }

    public async Task<SystemOverview?> GetOverviewAsync()
    {
        return await _httpClient.GetFromJsonAsync<SystemOverview>("api/statistics/overview");
    }

    public async Task<List<ResourcePoolStatistics>> GetResourcePoolStatsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<ResourcePoolStatistics>>(
            "api/statistics/resource-pools") ?? [];
    }
}

// Local response types (mirrors API responses — not shared via project reference)
public record SystemOverview(
    int TotalClients, int EnabledClients,
    int TotalServices, int EnabledServices,
    int TotalResourcePools, int ActiveAllocations);

public record ResourcePoolStatistics(
    string ResourcePoolId, string Name,
    int MaxSlots, int ActiveAllocations,
    int AvailableSlots, bool HasGlobalRateLimit);
```

### 7. Add static assets

Copy the default Blazor static assets (Bootstrap CSS, `app.css`, favicon) from the Blazor Web App template:

```
ClientManager.AdminUI/
  wwwroot/
    css/
      bootstrap/
        bootstrap.min.css
      app.css
    favicon.png
```

The `app.css` should include basic styling for the admin layout. Bootstrap handles most of the UI styling.

## Verification

- `dotnet build` succeeds for the entire solution
- `dotnet run --project ClientManager.AdminUI` starts the Blazor Server app
- The sidebar navigation renders with links to all admin sections
- `ClientApiService` can be resolved from DI
- All typed API services construct `HttpClient` with the configured base URL
- The `_Imports.razor` includes all required namespaces
- The admin UI runs on a different port than the API
