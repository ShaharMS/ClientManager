using ClientManager.AdminUI.Components;
using ClientManager.AdminUI.Services;
using ClientManager.Shared.Logging;
using NLog;
using NLog.Web;
using Radzen;

var logger = LogManager.Setup()
    .LoadConfigurationFromAppSettings()
    .GetCurrentClassLogger();

try
{
var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.AddSingleton(typeof(IAppLogger<>), typeof(AppLogger<>));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient("ClientManagerApi", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5062");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    if (builder.Environment.IsDevelopment())
    {
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    return handler;
});

builder.Services.AddScoped<ClientApiService>();
builder.Services.AddScoped<ServiceApiService>();
builder.Services.AddScoped<ResourcePoolApiService>();
builder.Services.AddScoped<GlobalRateLimitApiService>();
builder.Services.AddScoped<StatisticsApiService>();
builder.Services.AddSingleton<EntityColorService>();
builder.Services.AddScoped<UserPreferencesService>();
builder.Services.AddRadzenComponents();

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
}
catch (Exception exception)
{
    logger.Error(exception, "Stopped AdminUI because of exception");
    throw;
}
finally
{
    LogManager.Shutdown();
}
