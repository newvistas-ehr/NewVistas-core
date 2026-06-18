#!/usr/bin/env bash
# =============================================================================
# azure-teardown.sh — Tear down NewVistas Azure resources
#
# Usage:
#   chmod +x scripts/azure-teardown.sh
#   ./scripts/azure-teardown.sh
#
# WARNING: This permanently deletes all NewVistas Azure resources including:
#   - All 3 Container Apps (silohost, webserver, blazorweb)
#   - The Container Apps Environment
#   - The Azure Container Registry and all images
#   - The Azure SQL Server and database (all data is lost)
#   - The resource group itself
# =============================================================================
set -euo pipefail

# ── Must match the variables in azure-deploy.sh ──────────────────────────────
RESOURCE_GROUP="newvistas-rg"
ACR_NAME="newvistasacr"

# ── Confirm deletion ──────────────────────────────────────────────────────────
echo ""
echo "============================================================"
echo " NewVistas Azure Teardown"
echo "============================================================"
echo ""
echo " WARNING: This will permanently delete resource group:"
echo "   '$RESOURCE_GROUP'"
echo ""
echo " All resources will be destroyed:"
echo "   - Container Apps (silohost, webserver, blazorweb)"
echo "   - Container Apps Environment"
echo "   - Azure Container Registry '$ACR_NAME' and all images"
echo "   - Azure SQL Server and database (ALL DATA WILL BE LOST)"
echo ""
read -rp "Type the resource group name to confirm deletion: " CONFIRM

if [[ "$CONFIRM" != "$RESOURCE_GROUP" ]]; then
    echo "Confirmation did not match. Aborting."
    exit 1
fi

# ── Delete the resource group (removes everything inside it) ─────────────────
echo ""
echo ">>> Deleting resource group '$RESOURCE_GROUP' and all resources..."
echo "    (This may take several minutes)"
az group delete \
    --name "$RESOURCE_GROUP" \
    --yes \
    --no-wait

echo ">>> Resource group deletion initiated (running in background)."
echo "    You can monitor progress in the Azure Portal."

# ── Purge the ACR if soft-delete is enabled ───────────────────────────────────
# ACR soft-delete retains deleted registries for recovery. Purge to avoid
# name conflicts if you want to redeploy using the same ACR name.
echo ">>> Attempting to purge ACR '$ACR_NAME' (if soft-delete was enabled)..."
az acr delete \
    --name "$ACR_NAME" \
    --yes 2>/dev/null || true

echo ""
echo "============================================================"
echo " Teardown initiated successfully."
echo ""
echo " The resource group '$RESOURCE_GROUP' is being deleted."
echo " All costs will stop accruing once deletion completes"
echo " (typically 2-5 minutes)."
echo ""
echo " To redeploy, run: ./scripts/azure-deploy.sh"
echo "============================================================"
