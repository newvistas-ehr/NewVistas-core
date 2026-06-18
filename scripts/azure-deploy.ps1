# =============================================================================
# azure-deploy.ps1 - Deploy NewVistas to Azure Container Apps (PowerShell)
#
# Native Windows version of azure-deploy.sh - no bash or WSL required.
#
# Usage (from the repository root):
#   powershell -ExecutionPolicy Bypass -File .\scripts\azure-deploy.ps1
#
# Prerequisites:
#   - Azure CLI (az) installed and logged in (az login)
#   - Docker Desktop installed and running
#   - Run from the repository root directory
#
# Secrets can be supplied up front via environment variables to skip the prompts:
#   $env:SQL_ADMIN_PASSWORD = '...'
#   $env:WEBSERVER_JWT_KEY  = '...'
# =============================================================================

# az/docker are native commands: PowerShell does not stop on their non-zero exit
# codes, so we check $LASTEXITCODE explicitly after each step (the set -e analog).

# --- Helpers -----------------------------------------------------------------
function Stop-OnError {
    param([string]$What)
    if ($LASTEXITCODE -ne 0) {
        Write-Host ''
        Write-Host "ERROR: $What failed (exit code $LASTEXITCODE). Aborting." -ForegroundColor Red
        exit 1
    }
}

function ConvertFrom-SecureStringPlain {
    param([System.Security.SecureString]$Secure)
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

# --- Prerequisite checks -----------------------------------------------------
foreach ($tool in 'az', 'docker') {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        Write-Host "ERROR: '$tool' is not on PATH. Install it and re-run." -ForegroundColor Red
        exit 1
    }
}

# --- Configurable variables --------------------------------------------------
$ResourceGroup       = 'newvistas-rg'
$Location            = 'eastus'
$AcrName             = 'newvistasacr'        # Must be globally unique, lowercase, alphanumeric only
$EnvironmentName     = 'newvistas-env'
$SqlServerName       = 'newvistas-sql'       # Must be globally unique
$SqlDatabaseName     = 'NewVistasDB'
$SqlAdminUser        = 'newvistasadmin'

# JWT (non-secret) config
$WebserverJwtIssuer   = 'NewVistas'
$WebserverJwtAudience = 'NewVistas'

# Container app names
$AppSilohost  = 'silohost'
$AppWebserver = 'webserver'
$AppBlazorweb = 'blazorweb'

$ImageTag = 'latest'

# --- Gather secrets (env var, else prompt) -----------------------------------
$SqlAdminPassword = $env:SQL_ADMIN_PASSWORD
if ([string]::IsNullOrEmpty($SqlAdminPassword)) {
    $secure = Read-Host 'Enter SQL admin password (min 8 chars; use upper+lower+digit+symbol)' -AsSecureString
    $SqlAdminPassword = ConvertFrom-SecureStringPlain $secure
}

$WebserverJwtKey = $env:WEBSERVER_JWT_KEY
if ([string]::IsNullOrEmpty($WebserverJwtKey)) {
    $secure = Read-Host 'Enter WebServer JWT signing key (min 32 chars)' -AsSecureString
    $WebserverJwtKey = ConvertFrom-SecureStringPlain $secure
}

# --- Validate secrets --------------------------------------------------------
if ($SqlAdminPassword.Length -lt 8) {
    Write-Host 'ERROR: SQL admin password must be at least 8 characters.' -ForegroundColor Red
    exit 1
}
if ($WebserverJwtKey.Length -lt 32) {
    Write-Host 'ERROR: WebServer JWT key must be at least 32 characters.' -ForegroundColor Red
    exit 1
}

Write-Host ''
Write-Host '============================================================'
Write-Host ' NewVistas Azure Deployment'
Write-Host " Resource Group : $ResourceGroup"
Write-Host " Location       : $Location"
Write-Host " ACR            : $AcrName"
Write-Host " SQL Server     : $SqlServerName"
Write-Host '============================================================'
Write-Host ''

# --- 1. Resource Group -------------------------------------------------------
Write-Host ">>> [1/8] Creating resource group '$ResourceGroup'..."
az group create --name $ResourceGroup --location $Location --output none
Stop-OnError 'Create resource group'

# --- 2. Azure SQL Server + Database ------------------------------------------
Write-Host ">>> [2/8] Creating Azure SQL Server '$SqlServerName'..."
az sql server create `
    --name $SqlServerName `
    --resource-group $ResourceGroup `
    --location $Location `
    --admin-user $SqlAdminUser `
    --admin-password $SqlAdminPassword `
    --output none
Stop-OnError 'Create SQL server'

Write-Host ">>> [2/8] Creating SQL Database '$SqlDatabaseName' (Basic tier)..."
az sql db create `
    --name $SqlDatabaseName `
    --server $SqlServerName `
    --resource-group $ResourceGroup `
    --edition Basic `
    --capacity 5 `
    --output none
Stop-OnError 'Create SQL database'

Write-Host '>>> [2/8] Configuring SQL firewall to allow Azure services...'
az sql server firewall-rule create `
    --name 'AllowAzureServices' `
    --server $SqlServerName `
    --resource-group $ResourceGroup `
    --start-ip-address 0.0.0.0 `
    --end-ip-address 0.0.0.0 `
    --output none
Stop-OnError 'Configure SQL firewall'

$SqlFqdn = "$SqlServerName.database.windows.net"
$OrleansConnStr = "Server=tcp:$SqlFqdn,1433;Initial Catalog=$SqlDatabaseName;Persist Security Info=False;User ID=$SqlAdminUser;Password=$SqlAdminPassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

# --- 3. Azure Container Registry ---------------------------------------------
Write-Host ">>> [3/8] Creating Azure Container Registry '$AcrName' (Basic SKU)..."
az acr create `
    --name $AcrName `
    --resource-group $ResourceGroup `
    --sku Basic `
    --admin-enabled true `
    --output none
Stop-OnError 'Create ACR'

$AcrServer = "$AcrName.azurecr.io"

# Ensure admin is active before fetching credentials
az acr update --name $AcrName --resource-group $ResourceGroup --admin-enabled true --output none
Stop-OnError 'Enable ACR admin'

$AcrUsername = (az acr credential show --name $AcrName --resource-group $ResourceGroup --query 'username' --output tsv)
Stop-OnError 'Fetch ACR username'
$AcrUsername = $AcrUsername.Trim()

$AcrPassword = (az acr credential show --name $AcrName --resource-group $ResourceGroup --query 'passwords[0].value' --output tsv)
Stop-OnError 'Fetch ACR password'
$AcrPassword = $AcrPassword.Trim()

# --- 4. Build and push Docker images -----------------------------------------
Write-Host '>>> [4/8] Building and pushing Docker images to ACR...'

# Log in to ACR so docker can push
$AcrPassword | docker login $AcrServer --username $AcrUsername --password-stdin
Stop-OnError 'docker login to ACR'

Write-Host '    Building silohost...'
docker build --no-cache -f NewVistas.SiloHost/Dockerfile -t "$AcrServer/silohost:$ImageTag" .
Stop-OnError 'docker build silohost'
Write-Host '    Pushing silohost...'
docker push "$AcrServer/silohost:$ImageTag"
Stop-OnError 'docker push silohost'

Write-Host '    Building webserver...'
docker build --no-cache -f NewVistas.WebServer/Dockerfile -t "$AcrServer/webserver:$ImageTag" .
Stop-OnError 'docker build webserver'
Write-Host '    Pushing webserver...'
docker push "$AcrServer/webserver:$ImageTag"
Stop-OnError 'docker push webserver'

Write-Host '    Building blazorweb...'
docker build --no-cache -f NewVistas.BlazorWeb/Dockerfile -t "$AcrServer/blazorweb:$ImageTag" .
Stop-OnError 'docker build blazorweb'
Write-Host '    Pushing blazorweb...'
docker push "$AcrServer/blazorweb:$ImageTag"
Stop-OnError 'docker push blazorweb'

# --- 5. Container Apps Environment -------------------------------------------
Write-Host ">>> [5/8] Creating Container Apps Environment '$EnvironmentName'..."
az containerapp env create `
    --name $EnvironmentName `
    --resource-group $ResourceGroup `
    --location $Location `
    --output none
Stop-OnError 'Create Container Apps environment'

# --- 6. Deploy SiloHost (internal only) --------------------------------------
Write-Host '>>> [6/8] Deploying SiloHost (internal, no external ingress)...'
az containerapp create `
    --name $AppSilohost `
    --resource-group $ResourceGroup `
    --environment $EnvironmentName `
    --image "$AcrServer/silohost:$ImageTag" `
    --registry-server $AcrServer `
    --registry-identity system `
    --cpu 1.0 `
    --memory 2.0Gi `
    --min-replicas 1 `
    --max-replicas 1 `
    --env-vars `
        'ASPNETCORE_ENVIRONMENT=Production' `
        "ConnectionStrings__OrleansDatabase=$OrleansConnStr" `
        'Orleans__SiloPort=11111' `
        'Orleans__GatewayPort=30000' `
    --output none
Stop-OnError 'Deploy SiloHost'

# --- 7. Deploy WebServer (external ingress on port 8080) ---------------------
Write-Host '>>> [7/8] Deploying WebServer (external ingress)...'
az containerapp create `
    --name $AppWebserver `
    --resource-group $ResourceGroup `
    --environment $EnvironmentName `
    --image "$AcrServer/webserver:$ImageTag" `
    --registry-server $AcrServer `
    --registry-identity system `
    --ingress external `
    --target-port 8080 `
    --transport http `
    --cpu 0.5 `
    --memory 1.0Gi `
    --min-replicas 1 `
    --max-replicas 2 `
    --env-vars `
        'ASPNETCORE_ENVIRONMENT=Production' `
        "ConnectionStrings__OrleansDatabase=$OrleansConnStr" `
        "ConnectionStrings__IdentityDatabase=$OrleansConnStr" `
        "Jwt__Key=$WebserverJwtKey" `
        "Jwt__Issuer=$WebserverJwtIssuer" `
        "Jwt__Audience=$WebserverJwtAudience" `
    --output none
Stop-OnError 'Deploy WebServer'

$WebserverFqdn = (az containerapp show `
    --name $AppWebserver `
    --resource-group $ResourceGroup `
    --query 'properties.configuration.ingress.fqdn' `
    --output tsv)
Stop-OnError 'Fetch WebServer FQDN'
$WebserverFqdn = $WebserverFqdn.Trim()

# --- 8. Deploy BlazorWeb (external ingress on port 8080) ---------------------
Write-Host '>>> [8/8] Deploying BlazorWeb (external ingress)...'
az containerapp create `
    --name $AppBlazorweb `
    --resource-group $ResourceGroup `
    --environment $EnvironmentName `
    --image "$AcrServer/blazorweb:$ImageTag" `
    --registry-server $AcrServer `
    --registry-identity system `
    --ingress external `
    --target-port 8080 `
    --transport http `
    --cpu 0.5 `
    --memory 1.0Gi `
    --min-replicas 1 `
    --max-replicas 2 `
    --env-vars `
        'ASPNETCORE_ENVIRONMENT=Production' `
        "ConnectionStrings__OrleansDatabase=$OrleansConnStr" `
        "ApiBaseUrl=https://$WebserverFqdn" `
    --output none
Stop-OnError 'Deploy BlazorWeb'

$BlazorwebFqdn = (az containerapp show `
    --name $AppBlazorweb `
    --resource-group $ResourceGroup `
    --query 'properties.configuration.ingress.fqdn' `
    --output tsv)
Stop-OnError 'Fetch BlazorWeb FQDN'
$BlazorwebFqdn = $BlazorwebFqdn.Trim()

# --- Done --------------------------------------------------------------------
Write-Host ''
Write-Host '============================================================'
Write-Host ' NewVistas deployment complete!'
Write-Host '============================================================'
Write-Host ''
Write-Host ' Clinician UI (BlazorWeb):'
Write-Host "   https://$BlazorwebFqdn"
Write-Host ''
Write-Host ' WebServer API:'
Write-Host "   https://$WebserverFqdn"
Write-Host ''
Write-Host ' Demo credentials (for testing only - do not use in production)'
Write-Host '   Provider      : drsmith / smythVista1'
Write-Host '   Nurse         : nurse1  / smythVista1'
Write-Host '   Pharmacist    : pharm1  / smythVista1'
Write-Host '   Administrator : admin1  / smythVista1'
Write-Host '   See AZURE_DEPLOY.md for full credentials list.'
Write-Host ''
Write-Host ' NOTE: The SiloHost needs ~60 seconds to start and register'
Write-Host ' with the clustering table before WebServer and BlazorWeb'
Write-Host ' can connect. If you see connection errors,'
Write-Host ' wait a minute and refresh.'
Write-Host ''
Write-Host ' To tear down all resources, run: .\scripts\azure-teardown.ps1'
Write-Host '============================================================'
