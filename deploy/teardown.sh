#!/usr/bin/env bash
# SPDX-License-Identifier: MIT
#
# Tear down the Azure resource group for Bronnoysund.Lookup.MVP.
# Destroys the Container App, ACR, and managed environment.
#
# Usage:
#   ./deploy/teardown.sh --yes   # confirms intent, performs deletion
#   ./deploy/teardown.sh         # prints what would happen, exits

set -euo pipefail

readonly RG="bronnoysund-mvp-rg"

if [[ "${1:-}" != "--yes" ]]; then
    echo "This will permanently delete resource group '${RG}' and everything in it."
    echo "Re-run with --yes to confirm:"
    echo "    ./deploy/teardown.sh --yes"
    exit 1
fi

if ! az account show --only-show-errors >/dev/null 2>&1; then
    echo "ERROR: 'az login' required before teardown." >&2
    exit 1
fi

echo "==> Deleting resource group ${RG} (async)"
az group delete --name "$RG" --yes --no-wait
echo "Deletion queued. Check status with:"
echo "    az group show --name ${RG} 2>&1 | head -3"
