# SPDX-License-Identifier: MIT

# Multi-stage build for the WebApi host (JSON-only, no UI).
# Build from repo root:
#   docker build -f src/Bronnoysund.WebApi/Dockerfile -t bronnoysund-webapi .

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY NuGet.config .
COPY Directory.Build.props .

# Restore-only layer: copy csproj graph for the WebApi transitive closure
COPY src/Bronnoysund.Domain/Bronnoysund.Domain.csproj                 src/Bronnoysund.Domain/
COPY src/Bronnoysund.Application/Bronnoysund.Application.csproj       src/Bronnoysund.Application/
COPY src/Bronnoysund.Infrastructure/Bronnoysund.Infrastructure.csproj src/Bronnoysund.Infrastructure/
COPY src/Bronnoysund.WebApi/Bronnoysund.WebApi.csproj                 src/Bronnoysund.WebApi/

# Full source
COPY src/Bronnoysund.Domain/         src/Bronnoysund.Domain/
COPY src/Bronnoysund.Application/    src/Bronnoysund.Application/
COPY src/Bronnoysund.Infrastructure/ src/Bronnoysund.Infrastructure/
COPY src/Bronnoysund.WebApi/         src/Bronnoysund.WebApi/

RUN dotnet publish src/Bronnoysund.WebApi/Bronnoysund.WebApi.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

# icu-libs + tzdata so culture-sensitive code paths work on Alpine
RUN apk add --no-cache icu-libs tzdata

RUN adduser -D -u 10001 appuser
WORKDIR /app
COPY --from=build --chown=appuser:appuser /app/publish .
USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV DOTNET_PRINT_TELEMETRY_MESSAGE=false

ENTRYPOINT ["dotnet", "Bronnoysund.WebApi.dll"]
