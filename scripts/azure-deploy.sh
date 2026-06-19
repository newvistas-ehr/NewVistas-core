#!/usr/bin/env bash
# =============================================================================
# azure-deploy.sh — Deploy NewVistas to Azure Container Apps
#
# Usage:
#   chmod +x scripts/azure-deploy.sh
#   ./scripts/azure-deploy.sh                 # clinician stack (SiloHost, WebServer, BlazorWeb)
#   ./scripts/azure-deploy.sh --with-portal   # also deploy the Patient Portal
#   ./scripts/azure-deploy.sh --silo-replicas=3              # run 3 Orleans silos (HA / scale)
#
# Prerequisites:
#   - Azure CLI (az) installed and logged in (az login)
#   - Docker installed and running
#   - Run from the repository root directory
# =============================================================================
set -euo pipefail

# ── Optional components ───────────────────────────────────────────────────────
# Deploy the Patient Portal alongside the clinician stack. Enable with the
# --with-portal flag, or by setting INCLUDE_PATIENT_PORTAL=true.
INCLUDE_PATIENT_PORTAL="${INCLUDE_PATIENT_PORTAL:-false}"
# SiloHost replica count (Orleans silos). 1 = demo; 3+ = HA / higher throughput.
SILO_REPLICAS="${SILO_REPLICAS:-1}"
for arg in "$@"; do
    case "$arg" in
        --with-portal)     INCLUDE_PATIENT_PORTAL="true" ;;
        --silo-replicas=*) SILO_REPLICAS="${arg#*=}" ;;
    esac
done

# ── Configurable variables ────────────────────────────────────────────────────
RESOURCE_GROUP="newvistas-rg"
LOCATION="${LOCATION:-eastus2}"       # Azure region; override with: export LOCATION=centralus (etc.) if a region is at capacity
ACR_NAME="newvistasacr"               # Must be globally unique, lowercase, alphanumeric only
ENVIRONMENT_NAME="newvistas-env"
SQL_SERVER_NAME="newvistas-sql"       # Must be globally unique
SQL_DATABASE_NAME="NewVistasDB"
SQL_ADMIN_USER="newvistasadmin"
SQL_ADMIN_PASSWORD="${SQL_ADMIN_PASSWORD:-}"  # Set via env var or prompted below

# JWT secrets — override via environment variables before running
WEBSERVER_JWT_KEY="${WEBSERVER_JWT_KEY:-}"
WEBSERVER_JWT_ISSUER="NewVistas"
WEBSERVER_JWT_AUDIENCE="NewVistas"

# Patient Portal JWT (only used when --with-portal is set)
PATIENTPORTAL_JWT_KEY="${PATIENTPORTAL_JWT_KEY:-}"
PATIENTPORTAL_JWT_ISSUER="NewVistas-PatientPortal"
PATIENTPORTAL_JWT_AUDIENCE="NewVistas-PatientPortal"

# Container app names
APP_SILOHOST="silohost"
APP_WEBSERVER="webserver"
APP_BLAZORWEB="blazorweb"
APP_PATIENTPORTAL="patientportal"

# ── Pre-flight summary ────────────────────────────────────────────────────────
echo ""
echo "============================================================"
echo " NewVistas — Azure deployment"
echo "============================================================"
echo ""
echo " This will create, in resource group '$RESOURCE_GROUP' (region $LOCATION):"
echo "   - An Azure SQL Database (Basic tier)"
echo "   - An Azure Container Registry"
echo "   - A Container Apps environment and the application containers"
echo ""
echo " It builds and pushes Docker images, so the first run takes"
echo " roughly 10-15 minutes. Running it continuously costs about"
echo " 20-30 USD/month until you remove it with scripts/azure-teardown.sh."
echo ""
echo " Before continuing, make sure:"
echo "   - You are logged in to Azure   (az login)"
echo "   - Docker Desktop is running"
echo "   - You are in the repository root"
echo ""
echo " You will be asked for:"
echo "   - A SQL admin password (>= 8 chars, include a symbol; avoid ; and \")"
echo "   - A WebServer JWT signing key (>= 32 chars)"
if [[ "$INCLUDE_PATIENT_PORTAL" == "true" ]]; then
echo "   - A Patient Portal JWT signing key (>= 32 chars)"
fi
echo "============================================================"
echo ""
# Pause for confirmation when interactive. Set NEWVISTAS_ASSUME_YES=1 to skip.
if [[ -z "${NEWVISTAS_ASSUME_YES:-}" ]] && [ -t 0 ]; then
    read -rp " Press Enter to begin, or Ctrl+C to cancel... " _
    echo ""
fi

# ── Prompt for secrets if not set ─────────────────────────────────────────────
if [[ -z "$SQL_ADMIN_PASSWORD" ]]; then
    read -rsp "Enter SQL admin password (min 8 chars, upper+lower+digit): " SQL_ADMIN_PASSWORD
    echo
fi

if [[ -z "$WEBSERVER_JWT_KEY" ]]; then
    read -rsp "Enter WebServer JWT signing key (min 32 chars): " WEBSERVER_JWT_KEY
    echo
fi

if [[ "$INCLUDE_PATIENT_PORTAL" == "true" && -z "$PATIENTPORTAL_JWT_KEY" ]]; then
    read -rsp "Enter PatientPortal JWT signing key (min 32 chars): " PATIENTPORTAL_JWT_KEY
    echo
fi

# ── Validate secrets ──────────────────────────────────────────────────────────
if [[ ${#SQL_ADMIN_PASSWORD} -lt 8 ]]; then
    echo "ERROR: SQL admin password must be at least 8 characters." >&2
    exit 1
fi
if [[ ${#WEBSERVER_JWT_KEY} -lt 32 ]]; then
    echo "ERROR: WebServer JWT key must be at least 32 characters." >&2
    exit 1
fi
if [[ "$INCLUDE_PATIENT_PORTAL" == "true" && ${#PATIENTPORTAL_JWT_KEY} -lt 32 ]]; then
    echo "ERROR: PatientPortal JWT key must be at least 32 characters." >&2
    exit 1
fi

echo ""
echo "============================================================"
echo " NewVistas Azure Deployment"
echo " Resource Group : $RESOURCE_GROUP"
echo " Location       : $LOCATION"
echo " ACR            : $ACR_NAME"
echo " SQL Server     : $SQL_SERVER_NAME"
echo " Silo replicas  : $SILO_REPLICAS"
echo " Patient Portal : $INCLUDE_PATIENT_PORTAL"
echo "============================================================"
echo ""

# ── 1. Resource Group ─────────────────────────────────────────────────────────
echo ">>> [1/8] Creating resource group '$RESOURCE_GROUP'..."
az group create \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --output none

# ── 2. Azure SQL Server + Database ───────────────────────────────────────────
echo ">>> [2/8] Creating Azure SQL Server '$SQL_SERVER_NAME'..."
az sql server create \
    --name "$SQL_SERVER_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --admin-user "$SQL_ADMIN_USER" \
    --admin-password "$SQL_ADMIN_PASSWORD" \
    --output none

echo ">>> [2/8] Creating SQL Database '$SQL_DATABASE_NAME' (Basic tier)..."
az sql db create \
    --name "$SQL_DATABASE_NAME" \
    --server "$SQL_SERVER_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --edition Basic \
    --capacity 5 \
    --output none

echo ">>> [2/8] Configuring SQL firewall to allow Azure services..."
az sql server firewall-rule create \
    --name "AllowAzureServices" \
    --server "$SQL_SERVER_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --start-ip-address 0.0.0.0 \
    --end-ip-address 0.0.0.0 \
    --output none

SQL_FQDN="${SQL_SERVER_NAME}.database.windows.net"
ORLEANS_CONN_STR="Server=tcp:${SQL_FQDN},1433;Initial Catalog=${SQL_DATABASE_NAME};Persist Security Info=False;User ID=${SQL_ADMIN_USER};Password=${SQL_ADMIN_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

# ── 3. Azure Container Registry ───────────────────────────────────────────────
echo ">>> [3/8] Creating Azure Container Registry '$ACR_NAME' (Basic SKU)..."
az acr create \
    --name "$ACR_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --sku Basic \
    --admin-enabled true \
    --output none

ACR_SERVER="${ACR_NAME}.azurecr.io"

# Ensure admin is active before fetching credentials
az acr update \
    --name "$ACR_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --admin-enabled true \
    --output none

ACR_USERNAME=$(az acr credential show \
    --name "$ACR_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query "username" \
    --output tsv)
ACR_PASSWORD=$(az acr credential show \
    --name "$ACR_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query "passwords[0].value" \
    --output tsv)

# ── 4. Build and push Docker images ──────────────────────────────────────────
echo ">>> [4/8] Building and pushing Docker images to ACR..."

# Log in to ACR so docker can push
echo "$ACR_PASSWORD" | docker login "$ACR_SERVER" \
    --username "$ACR_USERNAME" \
    --password-stdin

IMAGE_TAG="latest"

echo "    Building silohost..."
docker build --no-cache \
    -f NewVistas.SiloHost/Dockerfile \
    -t "${ACR_SERVER}/silohost:${IMAGE_TAG}" \
    .

echo "    Pushing silohost..."
docker push "${ACR_SERVER}/silohost:${IMAGE_TAG}"

echo "    Building webserver..."
docker build --no-cache \
    -f NewVistas.WebServer/Dockerfile \
    -t "${ACR_SERVER}/webserver:${IMAGE_TAG}" \
    .

echo "    Pushing webserver..."
docker push "${ACR_SERVER}/webserver:${IMAGE_TAG}"

echo "    Building blazorweb..."
docker build --no-cache \
    -f NewVistas.BlazorWeb/Dockerfile \
    -t "${ACR_SERVER}/blazorweb:${IMAGE_TAG}" \
    .

echo "    Pushing blazorweb..."
docker push "${ACR_SERVER}/blazorweb:${IMAGE_TAG}"

if [[ "$INCLUDE_PATIENT_PORTAL" == "true" ]]; then
    echo "    Building patientportal..."
    docker build --no-cache \
        -f NewVistas.PatientPortal/Dockerfile \
        -t "${ACR_SERVER}/patientportal:${IMAGE_TAG}" \
        .

    echo "    Pushing patientportal..."
    docker push "${ACR_SERVER}/patientportal:${IMAGE_TAG}"
fi

# ── 5. Container Apps Environment ─────────────────────────────────────────────
echo ">>> [5/8] Creating Container Apps Environment '$ENVIRONMENT_NAME'..."
az containerapp env create \
    --name "$ENVIRONMENT_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --output none

# ── 6. Deploy SiloHost (internal only) ────────────────────────────────────────
echo ">>> [6/8] Deploying SiloHost (internal, no external ingress)..."
az containerapp create \
    --name "$APP_SILOHOST" \
    --resource-group "$RESOURCE_GROUP" \
    --environment "$ENVIRONMENT_NAME" \
    --image "${ACR_SERVER}/silohost:${IMAGE_TAG}" \
    --registry-server "$ACR_SERVER" \
    --registry-username "$ACR_USERNAME" \
    --registry-password "$ACR_PASSWORD" \
    --cpu 1.0 \
    --memory 2.0Gi \
    --min-replicas "$SILO_REPLICAS" \
    --max-replicas "$SILO_REPLICAS" \
    --env-vars \
        "ASPNETCORE_ENVIRONMENT=Production" \
        "ConnectionStrings__OrleansDatabase=${ORLEANS_CONN_STR}" \
        "Orleans__SiloPort=11111" \
        "Orleans__GatewayPort=30000" \
    --output none

# ── 7. Deploy WebServer (external ingress on port 8080) ───────────────────────
echo ">>> [7/8] Deploying WebServer (external ingress)..."
az containerapp create \
    --name "$APP_WEBSERVER" \
    --resource-group "$RESOURCE_GROUP" \
    --environment "$ENVIRONMENT_NAME" \
    --image "${ACR_SERVER}/webserver:${IMAGE_TAG}" \
    --registry-server "$ACR_SERVER" \
    --registry-username "$ACR_USERNAME" \
    --registry-password "$ACR_PASSWORD" \
    --ingress external \
    --target-port 8080 \
    --transport http \
    --cpu 0.5 \
    --memory 1.0Gi \
    --min-replicas 1 \
    --max-replicas 2 \
    --env-vars \
        "ASPNETCORE_ENVIRONMENT=Production" \
        "ConnectionStrings__OrleansDatabase=${ORLEANS_CONN_STR}" \
        "ConnectionStrings__IdentityDatabase=${ORLEANS_CONN_STR}" \
        "Jwt__Key=${WEBSERVER_JWT_KEY}" \
        "Jwt__Issuer=${WEBSERVER_JWT_ISSUER}" \
        "Jwt__Audience=${WEBSERVER_JWT_AUDIENCE}" \
    --output none

WEBSERVER_FQDN=$(az containerapp show \
    --name "$APP_WEBSERVER" \
    --resource-group "$RESOURCE_GROUP" \
    --query "properties.configuration.ingress.fqdn" \
    --output tsv)

# ── 8. Deploy BlazorWeb (external ingress on port 8080) ───────────────────────
echo ">>> [8/8] Deploying BlazorWeb (external ingress)..."
az containerapp create \
    --name "$APP_BLAZORWEB" \
    --resource-group "$RESOURCE_GROUP" \
    --environment "$ENVIRONMENT_NAME" \
    --image "${ACR_SERVER}/blazorweb:${IMAGE_TAG}" \
    --registry-server "$ACR_SERVER" \
    --registry-username "$ACR_USERNAME" \
    --registry-password "$ACR_PASSWORD" \
    --ingress external \
    --target-port 8080 \
    --transport http \
    --cpu 0.5 \
    --memory 1.0Gi \
    --min-replicas 1 \
    --max-replicas 2 \
    --env-vars \
        "ASPNETCORE_ENVIRONMENT=Production" \
        "ConnectionStrings__OrleansDatabase=${ORLEANS_CONN_STR}" \
        "ApiBaseUrl=https://${WEBSERVER_FQDN}" \
    --output none

BLAZORWEB_FQDN=$(az containerapp show \
    --name "$APP_BLAZORWEB" \
    --resource-group "$RESOURCE_GROUP" \
    --query "properties.configuration.ingress.fqdn" \
    --output tsv)

# ── 9. Deploy PatientPortal (optional, external ingress on port 8080) ─────────
PATIENTPORTAL_FQDN=""
if [[ "$INCLUDE_PATIENT_PORTAL" == "true" ]]; then
    echo ">>> [+] Deploying PatientPortal (optional, external ingress)..."
    az containerapp create \
        --name "$APP_PATIENTPORTAL" \
        --resource-group "$RESOURCE_GROUP" \
        --environment "$ENVIRONMENT_NAME" \
        --image "${ACR_SERVER}/patientportal:${IMAGE_TAG}" \
        --registry-server "$ACR_SERVER" \
        --registry-username "$ACR_USERNAME" \
        --registry-password "$ACR_PASSWORD" \
        --ingress external \
        --target-port 8080 \
        --transport http \
        --cpu 0.5 \
        --memory 1.0Gi \
        --min-replicas 1 \
        --max-replicas 2 \
        --env-vars \
            "ASPNETCORE_ENVIRONMENT=Production" \
            "ConnectionStrings__OrleansDatabase=${ORLEANS_CONN_STR}" \
            "Jwt__Key=${PATIENTPORTAL_JWT_KEY}" \
            "Jwt__Issuer=${PATIENTPORTAL_JWT_ISSUER}" \
            "Jwt__Audience=${PATIENTPORTAL_JWT_AUDIENCE}" \
        --output none

    PATIENTPORTAL_FQDN=$(az containerapp show \
        --name "$APP_PATIENTPORTAL" \
        --resource-group "$RESOURCE_GROUP" \
        --query "properties.configuration.ingress.fqdn" \
        --output tsv)
fi

# ── Done ──────────────────────────────────────────────────────────────────────
echo ""
echo "============================================================"
echo " NewVistas deployment complete!"
echo "============================================================"
echo ""
echo " Clinician UI (BlazorWeb):"
echo "   https://${BLAZORWEB_FQDN}"
echo ""
if [[ "$INCLUDE_PATIENT_PORTAL" == "true" ]]; then
echo " Patient Portal:"
echo "   https://${PATIENTPORTAL_FQDN}"
echo ""
fi
echo " WebServer API:"
echo "   https://${WEBSERVER_FQDN}"
echo ""
echo " Demo credentials (for testing only — do not use in production)"
echo "   Provider      : drsmith / smythVista1"
echo "   Nurse         : nurse1  / smythVista1"
echo "   Pharmacist    : pharm1  / smythVista1"
echo "   Administrator : admin1  / smythVista1"
echo "   See AZURE_DEPLOY.md for full credentials list."
echo ""
echo " NOTE: The SiloHost needs ~60 seconds to start and register"
echo " with the clustering table before WebServer and BlazorWeb"
echo " can connect. If you see connection errors,"
echo " wait a minute and refresh."
echo ""
echo " To tear down all resources, run: ./scripts/azure-teardown.sh"
echo "============================================================"
