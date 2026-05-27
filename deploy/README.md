# Deploy

Two Container Apps deployed from this repo:

| App | Workload | URL | Notes |
| --- | --- | --- | --- |
| `bronnoysund-mvp` | BlazorWeb (UI + `/api/companies/{orgnr}`) | <https://bronnoysund-mvp.redpebble-469bb928.norwayeast.azurecontainerapps.io> | Always deployed |
| `bronnoysund-webapi` | WebApi (JSON only, `/companies/{orgnr}`, `/companies?name=`, `/health`) | <https://bronnoysund-webapi.redpebble-469bb928.norwayeast.azurecontainerapps.io> | Provision once, then auto-deployed |

Two paths to deploy:

| Path | Trigger | Use when |
| --- | --- | --- |
| **CI/CD via GitHub Actions** (`.github/workflows/build-and-deploy.yml`) | Push to `master` (after PR merge) | Default — every approved change goes live this way. |
| **Local manual** (`./deploy/deploy.sh`) | You run it from your terminal | Hot-fix, off-hour deploy, or when you want to demo the local pipeline. |

Both paths use the **same image builds** (`az acr build`) and the **same Container App update** calls.

## CI/CD via GitHub Actions

Three jobs:

```text
push to master ┬─► build-and-test ┬─► deploy-blazorweb
               │                   └─► deploy-webapi (parallel)
pull request ──┘
               └─ (deploy jobs are skipped — no `if: refs/heads/master` match)
```

**Test gate**: both deploy jobs declare `needs: build-and-test`. If a single test fails, no deploy starts.

**WebApi-job is self-skipping**: the `deploy-webapi` job runs `az containerapp show` first. If the WebApi Container App hasn't been provisioned yet (see [WebApi one-time setup](#webapi-one-time-setup) below), it emits a GitHub notice and exits successfully — you can run the workflow without breaking CI before Azure-side provisioning is done.

**Azure auth**: OpenID Connect (OIDC) via a Federated Credential on an Azure AD App Registration — *no client-secret stored in GitHub*. The workflow only needs three repo secrets:

- `AZURE_CLIENT_ID` — the App Registration's appId
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

Federated credential subject: `repo:erlingsm/Bronnoysund.MVP:ref:refs/heads/master`. Same Service Principal grants both `AcrPush` + `Contributor` on the ACR and `Contributor` on each Container App.

**Image tag**: `${{ github.sha }}` per commit. Rollback: `az containerapp update --image <ACR>/<APP>:<old-sha>`.

### How to require an approved PR before deploy

The workflow itself doesn't enforce review — that lives in GitHub branch protection. In the repo:

**Settings → Branches → Branch protection rules → `master`**

- ✓ Require a pull request before merging
- ✓ Require approvals (count = 1 or whatever fits the team)
- ✓ Require status checks to pass before merging → select `Build & test`
- ✓ Require branches to be up to date before merging

With this in place: open PR → `Build & test` runs and must pass → reviewer approves → merge → both deploy jobs fire automatically.

## WebApi one-time setup

The CI workflow assumes the WebApi Container App already exists. Provision it once with these commands (run locally with `az login` active):

```bash
APP_ID="fb15be0c-d8ee-4636-88c1-82ee0b2dcf17"   # App Registration appId
SUB_ID=$(az account show --query id -o tsv)
RG="bronnoysund-mvp-rg"

# 1. Create the WebApi Container App in the existing environment
az containerapp create \
    --name bronnoysund-webapi \
    --resource-group "$RG" \
    --environment bronnoysund-mvp-env \
    --image "mcr.microsoft.com/k8se/quickstart:latest" \
    --target-port 8080 \
    --ingress external \
    --min-replicas 1 --max-replicas 1 \
    --cpu 0.5 --memory 1.0Gi

# 2. Grant Contributor on the new Container App to the same Service Principal
WEBAPI_SCOPE="/subscriptions/$SUB_ID/resourceGroups/$RG/providers/Microsoft.App/containerApps/bronnoysund-webapi"
az role assignment create --role "Contributor" --scope "$WEBAPI_SCOPE" --assignee "$APP_ID"

# 3. Verify both RBAC entries are now there
az role assignment list --assignee "$APP_ID" --all -o table
# Expect to see: Contributor on bronnoysund-webapi (and Contributor on bronnoysund-mvp, AcrPush + Contributor on bronnoysundmvp04726)
```

After this, the next push to `master` (or `Re-run` of the latest workflow) will build the WebApi image and deploy it. No new federated credential needed — same repo, same branch, same subject.

## Local manual via `deploy.sh`

For when you can't (or don't want to) go through CI — e.g. demoing the pipeline at the presentation.

Prerequisites:

- `az` CLI installed and logged in (`az login`)
- Active subscription with access to `bronnoysund-mvp-rg`
- .NET 10 SDK on PATH
- No local Docker daemon required — image build runs in ACR

From repo root:

```bash
./deploy/deploy.sh                  # build + test + deploy both apps that exist
./deploy/deploy.sh --dry-run        # build + test only, no Azure changes
./deploy/deploy.sh --blazorweb-only # skip the WebApi check entirely
```

The script deploys BlazorWeb unconditionally and tries WebApi if the Container App is provisioned. It mirrors the workflow's per-app sequence (acr build → containerapp update → smoke test).

Failure at any step aborts (`set -euo pipefail`).

## Resources used

| Resource | Name |
| --- | --- |
| Resource group | `bronnoysund-mvp-rg` (region `norwayeast`) |
| Container Apps environment | `bronnoysund-mvp-env` |
| Container Registry | `bronnoysundmvp04726` (Basic SKU) |
| Container App (UI) | `bronnoysund-mvp` (min=1, max=1, 0.5 CPU, 1 Gi mem) |
| Container App (API) | `bronnoysund-webapi` (min=1, max=1, 0.5 CPU, 1 Gi mem) |
| Azure AD App Registration | `github-bronnoysund-mvp` (federated credential issuer for GitHub Actions) |

## Teardown

To delete the resource group and stop incurring cost (~$10–20/mo with two apps):

```bash
./deploy/teardown.sh         # prints what would happen, exits
./deploy/teardown.sh --yes   # actually deletes
```

The script issues `az group delete --no-wait`, so it returns immediately. Full deletion takes a few minutes.

Note: the Azure AD App Registration lives in your tenant, not in the resource group, and survives `az group delete`. Remove it separately if you want a fully clean slate:

```bash
az ad app delete --id <appId>
```

## Re-creating from scratch

Not scripted (one-time setup). If the resource group has been torn down:

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
az containerapp create --name bronnoysund-webapi \
    --resource-group bronnoysund-mvp-rg \
    --environment bronnoysund-mvp-env \
    --image "mcr.microsoft.com/k8se/quickstart:latest" \
    --target-port 8080 --ingress external \
    --min-replicas 1 --max-replicas 1 \
    --cpu 0.5 --memory 1.0Gi
# Re-grant RBAC (AcrPush + Contributor on the registry, Contributor on each Container App), then push to master.
```
