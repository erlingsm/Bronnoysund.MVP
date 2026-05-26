// SPDX-License-Identifier: MIT

using Bronnoysund.Application;
using Bronnoysund.BlazorWeb.Components;
using Bronnoysund.Infrastructure;
using Bronnoysund.ViewModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using MudBlazor.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/bronnoysund-web-.log", rollingInterval: RollingInterval.Day));

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddLocalization();
    var supportedCultures = new[] { "en", "nb-NO", "nn-NO" };
    builder.Services.Configure<RequestLocalizationOptions>(opts =>
    {
        opts.SetDefaultCulture("en")
            .AddSupportedCultures(supportedCultures)
            .AddSupportedUICultures(supportedCultures);
    });

    builder.Services.AddMudServices();

    builder.Services.AddBronnoysundApplication();
    builder.Services.AddBronnoysundInfrastructure(builder.Configuration);

    builder.Services.AddTransient<CompanyLookupViewModel>();

    var app = builder.Build();

    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });

    app.UseSerilogRequestLogging();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseRequestLocalization();
    app.UseAntiforgery();
    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode()
        .AddAdditionalAssemblies(typeof(Bronnoysund.Components.Pages.Lookup).Assembly);

    app.MapGet("/set-culture", (string culture, string redirectUri, HttpContext ctx) =>
    {
        if (Array.IndexOf(supportedCultures, culture) < 0)
        {
            return Results.BadRequest("Unsupported culture.");
        }

        ctx.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });
        return Results.LocalRedirect(redirectUri);
    });

    Log.Information("Bronnoysund.BlazorWeb starting on {Urls}", string.Join(", ", app.Urls));
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Bronnoysund.BlazorWeb crashed during startup");
}
finally
{
    Log.CloseAndFlush();
}
