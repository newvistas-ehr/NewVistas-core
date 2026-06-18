# =============================================================================
# azure-deploy-with-portal.ps1 - Deploy NewVistas WITH the Patient Portal.
#
# Thin convenience wrapper. Identical to running:
#   powershell -ExecutionPolicy Bypass -File .\scripts\azure-deploy.ps1 -IncludePatientPortal
#
# All the real logic lives in azure-deploy.ps1 (single source of truth); this
# just flips the -IncludePatientPortal switch and forwards any other arguments.
#
# Usage (from the repository root):
#   powershell -ExecutionPolicy Bypass -File .\scripts\azure-deploy-with-portal.ps1
# =============================================================================
& "$PSScriptRoot\azure-deploy.ps1" -IncludePatientPortal @args
exit $LASTEXITCODE
