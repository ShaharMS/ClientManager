using System.Globalization;
using ClientManager.AdminUI.Components;
using ClientManager.AdminUI.Localization;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using ClientManager.AdminUI.Utils;
using ClientManager.Shared.Logging;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using NLog;
using NLog.Web;
using Radzen;

var logger = LogManager.Setup()
    .LoadConfigurationFromAppSettings()
    .GetCurrentClassLogger();

try
{
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.AddSingleton(typeof(IAppLogger<>), typeof(AppLogger<>));
builder.Services.AddHttpContextAccessor();

builder.Services.AddLocalization();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = SupportedCultures.Codes
        .Select(c => new CultureInfo(c))
        .ToList();
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
    options.SetDefaultCulture(SupportedCultures.Default);
    options.ApplyCurrentCultureToResponseHeaders = true;
    options.RequestCultureProviders =
    [
        new CmCultureCookieProvider(),
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider(),
    ];
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

if (builder.Environment.IsDevelopment())
{
    builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(
        options => options.DetailedErrors = true);
}

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
builder.Services.AddScoped<UrlQuerySync>();
builder.Services.AddScoped<CultureService>();
builder.Services.AddScoped<ApiErrorLocalizer>();
builder.Services.AddScoped<DeniedBreakdownFormatter>();
builder.Services.AddRadzenComponents();

var app = builder.Build();

var localizationOptions = app.Services
    .GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>()
    .Value;

LocalizationValidator.ValidateDevelopment(
    app.Services.GetRequiredService<IStringLocalizer<SharedResources>>(),
    app.Environment);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRequestLocalization(localizationOptions);
app.UseStaticFiles();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/_dev/culture", (HttpContext ctx, IStringLocalizer<SharedResources> localizer) =>
        Results.Json(new
        {
            uiCulture = CultureInfo.CurrentUICulture.Name,
            contentLanguage = ctx.Response.Headers.ContentLanguage.ToString(),
            cmCookie = ctx.Request.Cookies[CmCultureCookieProvider.CookieName],
            settingsTitle = localizer["Settings.Title"].Value,
        }));
}

app.Lifetime.ApplicationStarted.Register(() =>
{
    var appLogger = app.Services.GetRequiredService<IAppLogger<Program>>();
    var addresses = app.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>()?.Addresses;

    if (addresses is null || addresses.Count == 0)
    {
        appLogger.Info("Admin UI started");
        return;
    }

    foreach (var url in addresses)
    {
        appLogger.Info("Admin UI listening", new { Url = url });
    }
});

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
