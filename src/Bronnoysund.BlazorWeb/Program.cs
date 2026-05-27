// SPDX-License-Identifier: MIT

using Bronnoysund.Application;
using Bronnoysund.Application.Results;
using Bronnoysund.Application.UseCases.LookupCompany;
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

    // Scoped (per-circuit) so the title-click reset reaches the same VM instance the
    // Lookup page is bound to. Transient would give MainLayout a different VM than the page.
    builder.Services.AddScoped<CompanyLookupViewModel>();

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

    app.UseRequestLocalization();
    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseAntiforgery();
    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode()
        .AddAdditionalAssemblies(typeof(Bronnoysund.Components.Pages.Lookup).Assembly);

    // GET is unconventional for a cookie-setting endpoint — POST is the textbook choice.
    // Safety is preserved here by two load-bearing checks: (1) the culture parameter is
    // validated against the closed `supportedCultures` allowlist, so an attacker can only
    // toggle the user between the three supported cultures; (2) `Results.LocalRedirect`
    // rejects any off-site target. The worst a crafted link can do is flip a user's UI
    // language — no XSS, no open redirect, no privilege escalation.
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

    // JSON API endpoint exposed at /api/companies/{orgnr} so the deployed host satisfies the
    // assignment's "return a response object with the four English fields" requirement directly,
    // without a separate WebApi deployment. Same handler, same caching, same result-union
    // pattern-match the WebApi project uses — just on the BlazorWeb host.
    app.MapGet("/api/companies/{orgnr}", async (
        string orgnr,
        LookupCompanyHandler handler,
        CancellationToken ct) =>
    {
        var result = await handler.HandleAsync(new LookupCompanyQuery(orgnr), ct);
        return result switch
        {
            CompanyLookupResult.Found f => Results.Ok(f.Company),
            CompanyLookupResult.NotFound nf => Results.NotFound(new
            {
                error = "not_found",
                message = $"No company with organization number {nf.OrganizationNumber} was found."
            }),
            CompanyLookupResult.InvalidInput inv => Results.BadRequest(new
            {
                error = "invalid_input",
                message = inv.Message
            }),
            CompanyLookupResult.Unavailable unav => Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Brønnøysundregistrene (the Brønnøysund Register Centre) is temporarily unavailable",
                detail: unav.Message),
            _ => Results.Problem("Unexpected result type.")
        };
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
