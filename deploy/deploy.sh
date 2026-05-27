#!/usr/bin/env bash
# SPDX-License-Identifier: MIT
#
# Local manual deploy pipeline for Bronnoysund.MVP.
#
# Builds, tests, builds images in ACR (no local Docker daemon), and updates
# the Container Apps. Deploys BlazorWeb (UI + /api) and, if the resource has
# been provisioned, the standalone WebApi as well.
#
# Usage:
#   ./deploy/deploy.sh                  # full deploy of every provisioned app
#   ./deploy/deploy.sh --dry-run        # build + test only, no Azure changes
#   ./deploy/deploy.sh --blazorweb-only # only deploy BlazorWeb (skip WebApi check)
#
# Prerequisites:
#   - az login (active)
#   - subscription with access to bronnoysund-mvp-rg
#   - .NET 10 SDK on PATH

set -euo pipefail

readonly RG="bronnoysund-mvp-rg"
readonly ACR="bronnoysundmvp04726"
readonly ENV_DOMAIN="redpebble-469bb928.norwayeast.azurecontainerapps.io"

readonly BLAZOR_APP="bronnoysund-mvp"
readonly BLAZOR_DOCKERFILE="src/Bronnoysund.BlazorWeb/Dockerfile"
readonly BLAZOR_URL="https://${BLAZOR_APP}.${ENV_DOMAIN}"

readonly WEBAPI_APP="bronnoysund-webapi"
readonly WEBAPI_DOCKERFILE="src/Bronnoysund.WebApi/Dockerfile"
readonly WEBAPI_URL="https://${WEBAPI_APP}.${ENV_DOMAIN}"

DRY_RUN=0
DEPLOY_WEBAPI=1
for arg in "$@"; do
    case "$arg" in
        --dry-run) DRY_RUN=1 ;;
        --blazorweb-only) DEPLOY_WEBAPI=0 ;;
        *) echo "Unknown flag: $arg" >&2; exit 2 ;;
    esac
done

cd "$(dirname "$0")/.."

echo "==> 1/N Build"
dotnet build --nologo -v minimal

echo "==> 2/N Test"
dotnet test --no-build --nologo

if [[ $DRY_RUN -eq 1 ]]; then
    echo "==> dry run complete (skipped Azure steps)"
    exit 0
fi

echo "==> Verify Azure login"
if ! az account show --only-show-errors >/dev/null 2>&1; then
    echo "ERROR: 'az login' required before deploy." >&2
    exit 1
fi

TAG="$(git rev-parse --short HEAD)"

deploy_app() {
    local app="$1"
    local dockerfile="$2"
    local url="$3"
    local smoke_path="$4"

    echo "==> [${app}] ACR build (image: ${app}:${TAG})"
    az acr build \
        --registry "$ACR" \
        --image "${app}:${TAG}" \
        --image "${app}:latest" \
        --file "$dockerfile" \
        .

    echo "==> [${app}] Update Container App"
    az containerapp update \
        --name "$app" \
        --resource-group "$RG" \
        --image "${ACR}.azurecr.io/${app}:${TAG}" \
        --query "properties.latestRevisionName" \
        -o tsv

    echo "==> [${app}] Smoke test"
    sleep 10
    local code
    code="$(curl -fsS --max-time 30 -o /dev/null -w "%{http_code}" "${url}${smoke_path}" || echo "000")"
    if [[ "$code" != "200" ]]; then
        echo "[${app}] WARN: smoke test got HTTP ${code} on ${smoke_path} (revision may still be warming up)" >&2
        return 1
    fi
    echo "[${app}] Live: ${url}${smoke_path} → ${code}"
}

# BlazorWeb is the always-deployed primary app
deploy_app "$BLAZOR_APP" "$BLAZOR_DOCKERFILE" "$BLAZOR_URL" "/"

# WebApi is optional — skip gracefully if the Container App hasn't been provisioned yet
if [[ $DEPLOY_WEBAPI -eq 1 ]]; then
    if az containerapp show --name "$WEBAPI_APP" --resource-group "$RG" >/dev/null 2>&1; then
        deploy_app "$WEBAPI_APP" "$WEBAPI_DOCKERFILE" "$WEBAPI_URL" "/health"
    else
        echo "==> [${WEBAPI_APP}] not provisioned — skipping. See deploy/README.md for setup."
    fi
fi

echo "==> Done"
