using System.Globalization;
using ClientManager.AdminUI;
using ClientManager.AdminUI.Components;
using ClientManager.AdminUI.Http;
using ClientManager.AdminUI.Localization;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using ClientManager.Shared.Logging;
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

    builder.Services.AddTransient<OutboundHttpLoggingHandler>();

    builder.Services.AddHttpClient("ClientManagerApi", client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5062");
    })
    .AddHttpMessageHandler<OutboundHttpLoggingHandler>()
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
    builder.Services.AddScoped<GlobalRateLimitApiService>();
    builder.Services.AddScoped<StatisticsApiService>();
    builder.Services.AddScoped<UserPreferencesService>();
    builder.Services.AddScoped<UrlQuerySync>();
    builder.Services.AddScoped<CultureService>();
    builder.Services.AddScoped<ApiErrorLocalizer>();
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
    app.UseMiddleware<RequestTrackingMiddleware>();
    app.UseRequestLocalization(localizationOptions);
    app.UseStaticFiles();
    app.UseAntiforgery();
    app.MapStaticAssets();

    app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
    app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));

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
        var appLogger = app.Services.GetRequiredService<IAppLogger<StartupLogger>>();
        var environment = app.Environment;

        foreach (var url in app.Urls)
        {
            appLogger.Info("User interface bound to address", new { Url = url });
        }

        if (string.IsNullOrWhiteSpace(environment.EnvironmentName))
        {
            appLogger.Warn("Failed to detect hosting environment, falling back to default", new { Environment = Environments.Production });
        }
        else
        {
            appLogger.Info("Hosting environment detected successfully", new { Environment = environment.EnvironmentName });
        }

        appLogger.Info("Serving content from root path", new { ContentRoot = environment.ContentRootPath });
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
