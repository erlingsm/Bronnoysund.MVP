// SPDX-License-Identifier: MIT

using Bronnoysund.Application;
using Bronnoysund.Application.Results;
using Bronnoysund.Application.UseCases.LookupCompany;
using Bronnoysund.Application.UseCases.LookupCompanyRoles;
using Bronnoysund.Application.UseCases.SearchCompaniesByName;
using Bronnoysund.Infrastructure;
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
        .WriteTo.File("logs/bronnoysund-.log", rollingInterval: RollingInterval.Day));

    builder.Services.AddBronnoysundApplication();
    builder.Services.AddBronnoysundInfrastructure(builder.Configuration);
    builder.Services.AddProblemDetails();

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    // Single source of truth for the "Brreg is down" 503 — every endpoint funnels its
    // Unavailable branch through here so the title/shape can't drift between endpoints.
    static IResult BrregUnavailable(string detail) => Results.Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Brønnøysundregistrene (the Brønnøysund Register Centre) is temporarily unavailable",
        detail: detail);

    app.MapGet("/companies/{orgnr}", async (
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
    });

    app.MapGet("/companies/{orgnr}/roles", async (
        string orgnr,
        LookupCompanyRolesHandler handler,
        CancellationToken ct) =>
    {
        var result = await handler.HandleAsync(new LookupCompanyRolesQuery(orgnr), ct);
        return result switch
        {
            CompanyRolesResult.Found f => Results.Ok(f.Roles),
            // 404 here means the roles resource itself is absent (Brreg 404/410 — typically a
            // deleted or unregistered entity). A live entity with no roles returns 200 with an
            // empty group list, so the message must not imply "company found but role-less".
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
    });

    app.MapGet("/companies", async (
        string? name,
        int? size,
        SearchCompaniesByNameHandler handler,
        CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Results.BadRequest(new { error = "missing_name", message = "Query parameter 'name' is required." });
        }

        var result = await handler.HandleAsync(new SearchCompaniesByNameQuery(name, size), ct);
        return result switch
        {
            SearchCompaniesByNameResult.Found f => Results.Ok(f.Result),
            SearchCompaniesByNameResult.InvalidInput inv => Results.BadRequest(new { error = "invalid_input", message = inv.Message }),
            SearchCompaniesByNameResult.Unavailable u => BrregUnavailable(u.Message),
            _ => Results.Problem("Unexpected result type."),
        };
    });

    app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "Bronnoysund.WebApi" }));

    Log.Information("Bronnoysund.WebApi starting");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Bronnoysund.WebApi crashed during startup");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
