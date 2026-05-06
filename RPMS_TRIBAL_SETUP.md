# NewVistas — RPMS / Tribal Developer Setup

This guide is for developers at an Indian Health Service (IHS) tribal nation who want to clone, run, and exercise the **RPMS-flavored** build of NewVistas — the deployment shape used by tribal health authorities. It covers environment setup, the IHS-specific site profile, the seeded tribal demo data, the human test scripts you should follow to validate the system, and how to swap in a VistA-only module instead of an RPMS module (or vice versa).

> See also: [START.md](START.md) (project overview and quick start), [SETUP-DEVELOPMENT-ENVIRONMENT.md](SETUP-DEVELOPMENT-ENVIRONMENT.md) (general dev environment setup), [SYSADMIN_GUIDE.md](SYSADMIN_GUIDE.md) (operations).

---

## 1. Prerequisites

| Requirement | Version / Notes |
|---|---|
| Windows 10/11 (recommended) | Required for the WPF UIs ([NewVistas.Wpf_UI](NewVistas.Wpf_UI/), [NewVistas.WpfDelphiUI](NewVistas.WpfDelphiUI/)). The Blazor + API + Silo run cross-platform. |
| .NET SDK | **10.0** — the whole solution is `net10.0` (`net10.0-windows` for the WPF projects). |
| SQL Server Express (or LocalDB) | Required for the `ihs-tribal` profile. Recommended: SQL Server 2022 Express — the profile uses the `SqlExpress` connection string and ADO.NET grain storage. |
| Git | For the FOIA RPMS comparison data already vendored under [NewVistas.Abstractions/RPMS/FOIA-RPMS-master/](NewVistas.Abstractions/). |
| Visual Studio 2026 / Rider / VS Code | Any IDE that understands the [NewVistas.sln](NewVistas.sln). |

---

## 2. One-Time Machine Setup

Per [SETUP-DEVELOPMENT-ENVIRONMENT.md](SETUP-DEVELOPMENT-ENVIRONMENT.md), each developer creates their own `appsettings.Development.json` files (they're git-ignored). For the RPMS / tribal flavor you must define the `SqlExpress` connection string — the `IhsTribalSiteProfile` requires it.

### 2.1 NewVistas.SiloHost\appsettings.Development.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Orleans": "Warning"
    }
  },
  "ConnectionStrings": {
    "SqlExpress": "Server=.\\SQLEXPRESS;Database=NewVistasTribal;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true",
    "OrleansDatabase": ""
  },
  "Federation": {
    "LocalClusterId": "TRIBAL-HUB",
    "IcnPrefix": "910"
  }
}
```

The `Federation:LocalClusterId` and `Federation:IcnPrefix` lines are tribal-specific. Per-clinic spokes pick a different prefix from the 9xx allocation block (e.g. `911`, `912`, `913`) — see the IHS profile's class-level docs at [IhsTribalSiteProfile.cs:76-78](NewVistas.SiloHost/Infrastructure/Profiles/IhsTribalSiteProfile.cs#L76-L78).

### 2.2 NewVistas.WebServer\appsettings.Development.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Orleans": "Information"
    }
  },
  "ConnectionStrings": {
    "OrleansDatabase": "Server=.\\SQLEXPRESS;Database=NewVistasTribalOrleans;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true",
    "SqlExpress": "Server=.\\SQLEXPRESS;Database=NewVistasTribal;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
}
```

### 2.3 Build

```powershell
dotnet build
```

---

## 3. Selecting the RPMS / Tribal Site Profile

NewVistas supports several deployment "flavors" — one of them is `ihs-tribal`. Selection happens at silo startup and is resolved by [SiteProfileResolver.Resolve()](NewVistas.SiloHost/Infrastructure/Profiles/SiteProfileResolver.cs#L26-L52). Resolution order (first match wins):

1. `--profile=<name>` CLI argument
2. `NEWVISTAS_PROFILE` environment variable
3. Legacy `--use-sqlexpress` → `SqlExpressDemoProfile` (back-compat)
4. `IHostEnvironment.IsDevelopment()` → `LocalhostDevProfile`
5. Otherwise → `AzureCloudProfile`

Valid profile names are exactly: `localhost-dev`, `sql-express-demo`, `azure-cloud`, `remote-online`, `remote-offline`, **`ihs-tribal`**. An unknown name throws at startup ([SiteProfileResolver.cs:81-83](NewVistas.SiloHost/Infrastructure/Profiles/SiteProfileResolver.cs#L81-L83)) — misconfiguration fails loudly.

### 3.1 What `ihs-tribal` Pre-Wires

[IhsTribalSiteProfile.cs:34-115](NewVistas.SiloHost/Infrastructure/Profiles/IhsTribalSiteProfile.cs#L34-L115) registers, on top of the common silo services:

- **`IhsTribalEligibilityPolicy`** as the `IRegistrationEligibilityPolicy` — applies 38 CFR Part 136 IHS Beneficiary Eligibility rules at registration. This is what stamps a patient with `PrimaryEligibilityCode = "IHS CHS"` or `"IHS DIRECT"`.
- **`OutboxMpiFederationAnnouncer`** — peer tribal-authority clinics receive `patient-registered` and `patient-merged` MPI announcements through the same SQL outbox that clinical events use.
- **Federation outbox + HTTP transport (mTLS-capable)** — for hub/spoke replication between tribal clinics.
- **A pre-enabled feature set** ([IhsTribalSiteProfile.cs:45-57](NewVistas.SiloHost/Infrastructure/Profiles/IhsTribalSiteProfile.cs#L45-L57)) seeded by [FeatureFlagSeeder.cs](NewVistas.SiloHost/Infrastructure/Profiles/FeatureFlagSeeder.cs) on first startup:

  ```
  PATIENT_MERGE
  IMMUNIZATION_FORECAST
  EXTERNAL_REFERRAL_TRACKING       ← prerequisite for CHS authorization
  APPOINTMENT_WAITLIST
  PATIENT_RECALL
  AUTO_REFILL
  ENCOUNTER_FORM_TEMPLATES
  GPRA_REPORTING
  ICARE_DASHBOARD
  DIABETES_REGISTRY
  ```

Operators can still toggle any of these at runtime via the Site Parameters API or page (see §7) — the profile only sets the initial state.

---

## 4. Starting the Stack (RPMS / Tribal Mode)

Open three terminals from the repo root.

### 4.1 Silo Host — RPMS profile

```powershell
dotnet run --project NewVistas.SiloHost -- --profile=ihs-tribal
```

Or via env var:

```powershell
$env:NEWVISTAS_PROFILE = "ihs-tribal"
dotnet run --project NewVistas.SiloHost
```

On first startup the silo will:
1. Create the `NewVistasTribal` database tables for ADO.NET grain storage.
2. Start the federation outbox + drainer.
3. Run `FeatureFlagSeeder` — you'll see one log line per feature: `Site feature pre-enabled by profile: DIABETES_REGISTRY.` etc.
4. Open the Orleans Dashboard at <http://localhost:8080>.

### 4.2 Web Server (REST API)

```powershell
dotnet run --project NewVistas.WebServer
```

Default URL: <https://localhost:7127> with Swagger at `/swagger`.

### 4.3 UI — Pick Your Frontend

The "RPMS-flavored" surfaces are exposed in the **WpfDelphiUI** client — the CPRS-styled WPF chart was extended with the IHS-specific panels (Diabetes Registry on the Cover Sheet, CHS authorization bar on Consults, GPRA on Reports). Confirmed at [WpfDelphiUI/ViewModels/CoverSheetViewModel.cs:10-21](NewVistas.WpfDelphiUI/ViewModels/CoverSheetViewModel.cs#L10-L21) and [WpfDelphiUI/ViewModels/ConsultsViewModel.cs:15-18](NewVistas.WpfDelphiUI/ViewModels/ConsultsViewModel.cs#L15-L18).

| Surface | Project | When to use |
|---|---|---|
| **WpfDelphiUI** (CPRS clone, RPMS-flavored) | [NewVistas.WpfDelphiUI](NewVistas.WpfDelphiUI/) | Day-to-day clinical work in a tribal clinic. Has the Diabetes Registry / CHS / GPRA panels. |
| Wpf_UI (generic / non-IHS WPF) | [NewVistas.Wpf_UI](NewVistas.Wpf_UI/) | Generic CPRS-style chart without IHS extensions. |
| BlazorWeb | [NewVistas.BlazorWeb](NewVistas.BlazorWeb/) | Web access, admin pages, ops dashboards. |
| CharUI | [NewVistas.CharUI](NewVistas.CharUI/) | Roll-and-scroll terminal UI (List Manager / VA FileMan style). |

```powershell
# RPMS-flavored chart
dotnet run --project NewVistas.WpfDelphiUI

# Or Blazor (also fine — admin pages live here)
dotnet run --project NewVistas.BlazorWeb
```

---

## 5. Logins (All Profiles)

User accounts are **seeded by `NewVistas.WebServer` on every startup**. Full reference: [Demo Users & Login Reference](NewVistas.BlazorWeb/UserManual/admin/demo-users.md). The shared password is **`smythVista1`** for every seeded user.

### 5.1 The Logins You'll Use Most for RPMS Testing

| Username | Password | Role | Why you need them for RPMS workflows |
|---|---|---|---|
| `ADMIN1` | `smythVista1` | Administrator + ChiefOfStaff + PrivacyOfficer. Holds `CanRegisterPatients` and **`CanAuthorizeChs`**. | Loads the tribal demo data, approves/denies CHS authorizations, runs the GPRA submission, views audit trail. |
| `DOCTOR1` | `smythVista1` | Provider + OrderEntry, Internal Medicine. | Creates external referrals (some flagged as needing CHS funding), manages the Diabetes Registry pre-visit plan, signs orders. |
| `DOCTOR2` | `smythVista1` | Provider + OrderEntry, Family Medicine. **Does not** hold `CanAuthorizeChs`. | Negative test — confirms the `CanAuthorizeChs` security key gate rejects unauthorized CHS approvals. |
| `PHARM1` | `smythVista1` | Pharmacist. | EPCS, POS claims, auto-refill. |
| `NURSE1` | `smythVista1` | Nurse + OrderEntry. | BCMA, vitals, immunizations (the ACIP forecast is RPMS-flavored). |
| `CLERK1` | `smythVista1` | RegistrationClerk. | Registers tribal patients with eligibility hints (`IsTribalMember`, `TribalAffiliation`, `CdibNumber`, `ResidesInChsda`, `ChsdaResidencyDays`). |
| `BILLING1` | `smythVista1` | ARSupervisor. | CHS dollar-amount audit trail, third-party billing. |

### 5.2 Login Endpoints

```powershell
# REST (returns a JWT)
$login = Invoke-RestMethod -Method Post -Uri https://localhost:7127/api/auth/login `
  -Body (@{ username = "ADMIN1"; password = "smythVista1" } | ConvertTo-Json) `
  -ContentType "application/json"
$jwt = $login.token
```

Blazor login: <https://localhost:7128/login>. WpfDelphiUI: standard CPRS login dialog on launch.

---

## 6. Loading the Tribal Test Dataset

A small, byte-stable, fully synthetic tribal dataset lives at [exports/TribalDemo/](exports/TribalDemo/README.md). It's loaded by [`ITribalDemoSeederGrain`](NewVistas.Abstractions/GrainInterfaces/ITribalDemoSeederGrain.cs), which calls the same workflow grains a live operator would — so loading the seed exercises `IhsTribalEligibilityPolicy`, the CHS authorization workflow, and the GPRA submission packager end-to-end.

### 6.1 What's in the Dataset

| File | Contents |
|---|---|
| [patients.json](exports/TribalDemo/patients.json) | **50** tribal patients with realistic eligibility distribution |
| [chs-referrals.json](exports/TribalDemo/chs-referrals.json) | **8** CHS authorization requests across IHS Medical Priority Classes I–V (6 approved, 2 denied) |
| [gpra-report.json](exports/TribalDemo/gpra-report.json) | **1** completed FY2026 Q1 GPRA report with 11 indicators (diabetes HbA1c testing 79%, depression screening 70%, BP control 70%, etc.) |

Patient eligibility distribution (from [exports/TribalDemo/README.md](exports/TribalDemo/README.md#patient-eligibility-distribution)):

| Tier | Count | What `IhsTribalEligibilityPolicy` stamps |
|---|---|---|
| CHS-eligible | 28 | `PrimaryEligibilityCode = "IHS CHS"` (tribal member, in CHSDA, ≥180 days) |
| Direct-care only | 12 | `PrimaryEligibilityCode = "IHS DIRECT"` (tribal member, but outside CHSDA or <180 days) |
| Eligible by category | 3 | `IHS DIRECT` (non-Indian eligible per 25 CFR § 136.12, e.g. pregnant by an eligible Indian) |
| Walk-in / no IHS hints | 7 | No enrollment record |

Tribal affiliations seeded: Cherokee Nation, Navajo Nation, Choctaw, Chickasaw, Creek, Lakota Sioux, White Mountain Apache, Tohono O'odham, Pascua Yaqui, Cheyenne River Sioux.

ICN format: `099{1000000+index:D7}V{checksum:D6}` — the `099` prefix marks demo data so it can never be confused with a live ICN ([TribalDemoSeederGrain.cs:225](NewVistas.Abstractions/Grains/TribalDemoSeederGrain.cs#L225)).

### 6.2 Loading the Seed

Follow [Blazor/Admin/13-Tribal-Demo-Data.md](NewVistas.Abstractions/Docs/Human-Test-Scripts/Blazor/Admin/13-Tribal-Demo-Data.md). Programmatically:

```csharp
ITribalDemoSeederGrain seeder =
    grainFactory.GetGrain<ITribalDemoSeederGrain>("TRIBAL-DEMO-SEEDER");

TribalDemoSeedResult result = await seeder.LoadAsync(
    manifestDirectory: @"C:\Source\NewVistas\exports\TribalDemo",
    seededByUserId: "ADMIN1",
    seededByUserName: "System Administrator");
```

The seeder is idempotent on patient ICN, so re-running produces no duplicates.

Expected result:

```
result.PatientsRegistered    = 50
result.ChsReferralsCreated   = 8
result.ChsReferralsApproved  = 6
result.ChsReferralsDenied    = 2
result.GpraReportsCreated    = 1
result.PatientIcns           = 50 ICNs all starting with "099"
result.Errors                = []
```

Caller must hold the `CanRegisterPatients` security key — `ADMIN1` does. The grain enforces this with `[RequiresSecurityKey]` on `LoadAsync` via the `AuthorizationCallFilter`.

---

## 7. Verifying / Toggling Site Features at Runtime

The tribal profile pre-enables the RPMS feature flags, but you can flip them at runtime — useful for negative tests ("what does the system look like if CHS is disabled?") and for hybrid clinics that only want some features.

```powershell
# What's currently on?
curl https://localhost:7127/api/siteparameters/features `
  -H "Authorization: Bearer $jwt"

# Disable / enable a single feature
curl -X DELETE https://localhost:7127/api/siteparameters/features/DIABETES_REGISTRY `
  -H "Authorization: Bearer $jwt"
curl -X POST   https://localhost:7127/api/siteparameters/features/DIABETES_REGISTRY `
  -H "Authorization: Bearer $jwt"

# Or check one
curl https://localhost:7127/api/siteparameters/features/DIABETES_REGISTRY `
  -H "Authorization: Bearer $jwt"
```

Or, in code:

```csharp
var siteParams = grainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
await siteParams.DisableFeatureAsync("DIABETES_REGISTRY");
await siteParams.EnableFeatureAsync("DIABETES_REGISTRY");
bool on = await siteParams.IsFeatureEnabledAsync("DIABETES_REGISTRY");
```

---

## 8. Test Documents — What to Run, In What Order

All human test scripts live under [NewVistas.Abstractions/Docs/Human-Test-Scripts/](NewVistas.Abstractions/Docs/Human-Test-Scripts/) organized by UI (`Blazor/`, `Wpf_UI/`, `WpfDelphiUI/`, `CharUI/`) and then by role (`Doctors/`, `Nurses/`, `Pharmacist/`, `Admin/`).

### 8.1 RPMS-Specific Scripts (Run These First — They Exercise Tribal Workflows)

These scripts are **not in stock VistA**. They validate the IHS-specific code paths.

| # | Script | What it validates | Login |
|---|---|---|---|
| 1 | [Blazor/Admin/13-Tribal-Demo-Data.md](NewVistas.Abstractions/Docs/Human-Test-Scripts/Blazor/Admin/13-Tribal-Demo-Data.md) | Tribal seeder, eligibility tier distribution, idempotency | `ADMIN1` |
| 2 | [Blazor/Admin/11-CHS-Authorization.md](NewVistas.Abstractions/Docs/Human-Test-Scripts/Blazor/Admin/11-CHS-Authorization.md) | CHS authorization under 25 CFR Part 136, `CanAuthorizeChs` gate, eligibility check, dollar-amount audit | `DOCTOR1` (request), `ADMIN1` (approve), `DOCTOR2` (negative — denied without key) |
| 3 | [Blazor/Doctors/16-Diabetes-Registry.md](NewVistas.Abstractions/Docs/Human-Test-Scripts/Blazor/Doctors/16-Diabetes-Registry.md) | HbA1c trending, foot/eye/ACR exam tracking, eGFR staging, pre-visit plan composition | `DOCTOR1` + `CanManageDiabetesRegistry` |
| 4 | [Blazor/Admin/12-GPRA-Submission.md](NewVistas.Abstractions/Docs/Human-Test-Scripts/Blazor/Admin/12-GPRA-Submission.md) | GPRA report packaging from the seeded FY2026 Q1 dataset | `ADMIN1` |
| 5 | [Blazor/Admin/14-NDW-Export.md](NewVistas.Abstractions/Docs/Human-Test-Scripts/Blazor/Admin/14-NDW-Export.md) | National Data Warehouse export for tribal data submission | `ADMIN1` |
| 6 | [Blazor/Admin/10-Patient-Merge.md](NewVistas.Abstractions/Docs/Human-Test-Scripts/Blazor/Admin/10-Patient-Merge.md) | Duplicate patient merge — heavy IHS use due to multi-facility registrations | `ADMIN1` |
| 7 | [Blazor/Admin/15-Federation-MPI-Propagation.md](NewVistas.Abstractions/Docs/Human-Test-Scripts/Blazor/Admin/15-Federation-MPI-Propagation.md) | MPI announcements between tribal hub and per-clinic spokes | `ADMIN1` |
| 8 | [Blazor/Pharmacist/13-EPCS.md](NewVistas.Abstractions/Docs/Human-Test-Scripts/Blazor/Pharmacist/13-EPCS.md) | E-prescribing for controlled substances (DEA-compliant) | `PHARM1` + `DOCTOR1` |
| 9 | [Blazor/Pharmacist/12-POS-Claims.md](NewVistas.Abstractions/Docs/Human-Test-Scripts/Blazor/Pharmacist/12-POS-Claims.md) | NCPDP real-time pharmacy claims adjudication | `PHARM1` |
| 10 | [Blazor/Pharmacist/11-Auto-Refill.md](NewVistas.Abstractions/Docs/Human-Test-Scripts/Blazor/Pharmacist/11-Auto-Refill.md) | Automated refill scheduling | `PHARM1` |
| 11 | [WpfDelphiUI/Doctors/01-CPRS-Chart-Walkthrough.md](NewVistas.Abstractions/Docs/Human-Test-Scripts/WpfDelphiUI/Doctors/01-CPRS-Chart-Walkthrough.md) | End-to-end CPRS chart walk-through that exercises the Diabetes Registry panel, the External Referrals + CHS action bar on Consults, and GPRA on Reports | `DOCTOR1` |

### 8.2 Federation Setup (Multi-Clinic Tribal Authority)

If you're running more than one silo (tribal hub + per-clinic spokes), do these once:

| # | Script | Purpose |
|---|---|---|
| - | [Blazor/Admin/00-Federation-Test-Environment.md](NewVistas.Abstractions/Docs/Human-Test-Scripts/Blazor/Admin/00-Federation-Test-Environment.md) | Stand up the multi-cluster test environment |
| - | [Blazor/Admin/02-Hub-CA-Spoke-Onboarding.md](NewVistas.Abstractions/Docs/Human-Test-Scripts/Blazor/Admin/02-Hub-CA-Spoke-Onboarding.md) | Register a clinic spoke under the tribal hub CA |
| - | [Blazor/Admin/05-Sneakernet-Bundle-Transfer.md](NewVistas.Abstractions/Docs/Human-Test-Scripts/Blazor/Admin/05-Sneakernet-Bundle-Transfer.md) | Offline / poor-connectivity replication (relevant for remote tribal sites) |

### 8.3 Universal Scripts (Same in Every Flavor)

Once you're satisfied the RPMS-specific surfaces work, run the universal clinical workflow scripts to confirm nothing tribal-flavored broke the core. Pick whichever UI you ship to clinicians:

- **Doctors:** Cover Sheet Review, Progress Notes, Consult Management, Problem List, Prescribing, Lab/Radiology Orders, Diet Orders, Clinical Reminders, Patient Demographics — see [Blazor/Doctors/](NewVistas.Abstractions/Docs/Human-Test-Scripts/Blazor/Doctors/) (or `Wpf_UI/Doctors/`, `CharUI/Doctors/`).
- **Nurses:** BCMA, Vital Signs, Nursing Assessment + Care Plan, Triage, Task Worklist, Pain Assessment, Shift Handoff — see [Blazor/Nurses/](NewVistas.Abstractions/Docs/Human-Test-Scripts/Blazor/Nurses/).
- **Pharmacists:** Prescription Verification, DUR, Interaction Screening, Inpatient Orders, IV Admixture, Controlled Substances, Drug Accountability, PA, Dispensing/Counseling, CMOP — see [Blazor/Pharmacist/](NewVistas.Abstractions/Docs/Human-Test-Scripts/Blazor/Pharmacist/).

---

## 9. Substituting Modules — VistA Instead of RPMS (or Vice Versa)

The system uses **composition over branching** for site-flavor variation. There is no "RPMS module" and "VistA module" pair to swap; instead, RPMS-specific behavior is provided by *additive* feature grains that only activate when the corresponding flag is on. So "swap a module" really means **toggle a flag** (and optionally swap the policy implementation).

### 9.1 Run an RPMS Site Without (Say) the Diabetes Registry

You want VA-style generic registries instead of the IHS BDM-shaped diabetes registry.

```powershell
$jwt = (Invoke-RestMethod -Method Post `
  -Uri https://localhost:7127/api/auth/login `
  -Body (@{ username = "ADMIN1"; password = "smythVista1" } | ConvertTo-Json) `
  -ContentType "application/json").token

# Turn the IHS-specific module off
Invoke-RestMethod -Method Delete `
  -Uri https://localhost:7127/api/siteparameters/features/DIABETES_REGISTRY `
  -Headers @{ Authorization = "Bearer $jwt" }
```

The Diabetes Registry panel disappears from the WpfDelphiUI Cover Sheet (it's gated by `HasDiabetesRegistry`), the `api/diabetesregistry/...` endpoints start returning `feature-status = false`, and the generic `IClinicalRegistryEntryGrain` is what your code relies on instead. No restart needed.

You can do the same for any of the IHS pre-enabled flags listed in §3.1 — `GPRA_REPORTING`, `IMMUNIZATION_FORECAST`, `EXTERNAL_REFERRAL_TRACKING` (which also disables CHS authorization since it's built on top), `ICARE_DASHBOARD`, etc.

### 9.2 Run a VistA Site That Borrows One RPMS Module

Reverse direction. Start under a generic profile, then turn on a single tribal feature:

```powershell
# Start as a generic dev silo (not IHS)
dotnet run --project NewVistas.SiloHost -- --profile=localhost-dev

# Cherry-pick: enable just immunization forecasting
curl -X POST https://localhost:7127/api/siteparameters/features/IMMUNIZATION_FORECAST `
  -H "Authorization: Bearer $jwt"
```

This is the "Hospital 3 (hybrid)" scenario: enabling `EXTERNAL_REFERRAL_TRACKING` on a non-IHS profile gives you general external-referral tracking *but not the CHS authorization workflow at full fidelity* — CHS approval requires the registration policy to stamp `PrimaryEligibilityCode = "IHS CHS"`, which only `IhsTribalEligibilityPolicy` does. To get full CHS, you also need to swap the eligibility policy (next section).

### 9.3 Swap the Registration Eligibility Policy

The IHS profile registers `IhsTribalEligibilityPolicy` as the `IRegistrationEligibilityPolicy` ([IhsTribalSiteProfile.cs:86](NewVistas.SiloHost/Infrastructure/Profiles/IhsTribalSiteProfile.cs#L86)) **before** `AddCommonSiloServices`, so the default no-op policy from the common services is skipped. To run VA-style on the same silo, do not start with `--profile=ihs-tribal` — pick a different profile (`localhost-dev` or `sql-express-demo`), which leaves the no-op policy in place.

Alternative: write your own profile (a sibling of `IhsTribalSiteProfile`) that registers a different `IRegistrationEligibilityPolicy` (e.g. a hypothetical `VaMeansTestEligibilityPolicy`) and add it to the `Create` switch in [SiteProfileResolver.cs:73-83](NewVistas.SiloHost/Infrastructure/Profiles/SiteProfileResolver.cs#L73-L83).

### 9.4 Author Your Own Profile (e.g. `--profile=mytribe`)

When a single tribal authority needs a customized pre-enabled feature set:

1. Add `MyTribeSiteProfile.cs` next to [IhsTribalSiteProfile.cs](NewVistas.SiloHost/Infrastructure/Profiles/IhsTribalSiteProfile.cs) implementing `ISiteProfile`. Copy the IHS profile and change the `PreEnabledFeatures` array, the cluster ID/ICN prefix defaults, and (optionally) the `IRegistrationEligibilityPolicy` registration.
2. Add a case in [SiteProfileResolver.cs:73-83](NewVistas.SiloHost/Infrastructure/Profiles/SiteProfileResolver.cs#L73-L83):
   ```csharp
   "mytribe" => new MyTribeSiteProfile(),
   ```
3. Also update the error message at [line 82](NewVistas.SiloHost/Infrastructure/Profiles/SiteProfileResolver.cs#L82) so misconfiguration is still loud.
4. Run with `--profile=mytribe` (or `NEWVISTAS_PROFILE=mytribe`).

### 9.5 What You Should *Not* Do

Don't fork the data grains (`IPatientGrain`, `ILabTestGrain`, etc.) into RPMS / VistA variants — clinical data is universal. All flavor differences belong in either the workflow grain (additive feature methods), the site profile (DI substitutions and pre-enabled flags), or the UI (separate Blazor pages / WPF views per flavor).

---

## 10. Quick Reference — Day-One Checklist

```
[ ] Install .NET 10 SDK + SQL Express
[ ] Create the two appsettings.Development.json files from §2 with SqlExpress connection strings
[ ] dotnet build
[ ] Terminal 1:  dotnet run --project NewVistas.SiloHost -- --profile=ihs-tribal
[ ] Terminal 2:  dotnet run --project NewVistas.WebServer
[ ] Terminal 3:  dotnet run --project NewVistas.WpfDelphiUI
[ ] Login as ADMIN1 / smythVista1 → load the tribal demo (script 13)
[ ] Walk through scripts 11, 16 (Diabetes Registry), 12 (GPRA), 01 (WpfDelphiUI Cover Sheet)
[ ] Negative test: log in as DOCTOR2, attempt CHS approval → expect 403 (no CanAuthorizeChs key)
[ ] Confirm Orleans Dashboard at http://localhost:8080 shows tribal grains active
```

Welcome aboard.
