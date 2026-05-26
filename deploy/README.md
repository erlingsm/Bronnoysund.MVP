# Deploy

Two paths to Azure Container Apps:

| Path | Trigger | Use when |
| --- | --- | --- |
| **CI/CD via GitHub Actions** (`.github/workflows/build-and-deploy.yml`) | Push to `master` (after PR merge) | Default — every approved change goes live this way. |
| **Local manual** (`./deploy/deploy.sh`) | You run it from your terminal | Hot-fix, off-hour deploy, or when you want to demo the local pipeline. |

Both paths use the **same image build** (`az acr build`) and the **same Container App update**. The only difference is who triggers them and where the Azure auth comes from.

## CI/CD via GitHub Actions

`.github/workflows/build-and-deploy.yml` runs on every push to `master`, every pull-request, and on manual `workflow_dispatch`.

```text
push to master ─┬─► build-and-test ─► deploy
                │       │
pull request ───┤       └─ (deploy job is skipped — no `if: refs/heads/master` match)
                │
workflow_dispatch ─► build-and-test ─► deploy
```

**Test gate**: the `deploy` job declares `needs: build-and-test`. If a single test fails, the deploy job never starts.

**Azure auth**: OpenID Connect (OIDC) via a Federated Credential on an Azure AD App Registration — *no client-secret stored in GitHub*. The workflow only needs three repo variables (not secrets):

- `AZURE_CLIENT_ID` — the App Registration's appId
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

The App Registration's federated credential trusts OIDC tokens with subject `repo:erlingsm/Bronnoysund.MVP:ref:refs/heads/master`, and the service principal has `AcrPush` on the registry and `Contributor` on the Container App resource only — nothing wider.

**Image tag**: `${{ github.sha }}` per commit, so every deploy is traceable and rollback is `az containerapp update --image <ACR>/<APP>:<old-sha>`.

### How to require an approved PR before deploy

The workflow itself doesn't enforce review — that lives in GitHub branch protection. In the repo:

**Settings → Branches → Branch protection rules → `master`**

- ✓ Require a pull request before merging
- ✓ Require approvals (count = 1 or whatever fits the team)
- ✓ Require status checks to pass before merging → select `build-and-test`
- ✓ Require branches to be up to date before merging

With this in place: open PR → `build-and-test` runs and must pass → reviewer approves → merge → deploy fires automatically.

## Local manual via `deploy.sh`

For when you can't (or don't want to) go through CI — e.g. demoing the pipeline at the presentation.

Prerequisites:

- `az` CLI installed and logged in (`az login`)
- Active subscription with access to `bronnoysund-mvp-rg`
- .NET 10 SDK on PATH
- No local Docker daemon required — image build runs in ACR

From repo root:

```bash
./deploy/deploy.sh            # full pipeline (~3–4 min)
./deploy/deploy.sh --dry-run  # build + test only, no Azure changes
```

Steps the script runs (mirrors the GitHub Actions workflow):

1. `dotnet build`
2. `dotnet test`
3. Check `az account show`
4. `az acr build` — pushes image tagged with the current git short hash
5. `az containerapp update` — points the app at the new image
6. `curl` smoke test against the public URL

Failure at any step aborts (`set -euo pipefail`).

## Resources used

| Resource | Name |
| --- | --- |
| Resource group | `bronnoysund-mvp-rg` (region `norwayeast`) |
| Container Apps environment | `bronnoysund-mvp-env` |
| Container Registry | `bronnoysundmvp04726` (Basic SKU) |
| Container App | `bronnoysund-mvp` (min=1, max=1, 0.5 CPU, 1 Gi mem) |
| Azure AD App Registration | `github-bronnoysund-mvp` (federated credential issuer for GitHub Actions) |
| Public URL | <https://bronnoysund-mvp.redpebble-469bb928.norwayeast.azurecontainerapps.io> |

## Teardown

To delete the resource group and stop incurring cost (~$5–10/mo):

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
# Re-grant RBAC to the existing App Registration on the new resources, then push to master.
```
