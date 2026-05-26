#!/usr/bin/env bash
# SPDX-License-Identifier: MIT
#
# Local manual deploy pipeline for Bronnoysund.Lookup.MVP.
#
# Builds, tests, builds an image in ACR (no local Docker daemon),
# updates the Container App, and smoke-tests the public URL.
#
# Usage:
#   ./deploy/deploy.sh           # full deploy
#   ./deploy/deploy.sh --dry-run # build + test only, no Azure changes
#
# Prerequisites:
#   - az login (active)
#   - subscription with access to bronnoysund-mvp-rg
#   - .NET 10 SDK on PATH

set -euo pipefail

readonly RG="bronnoysund-mvp-rg"
readonly ACR="bronnoysundmvp04726"
readonly APP="bronnoysund-mvp"
readonly URL="https://${APP}.redpebble-469bb928.norwayeast.azurecontainerapps.io"
readonly DOCKERFILE="src/Bronnoysund.Lookup.BlazorWeb/Dockerfile"

DRY_RUN=0
if [[ "${1:-}" == "--dry-run" ]]; then
    DRY_RUN=1
fi

cd "$(dirname "$0")/.."

echo "==> 1/5 Build"
dotnet build --nologo -v minimal

echo "==> 2/5 Test"
dotnet test --no-build --nologo

if [[ $DRY_RUN -eq 1 ]]; then
    echo "==> dry run complete (skipped Azure steps)"
    exit 0
fi

echo "==> 3/5 Verify Azure login"
if ! az account show --only-show-errors >/dev/null 2>&1; then
    echo "ERROR: 'az login' required before deploy." >&2
    exit 1
fi

TAG="$(git rev-parse --short HEAD)"
echo "==> 4/5 ACR build (image: ${APP}:${TAG})"
az acr build \
    --registry "$ACR" \
    --image "${APP}:${TAG}" \
    --file "$DOCKERFILE" \
    .

echo "==> 5/5 Update Container App"
az containerapp update \
    --name "$APP" \
    --resource-group "$RG" \
    --image "${ACR}.azurecr.io/${APP}:${TAG}" \
    --query "properties.latestRevisionName" \
    -o tsv

echo "==> Smoke test"
sleep 10
HTTP_CODE="$(curl -fsS --max-time 30 -o /dev/null -w "%{http_code}" "$URL/" || echo "000")"
if [[ "$HTTP_CODE" == "200" ]]; then
    echo "Live: ${URL}/ → ${HTTP_CODE}"
else
    echo "WARN: smoke test got HTTP ${HTTP_CODE} (revision may still be warming up)" >&2
    exit 1
fi
