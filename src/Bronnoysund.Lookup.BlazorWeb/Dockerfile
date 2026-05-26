# SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

# Multi-stage build for ASP.NET Core 10.0 — final image is Alpine for minimal size.
# Build from repo root:
#   docker build -f src/Bronnoysund.Lookup.BlazorWeb/Dockerfile -t bronnoysund-mvp .

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy NuGet config + Directory.Build.props first
COPY NuGet.config .
COPY Directory.Build.props .

# Copy only csproj files first to leverage Docker layer cache
COPY src/Bronnoysund.Lookup.Domain/Bronnoysund.Lookup.Domain.csproj                 src/Bronnoysund.Lookup.Domain/
COPY src/Bronnoysund.Lookup.Application/Bronnoysund.Lookup.Application.csproj       src/Bronnoysund.Lookup.Application/
COPY src/Bronnoysund.Lookup.Infrastructure/Bronnoysund.Lookup.Infrastructure.csproj src/Bronnoysund.Lookup.Infrastructure/
COPY src/Bronnoysund.Lookup.ViewModels/Bronnoysund.Lookup.ViewModels.csproj         src/Bronnoysund.Lookup.ViewModels/
COPY src/Bronnoysund.Lookup.Components/Bronnoysund.Lookup.Components.csproj         src/Bronnoysund.Lookup.Components/
COPY src/Bronnoysund.Lookup.BlazorWeb/Bronnoysund.Lookup.BlazorWeb.csproj           src/Bronnoysund.Lookup.BlazorWeb/

# Copy all source first, then restore+publish in one step (more reliable for multi-project graphs)
COPY src/Bronnoysund.Lookup.Domain/         src/Bronnoysund.Lookup.Domain/
COPY src/Bronnoysund.Lookup.Application/    src/Bronnoysund.Lookup.Application/
COPY src/Bronnoysund.Lookup.Infrastructure/ src/Bronnoysund.Lookup.Infrastructure/
COPY src/Bronnoysund.Lookup.ViewModels/     src/Bronnoysund.Lookup.ViewModels/
COPY src/Bronnoysund.Lookup.Components/     src/Bronnoysund.Lookup.Components/
COPY src/Bronnoysund.Lookup.BlazorWeb/      src/Bronnoysund.Lookup.BlazorWeb/

RUN dotnet publish src/Bronnoysund.Lookup.BlazorWeb/Bronnoysund.Lookup.BlazorWeb.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# Slim runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

# Do not run as root
RUN adduser -D -u 10001 appuser
WORKDIR /app
COPY --from=build --chown=appuser:appuser /app/publish .
USER appuser

# Container Apps uses port 8080 as default
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_PRINT_TELEMETRY_MESSAGE=false

ENTRYPOINT ["dotnet", "Bronnoysund.Lookup.BlazorWeb.dll"]
