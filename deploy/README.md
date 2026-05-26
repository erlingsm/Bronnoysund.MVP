# Deploy scripts

Local manual deploy pipeline for the Azure Container Apps demo. No CI,
no GitHub Actions — run these scripts from your terminal.

## Prerequisites

- `az` CLI installed and logged in (`az login`)
- Active subscription with access to `bronnoysund-mvp-rg`
- .NET 10 SDK on PATH

No local Docker daemon required — image builds run in ACR.

## Resources used

| Resource | Name |
| --- | --- |
| Resource group | `bronnoysund-mvp-rg` (region `norwayeast`) |
| Container Apps environment | `bronnoysund-mvp-env` |
| Container Registry | `bronnoysundmvp04726` (Basic SKU) |
| Container App | `bronnoysund-mvp` (min=1, max=1, 0.5 CPU, 1 Gi mem) |
| Public URL | <https://bronnoysund-mvp.redpebble-469bb928.norwayeast.azurecontainerapps.io> |

## Deploy

From repo root:

```bash
./deploy/deploy.sh            # full pipeline (~3–4 min)
./deploy/deploy.sh --dry-run  # build + test only, no Azure changes
```

Steps the script runs:

1. `dotnet build`
2. `dotnet test`
3. Check `az account show`
4. `az acr build` — pushes image tagged with the current git short hash
5. `az containerapp update` — points the app at the new image
6. `curl` smoke test against the public URL

Failure at any step aborts the deploy (`set -euo pipefail`).

## Teardown

To delete the resource group and stop incurring cost (~$5–10/mo):

```bash
./deploy/teardown.sh         # prints what would happen, exits
./deploy/teardown.sh --yes   # actually deletes
```

The script issues `az group delete --no-wait`, so it returns immediately.
Full deletion takes a few minutes.

## Re-creating from scratch

Not scripted (one-time setup, see hand-off doc for parameters). If the
resource group has been torn down, manually:

```bash
az group create --name bronnoysund-mvp-rg --location norwayeast
az acr create --resource-group bronnoysund-mvp-rg \
    --name bronnoysundmvp04726 --sku Basic --admin-enabled true
az containerapp env create --name bronnoysund-mvp-env \
    --resource-group bronnoysund-mvp-rg --location norwayeast
az containerapp create --name bronnoysund-mvp \
    --resource-group bronnoysund-mvp-rg \
    --environment bronnoysund-mvp-env \
    --image "mcr.microsoft.com/k8se/quickstart:latest" \
    --target-port 8080 --ingress external \
    --min-replicas 1 --max-replicas 1 \
    --cpu 0.5 --memory 1.0Gi
# Then run ./deploy/deploy.sh to push the real image.
```
