# Azure Container Apps Deployment Guide

Deploy NewVistas to Azure so friends can test it, then tear down the environment between testing periods to avoid costs.

## Architecture

```
                        Internet
                           │
           ┌───────────────┼───────────────┐
           ▼               ▼               ▼
    ┌─────────────┐  ┌──────────┐  ┌──────────────┐
    │  BlazorWeb  │  │  WebSvr  │  │PatientPortal │
    │  (HTTPS)    │  │  (HTTPS) │  │  (HTTPS)     │
    │  Port 8080  │  │  Port    │  │  Port 8080   │
    └──────┬──────┘  │  8080    │  └──────┬───────┘
           │         └────┬─────┘         │
           │ Orleans       │ Orleans       │ Orleans
           │ Client        │ Client        │ Client
           │               │               │
           └───────────────┼───────────────┘
                           │ Gateway :30000
                  ┌────────▼────────┐
                  │    SiloHost     │
                  │  (internal)     │
                  │  Silo  :11111   │
                  │  GW    :30000   │
                  └────────┬────────┘
                           │ SQL ADO.NET
                  ┌────────▼────────┐
                  │  Azure SQL DB   │
                  │  NewVistasDB    │
                  │  (Basic tier)   │
                  └─────────────────┘

All 4 containers run in the same Azure Container Apps Environment
(shared virtual network), so internal TCP ports are reachable
without any public exposure.
```

| Container App | Project | External Access |
|---|---|---|
| **silohost** | `NewVistas.SiloHost` | Internal only (TCP 11111, 30000) |
| **webserver** | `NewVistas.WebServer` | HTTPS (port 8080) |
| **blazorweb** | `NewVistas.BlazorWeb` | HTTPS (port 8080) |
| **patientportal** | `NewVistas.PatientPortal` | HTTPS (port 8080) |

## Prerequisites

- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) installed
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running
- An active Azure subscription
- Run commands from the **repository root directory**

Log in to Azure before running the scripts:

```bash
az login
```

## Quick Start

### 1. Deploy

```bash
chmod +x scripts/azure-deploy.sh
./scripts/azure-deploy.sh
```

You will be prompted for:
- **SQL admin password** — must be at least 8 characters with uppercase, lowercase, and a digit
- **WebServer JWT signing key** — must be at least 32 characters (used to sign clinician tokens)
- **PatientPortal JWT signing key** — must be at least 32 characters (used to sign patient tokens)

Or set them as environment variables to skip the prompts:

```bash
export SQL_ADMIN_PASSWORD="YourPassword123"
export WEBSERVER_JWT_KEY="your-webserver-jwt-signing-key-32chars-min"
export PATIENTPORTAL_JWT_KEY="your-patientportal-jwt-key-32chars-min"
./scripts/azure-deploy.sh
```

The script will:
1. Create a resource group
2. Create an Azure SQL Server + Basic database (~$5/month)
3. Create an Azure Container Registry (Basic SKU)
4. Build all 4 Docker images and push to ACR
5. Create a Container Apps Environment
6. Deploy all 4 container apps with the correct configuration
7. Print the URLs when complete

### 2. Access the Application

After deployment, the script prints the URLs:

```
 Clinician UI (BlazorWeb):
   https://blazorweb.<unique-id>.eastus.azurecontainerapps.io

 Patient Portal:
   https://patientportal.<unique-id>.eastus.azurecontainerapps.io
```

> **Note:** Wait ~60 seconds after deployment for the SiloHost to start and register with the clustering table before the other apps can connect. If you see connection errors on first load, wait a minute and refresh.

## Demo Credentials

All demo users share the password **`smythVista1`**.

| Username | Role | Access |
|---|---|---|
| `drsmith` | Provider | Order entry, clinical notes, all clinical modules |
| `nurse1` | Nurse | Vitals, medication administration, notes |
| `pharm1` | Pharmacist | Pharmacy verification, controlled substances |
| `admin1` | Administrator | Patient registration, audit trail, all access |

These users are seeded automatically by the WebServer on startup.

## Teardown

When you're done testing, tear down the entire environment to stop costs:

```bash
chmod +x scripts/azure-teardown.sh
./scripts/azure-teardown.sh
```

You will be asked to confirm by typing the resource group name. All resources — SQL database, container registry, container apps, and networking — are deleted.

> **Warning:** Teardown permanently deletes all data. There is no recovery after deletion.

## Customization

Edit the variables at the top of `scripts/azure-deploy.sh` to change:

| Variable | Default | Description |
|---|---|---|
| `RESOURCE_GROUP` | `newvistas-rg` | Azure resource group name |
| `LOCATION` | `eastus` | Azure region |
| `ACR_NAME` | `newvistasacr` | Container registry name (must be globally unique) |
| `SQL_SERVER_NAME` | `newvistas-sql` | SQL server name (must be globally unique) |
| `SQL_DATABASE_NAME` | `NewVistasDB` | Database name |

## Encryption at Rest

Azure SQL Database enables **Transparent Data Encryption (TDE)** by default —
verify it is on for `NewVistasDB` (`az sql db tde show`). Grain state is stored
as Orleans binary serialization, which is **not encryption** (string fields are
readable in a hex dump); TDE plus encrypted backups is the actual control. See
SYSADMIN_GUIDE.md "Encryption at Rest" for details.

## Cost Estimate

Running 24/7 for one month (approximate, varies by region):

| Resource | Tier | Est. Monthly Cost |
|---|---|---|
| Azure SQL Database | Basic (5 DTU) | ~$5 |
| Container Apps (4 apps) | Consumption plan | ~$10–20 (active) / ~$0 (idle) |
| Azure Container Registry | Basic SKU | ~$5 |
| **Total** | | **~$20–30/month** |

Container Apps on the Consumption plan charge per vCPU-second and GB-memory-second. When no requests are being served and min-replicas=1, the baseline cost is very low. You can reduce costs further by setting `--min-replicas 0` for the web apps (they will cold-start in ~5 seconds on first request).

## Troubleshooting

### Apps can't connect to SiloHost

The SiloHost must fully start and write its entry to the `OrleansMembershipVersionTable` before clients can connect. Check SiloHost logs:

```bash
az containerapp logs show \
  --name silohost \
  --resource-group newvistas-rg \
  --tail 50
```

### WebServer fails to start

Check that the Identity database schema was created:

```bash
az containerapp logs show \
  --name webserver \
  --resource-group newvistas-rg \
  --tail 50
```

Common issues:
- SQL firewall rule not applied yet — wait 30 seconds and restart the container app
- Wrong connection string — verify the SQL server name and credentials

### Viewing logs for any container app

```bash
az containerapp logs show \
  --name <silohost|webserver|blazorweb|patientportal> \
  --resource-group newvistas-rg \
  --follow
```

### Redeploying after a code change

Rebuild and push images, then restart the container apps:

```bash
# Rebuild and push (run from repo root)
docker build -f NewVistas.BlazorWeb/Dockerfile -t newvistasacr.azurecr.io/blazorweb:latest .
docker push newvistasacr.azurecr.io/blazorweb:latest

# Restart the container app to pull the new image
az containerapp update \
  --name blazorweb \
  --resource-group newvistas-rg \
  --image newvistasacr.azurecr.io/blazorweb:latest
```
