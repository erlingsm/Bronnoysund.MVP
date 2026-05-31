// SPDX-License-Identifier: MIT

using Bronnoysund.Application;
using Bronnoysund.Application.Dtos;
using Bronnoysund.Application.Results;
using Bronnoysund.Application.UseCases.LookupCompany;
using Bronnoysund.Application.UseCases.LookupCompanyRoles;
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

    // OpenAPI for the JSON /api surface (lookup + roles); rendered by Swagger UI below.
    builder.Services.AddOpenApi();

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

    // OpenAPI document at /openapi/v1.json + Swagger UI at /swagger for the JSON /api surface.
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "Bronnoysund BlazorWeb API v1"));

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
    })
    .ExcludeFromDescription(); // internal UI plumbing, not part of the public JSON API

    // Single source of truth for the "Brreg is down" 503, shared by the JSON endpoints below.
    static IResult BrregUnavailable(string detail) => Results.Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Brønnøysundregistrene (the Brønnøysund Register Centre) is temporarily unavailable",
        detail: detail);

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
            CompanyLookupResult.Unavailable unav => BrregUnavailable(unav.Message),
            _ => Results.Problem("Unexpected result type.")
        };
    })
    .WithName("LookupCompany")
    .WithSummary("Look up a company by organisation number")
    .WithTags("Companies")
    .Produces<CompanyResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    // Roles for an entity, mirroring the WebApi project's /companies/{orgnr}/roles endpoint.
    app.MapGet("/api/companies/{orgnr}/roles", async (
        string orgnr,
        LookupCompanyRolesHandler handler,
        CancellationToken ct) =>
    {
        var result = await handler.HandleAsync(new LookupCompanyRolesQuery(orgnr), ct);
        return result switch
        {
            CompanyRolesResult.Found f => Results.Ok(f.Roles),
            // 404 = roles resource absent (Brreg 404/410, typically a deleted/unregistered
            // entity). A live entity with no roles returns 200 with an empty group list.
            CompanyRolesResult.NotFound nf => Results.NotFound(new
            {
                error = "not_found",
                message = $"No roles are registered for organization number {nf.OrganizationNumber}, or the entity does not exist."
            }),
            CompanyRolesResult.InvalidInput inv => Results.BadRequest(new
            {
                error = "invalid_input",
                message = inv.Message
            }),
            CompanyRolesResult.Unavailable unav => BrregUnavailable(unav.Message),
            _ => Results.Problem("Unexpected result type.")
        };
    })
    .WithName("LookupCompanyRoles")
    .WithSummary("List the roles registered for a company (board, CEO, auditor …), grouped by role group")
    .WithTags("Companies")
    .Produces<CompanyRolesResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

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
