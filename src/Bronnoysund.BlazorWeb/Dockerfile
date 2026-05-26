# SPDX-License-Identifier: MIT

# Multi-stage build for ASP.NET Core 10.0 — final image is Alpine for minimal size.
# Build from repo root:
#   docker build -f src/Bronnoysund.BlazorWeb/Dockerfile -t bronnoysund-mvp .

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy NuGet config + Directory.Build.props first
COPY NuGet.config .
COPY Directory.Build.props .

# Copy only csproj files first to leverage Docker layer cache
COPY src/Bronnoysund.Domain/Bronnoysund.Domain.csproj                 src/Bronnoysund.Domain/
COPY src/Bronnoysund.Application/Bronnoysund.Application.csproj       src/Bronnoysund.Application/
COPY src/Bronnoysund.Infrastructure/Bronnoysund.Infrastructure.csproj src/Bronnoysund.Infrastructure/
COPY src/Bronnoysund.ViewModels/Bronnoysund.ViewModels.csproj         src/Bronnoysund.ViewModels/
COPY src/Bronnoysund.Components/Bronnoysund.Components.csproj         src/Bronnoysund.Components/
COPY src/Bronnoysund.BlazorWeb/Bronnoysund.BlazorWeb.csproj           src/Bronnoysund.BlazorWeb/

# Copy all source first, then restore+publish in one step (more reliable for multi-project graphs)
COPY src/Bronnoysund.Domain/         src/Bronnoysund.Domain/
COPY src/Bronnoysund.Application/    src/Bronnoysund.Application/
COPY src/Bronnoysund.Infrastructure/ src/Bronnoysund.Infrastructure/
COPY src/Bronnoysund.ViewModels/     src/Bronnoysund.ViewModels/
COPY src/Bronnoysund.Components/     src/Bronnoysund.Components/
COPY src/Bronnoysund.BlazorWeb/      src/Bronnoysund.BlazorWeb/

RUN dotnet publish src/Bronnoysund.BlazorWeb/Bronnoysund.BlazorWeb.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# Slim runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

# icu-libs + tzdata so CultureInfo("en"/"nb-NO"/"nn-NO") works (Alpine ships globalization-invariant by default).
RUN apk add --no-cache icu-libs tzdata

# Do not run as root
RUN adduser -D -u 10001 appuser
WORKDIR /app
COPY --from=build --chown=appuser:appuser /app/publish .
USER appuser

# Container Apps uses port 8080 as default
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV DOTNET_PRINT_TELEMETRY_MESSAGE=false

ENTRYPOINT ["dotnet", "Bronnoysund.BlazorWeb.dll"]
