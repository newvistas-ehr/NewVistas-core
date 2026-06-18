# =============================================================================
# azure-teardown.ps1 — Tear down NewVistas Azure resources (PowerShell)
#
# Native Windows version of azure-teardown.sh — no bash or WSL required.
#
# Usage (Windows PowerShell or PowerShell 7):
#   ./scripts/azure-teardown.ps1
#
# WARNING: This permanently deletes all NewVistas Azure resources including:
#   - All 3 Container Apps (silohost, webserver, blazorweb)
#   - The Container Apps Environment
#   - The Azure Container Registry and all images
#   - The Azure SQL Server and database (all data is lost)
#   - The resource group itself
# =============================================================================
# az is a native command: we check $LASTEXITCODE explicitly rather than rely on
# $ErrorActionPreference, which does not trip on native non-zero exit codes (and,
# when set to 'Stop', turns a native command's redirected stderr into a fatal error).

# ── Must match the variables in azure-deploy.ps1 / azure-deploy.sh ────────────
$ResourceGroup = 'newvistas-rg'
$AcrName       = 'newvistasacr'

# ── Confirm deletion ──────────────────────────────────────────────────────────
Write-Host ''
Write-Host '============================================================'
Write-Host ' NewVistas Azure Teardown'
Write-Host '============================================================'
Write-Host ''
Write-Host ' WARNING: This will permanently delete resource group:'
Write-Host "   '$ResourceGroup'"
Write-Host ''
Write-Host ' All resources will be destroyed:'
Write-Host '   - Container Apps (silohost, webserver, blazorweb)'
Write-Host '   - Container Apps Environment'
Write-Host "   - Azure Container Registry '$AcrName' and all images"
Write-Host '   - Azure SQL Server and database (ALL DATA WILL BE LOST)'
Write-Host ''
$confirm = Read-Host 'Type the resource group name to confirm deletion'

if ($confirm -ne $ResourceGroup) {
    Write-Host 'Confirmation did not match. Aborting.'
    exit 1
}

# ── Delete the resource group (removes everything inside it) ──────────────────
Write-Host ''
Write-Host ">>> Deleting resource group '$ResourceGroup' and all resources..."
Write-Host '    (This may take several minutes)'
az group delete --name $ResourceGroup --yes --no-wait
if ($LASTEXITCODE -ne 0) {
    Write-Host 'ERROR: Failed to initiate resource group deletion.'
    exit 1
}

Write-Host '>>> Resource group deletion initiated (running in background).'
Write-Host '    You can monitor progress in the Azure Portal.'

# ── Purge the ACR if soft-delete is enabled ───────────────────────────────────
# ACR soft-delete retains deleted registries for recovery. Purge to avoid
# name conflicts if you want to redeploy using the same ACR name.
# Best-effort: ignore errors (no-op if the registry does not exist).
Write-Host ">>> Attempting to purge ACR '$AcrName' (if soft-delete was enabled)..."
# Best-effort: the registry usually does not exist yet (a clean no-op). Suppress
# all output and any error so the script always reaches the summary below.
$prevEAP = $ErrorActionPreference
$ErrorActionPreference = 'SilentlyContinue'
try { az acr delete --name $AcrName --yes 2>&1 | Out-Null } catch { }
$ErrorActionPreference = $prevEAP
$global:LASTEXITCODE = 0

Write-Host ''
Write-Host '============================================================'
Write-Host ' Teardown initiated successfully.'
Write-Host ''
Write-Host " The resource group '$ResourceGroup' is being deleted."
Write-Host ' All costs will stop accruing once deletion completes'
Write-Host ' (typically 2-5 minutes).'
Write-Host ''
Write-Host ' To redeploy, run: ./scripts/azure-deploy.ps1'
Write-Host '============================================================'
