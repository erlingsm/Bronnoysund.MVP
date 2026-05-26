// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

using Bronnoysund.Lookup.Application;
using Bronnoysund.Lookup.Application.Results;
using Bronnoysund.Lookup.Application.UseCases.LookupCompany;
using Bronnoysund.Lookup.Application.UseCases.SearchCompaniesByName;
using Bronnoysund.Lookup.Infrastructure;
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
        .WriteTo.File("logs/bronnoysund-lookup-.log", rollingInterval: RollingInterval.Day));

    builder.Services.AddBronnoysundApplication();
    builder.Services.AddBronnoysundInfrastructure(builder.Configuration);
    builder.Services.AddProblemDetails();

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages();

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
            CompanyLookupResult.Unavailable unav => Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Brønnøysundregistrene (the Brønnøysund Register Centre) is temporarily unavailable",
                detail: unav.Message),
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
            SearchCompaniesByNameResult.Unavailable u => Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Brønnøysundregistrene (the Brønnøysund Register Centre) is temporarily unavailable",
                detail: u.Message),
            _ => Results.Problem("Unexpected result type."),
        };
    });

    app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "Bronnoysund.Lookup.WebApi" }));

    Log.Information("Bronnoysund.Lookup.WebApi starting");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Bronnoysund.Lookup.WebApi crashed during startup");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
