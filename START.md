# NewVistas — Start Guide

## Prerequisites

- .NET 10.0 SDK
- SQL Server Express (optional, for persistent storage)

## Architecture

```
NewVistas.SiloHost      Orleans silo (grains, state, streams)
NewVistas.WebServer     REST API + Identity/JWT auth (Orleans client)
NewVistas.BlazorWeb     Blazor Server frontend (calls WebServer API)
NewVistas.Wpf_UI        WPF desktop client (calls WebServer API)
NewVistas.WpfDelphiUI   WPF Delphi-style desktop client (calls WebServer API)
NewVistas.CharUI        Terminal/character-mode UI (direct Orleans grain access)
NewVistas.PatientPortal Patient-facing web portal (calls WebServer API)
```

## Quick Start (Development — In-Memory)

Open three terminals from the repo root. Start them in order:

### 1. Orleans Silo

```bash
dotnet run --project NewVistas.SiloHost
```

- Clustering: localhost (silo port 11111, gateway port 30000)
- Storage: in-memory (data lost on restart)
- Orleans Dashboard: http://localhost:8080

### 2. REST API

```bash
dotnet run --project NewVistas.WebServer
```

- HTTP: http://localhost:5298
- HTTPS: https://localhost:7127
- OpenAPI: http://localhost:5298/openapi/v1.json

### 3a. Blazor Web UI

```bash
dotnet run --project NewVistas.BlazorWeb
```

- HTTP: http://localhost:5196
- HTTPS: https://localhost:7137
- Connects to WebServer at https://localhost:7127

### 3b. WPF Desktop UI (alternative)

```bash
dotnet run --project NewVistas.Wpf_UI
```

- Connects to WebServer at https://localhost:7127

## SQL Express Mode (Persistent Data)

Uses SQL Server Express for grain storage. Data survives restarts.

### 1. Orleans Silo with SQL Express

```bash
dotnet run --project NewVistas.SiloHost -- --use-sqlexpress
```

- Connection string: `Server=DIGITALSTORM-PC\SQLEXPRESS;Database=NewVistasDB;Trusted_Connection=True;TrustServerCertificate=True;`
- Automatically creates the database and Orleans schema tables on first run
- Clustering: still localhost (single-machine dev)

### 2–3. WebServer and UI

Same as Quick Start above — no changes needed.

## Seeding Test Data

### Automatic Reference Data (Development Mode)

When the WebServer starts in Development mode, it automatically seeds:
- ICD-10-CM codes (from `icd10cm-order-2023.txt`)
- NDF Drug Formulary (classes, generics, products)
- Demo users with VistA security keys

No action needed — this happens on every startup with in-memory storage.

### ZWR Patient Data Import

The `exports/` folder contains synthetic VistA patient data in ZWR (MUMPS global export) format. Two datasets are available:

| Dataset | Path | Patients | Patient IDs |
|---------|------|----------|-------------|
| Small | `exports/Fifty/` | 50 | P1 – P50 |
| Large | `exports/FiveHundred/` | 500 | P1 – P500 |

Each dataset includes 12 ZWR files covering patients, allergies, consults, labs, orders, pharmacy, problems, radiology, surgery, TIU notes, vitals, and ADT movements.

The `ZwrImportOrchestrator` imports in two phases:
1. **Phase 1** — Patients (all other domains reference patient grain keys)
2. **Phase 2** — All clinical domains in parallel

Import is available through the WPF UI under **Tools > ZWR Import** (point it at the `exports/Fifty` or `exports/FiveHundred` folder).

### Per-Domain Demo Data (API Endpoints)

Many controllers expose a `POST demo/load` endpoint that seeds representative data for that domain. These are useful for quickly populating a single module without a full ZWR import.

Available `demo/load` endpoints:

| Endpoint | Description |
|----------|-------------|
| `api/accesscontrol/demo/load` | Security keys for demo users |
| `api/adt/demo/load` | ADT movements and ward data |
| `api/bcma/demo/load` | Barcode medication administration |
| `api/beneficiary-travel/demo/load` | Travel claims |
| `api/cmop/demo/load` | CMOP transmissions |
| `api/drg/demo/load` | DRG grouper definitions |
| `api/drugaccountability/demo/load` | Drug accountability locations |
| `api/drugfile/demo/load` | Drug file entries and orderable items |
| `api/drugformulary/demo/load` | NDF sample products |
| `api/edis/demo/load` | Emergency department visits |
| `api/incompleterecords/demo/load` | Incomplete chart records |
| `api/inpatientpharmacy/demo/load` | Inpatient pharmacy orders |
| `api/lab/demo/load` | Lab panels and results |
| `api/labedi/demo/load` | Lab EDI reference configurations |
| `api/labinstrument/demo/load` | Instrument auto-verify rules |
| `api/lexicon/demo/load` | Clinical terms (~100) |
| `api/mailman/demo/load` | MailMan messages and groups |
| `api/mpi/demo/load` | MPI correlations (10 patients) |
| `api/outpatientpharmacy/demo/load` | Outpatient prescriptions |
| `api/pce/demo/load` | Patient care encounters |
| `api/pharmacybenefits/demo/load` | Pharmacy benefit profiles |
| `api/scheduling/demo/load` | Appointments |
| `api/security/demo/load` | Patient access controls |
| `api/wardstock/demo/load` | Ward stock inventory |

Most endpoints accept a `patientId` query parameter (e.g., `?patientId=P1`).

## Build

```bash
dotnet build NewVistas.sln
```

## Tests

```bash
# All tests (unit + functional)
dotnet test NewVistas.sln

# Unit tests only (1,065 tests)
dotnet test NewVistas.UnitTests

# Functional tests only (924 tests)
dotnet test NewVistas.FunctionalTests
```

Tests use Orleans `TestCluster` with in-memory storage — no silo or database needed.

## Production (Azure)

| Component | Azure Service | Notes |
|-----------|--------------|-------|
| SiloHost | Container Apps | Stable networking for silo-to-silo communication |
| WebServer | Web Apps | Stateless, horizontally scalable |
| BlazorWeb | Web Apps | Stateless, connects to WebServer |
| Database | SQL Server / Azure SQL | Orleans clustering + grain persistence |

Production uses ADO.NET clustering — silos discover each other via the `OrleansDatabase` connection string.

## Ports Summary

| Service | HTTP | HTTPS | Notes |
|---------|------|-------|-------|
| SiloHost | — | — | Silo: 11111, Gateway: 30000 |
| Orleans Dashboard | 8080 | — | Dev only |
| WebServer | 5298 | 7127 | REST API |
| BlazorWeb | 5196 | 7137 | Web UI |
