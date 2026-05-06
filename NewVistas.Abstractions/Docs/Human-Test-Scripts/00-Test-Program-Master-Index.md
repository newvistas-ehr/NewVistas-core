# NewVistas Comprehensive Human Test Program -- Master Index

**Audience:** QA testers, system administrators, clinical SMEs, deployment engineers.
**Scope:** End-to-end manual validation of all clinical, administrative, and infrastructure features in NewVistas.
**Last updated:** 2026-04-27 (covers MultiCluster branch through commit 6f9e7a8d).

This index ties every existing role-based test script together with the **new infrastructure test scripts** added for federation, certificate management, multi-cluster, sneakernet, and clinical event sourcing. Use it as the entry point when planning a full regression test pass before a release or deployment.

---

## How to Use This Document

1. Pick the **Test Pass** that matches the change you are validating (Smoke, Clinical Regression, Federation/Infrastructure, Full Regression, or Pre-Release).
2. Follow the scripts **in the listed order** -- prerequisites compound (e.g., Hub-CA must work before Sneakernet bundle import can be tested).
3. For each script, fill in the `[ ]` checkboxes and record the tester's initials, date, build/commit hash, and pass/fail.
4. Cross-reference any failure with the listed functional/unit tests under `NewVistas.FunctionalTests` or `NewVistas.UnitTests` -- if both the human script and the automated test fail, the bug is in the implementation; if only the human script fails, the bug is in the UI/integration glue.
5. File defects with the script ID, scenario number, and step number (e.g., "Admin-02 Scenario 4 Step 3").

---

## Test Pass Profiles

### Smoke Pass (~30 minutes)
Quick post-build sanity check. All scripts in this list use a single-silo localhost configuration.

| # | Script | Role | Time |
|---|--------|------|------|
| 1 | [Blazor/Doctors/01-Cover-Sheet-Review.md](Blazor/Doctors/01-Cover-Sheet-Review.md) | Doctor | 5m |
| 2 | [Blazor/Doctors/02-Order-Entry.md](Blazor/Doctors/02-Order-Entry.md) | Doctor | 5m |
| 3 | [Blazor/Nurses/01-BCMA-Medication-Administration.md](Blazor/Nurses/01-BCMA-Medication-Administration.md) | Nurse | 5m |
| 4 | [Blazor/Nurses/02-Vital-Signs-Recording.md](Blazor/Nurses/02-Vital-Signs-Recording.md) | Nurse | 5m |
| 5 | [Blazor/Pharmacist/01-Prescription-Verification-Fill.md](Blazor/Pharmacist/01-Prescription-Verification-Fill.md) | Pharmacist | 5m |
| 6 | [Blazor/Admin/01-Federation-Dashboard-Smoke.md](Blazor/Admin/01-Federation-Dashboard-Smoke.md) | Administrator | 5m |

### Clinical Regression Pass (~4 hours)
All clinical workflow scripts across the three role suites.

- All 15 scripts in [Blazor/Doctors/](Blazor/Doctors/)
- All 10 scripts in [Blazor/Nurses/](Blazor/Nurses/)
- All 18 scripts in [Blazor/Pharmacist/](Blazor/Pharmacist/)

### Federation & Infrastructure Pass (~3 hours)
The new infrastructure test scripts. **Requires two silo deployments** (a "Hub" and a "Spoke") -- see [Blazor/Admin/00-Federation-Test-Environment.md](Blazor/Admin/00-Federation-Test-Environment.md) for setup.

| # | Script | Role | Time | Cluster Setup |
|---|--------|------|------|---------------|
| 1 | [Blazor/Admin/00-Federation-Test-Environment.md](Blazor/Admin/00-Federation-Test-Environment.md) | Administrator | 30m | Hub + Spoke |
| 2 | [Blazor/Admin/01-Federation-Dashboard-Smoke.md](Blazor/Admin/01-Federation-Dashboard-Smoke.md) | Administrator | 10m | Single |
| 3 | [Blazor/Admin/02-Hub-CA-Spoke-Onboarding.md](Blazor/Admin/02-Hub-CA-Spoke-Onboarding.md) | Administrator | 30m | Hub + Spoke |
| 4 | [Blazor/Admin/03-Certificate-Renewal.md](Blazor/Admin/03-Certificate-Renewal.md) | Administrator | 25m | Hub + Spoke |
| 5 | [Blazor/Admin/04-Certificate-Revocation.md](Blazor/Admin/04-Certificate-Revocation.md) | Administrator | 25m | Hub + Spoke |
| 6 | [Blazor/Admin/05-Sneakernet-Bundle-Transfer.md](Blazor/Admin/05-Sneakernet-Bundle-Transfer.md) | Administrator | 30m | Hub + Spoke |
| 7 | [Blazor/Admin/06-Federation-Outbox-Drainer.md](Blazor/Admin/06-Federation-Outbox-Drainer.md) | Administrator | 25m | Hub + Spoke |
| 8 | [Blazor/Admin/07-Cluster-Identity-Multi-Cluster.md](Blazor/Admin/07-Cluster-Identity-Multi-Cluster.md) | Administrator | 25m | Hub + Spoke |
| 9 | [Blazor/Admin/08-Clinical-Event-Sourcing.md](Blazor/Admin/08-Clinical-Event-Sourcing.md) | Administrator + Clinician | 30m | Single |

### Pre-Release Pass (~10 hours)
Smoke + Clinical Regression + Federation/Infrastructure + Patient Portal.

---

## Cross-Cutting Prerequisites

These apply to **every** script unless noted otherwise.

### Demo Logins (password = `smythVista1` for all)

| Role | Username | Notes |
|------|----------|-------|
| Doctor | `DOCTOR1` (SMITH,JOHN A) | Default for all Doctors scripts |
| Nurse | `NURSE1` (JOHNSON,MARY R) | Default for all Nurses scripts |
| Pharmacist | `PHARM1` (WILLIAMS,ROBERT L) | Default for all Pharmacist scripts |
| Administrator | `ADMIN1` (SMYTH,JAMES B) | Required for all Admin/Federation scripts. Has Administrator + ChiefOfStaff + PrivacyOfficer roles. |
| PrivacyOfficer | `ADMIN4` (GREEN,SANDRA J) | Audit-trail-only scenarios |

Reference: [Demo Users & Login Reference](../../../NewVistas.BlazorWeb/UserManual/admin/demo-users.md)

### Standard Ports

| Service | HTTPS | HTTP | Notes |
|---------|-------|------|-------|
| WebServer (REST API) | 7127 | 5298 | All `/api/...` URLs in scripts assume `https://localhost:7127` |
| BlazorWeb (UI) | 7137 | 5196 | All `/admin/...` and `/scheduling` URLs assume `https://localhost:7137` |
| Orleans Dashboard | 8080 | 8080 | Dev only; `http://localhost:8080` |
| Orleans Silo Gateway | -- | 30000 | Internal cluster comms |

Reference: [START.md](../../../START.md), [SETUP-DEVELOPMENT-ENVIRONMENT.md](../../../SETUP-DEVELOPMENT-ENVIRONMENT.md)

### Demo Patient Sets

| Set | Path | Count | Use For |
|-----|------|-------|---------|
| Fifty | `exports/Fifty/` | 50 | Default for all role scripts |
| FiveHundred | `exports/FiveHundred/` | 500 | Performance & batch scripts |
| OneThousand | `exports/OneThousand/` | 1000 | Stress / federation outbox load |

---

## Test Suite Inventory

### Blazor / Doctor (15 scripts)

| # | Script | Covers | Recently Updated For |
|---|--------|--------|----------------------|
| 01 | [Cover-Sheet-Review](Blazor/Doctors/01-Cover-Sheet-Review.md) | Patient summary, problem list, recent labs/vitals | -- |
| 02 | [Order-Entry](Blazor/Doctors/02-Order-Entry.md) | CPRS-style order entry | **Event Sourcing appendix** |
| 03 | [Progress-Notes](Blazor/Doctors/03-Progress-Notes.md) | TIU note authoring & e-signature | -- |
| 04 | [Consult-Management](Blazor/Doctors/04-Consult-Management.md) | Consult requests & tracking | -- |
| 05 | [Problem-List](Blazor/Doctors/05-Problem-List.md) | Active/inactive problem maintenance | -- |
| 06 | [Laboratory-Orders](Blazor/Doctors/06-Laboratory-Orders.md) | Lab order entry & result review | **Event Sourcing appendix** |
| 07 | [Prescribing](Blazor/Doctors/07-Prescribing.md) | Outpatient & inpatient prescribing | -- |
| 08 | [Surgery-Scheduling](Blazor/Doctors/08-Surgery-Scheduling.md) | OR scheduling | -- |
| 09 | [Radiology-Orders](Blazor/Doctors/09-Radiology-Orders.md) | Imaging orders | -- |
| 10 | [Mental-Health-Screening](Blazor/Doctors/10-Mental-Health-Screening.md) | PHQ-9, AUDIT-C, etc. | **Event Sourcing appendix** |
| 11 | [Diet-Orders](Blazor/Doctors/11-Diet-Orders.md) | Inpatient diet orders | -- |
| 12 | [Clinical-Reminders](Blazor/Doctors/12-Clinical-Reminders.md) | Reminder firing & resolution | -- |
| 13 | [Patient-Demographics](Blazor/Doctors/13-Patient-Demographics.md) | Demographic edits | -- |
| 14 | [Allergy-Documentation](Blazor/Doctors/14-Allergy-Documentation.md) | Allergy capture & verification | **Event Sourcing appendix** |
| 15 | [Scheduling-Enhancements](Blazor/Doctors/15-Scheduling-Enhancements.md) | Provider availability + batch unavailability | **Functional test cross-refs added** |

### Blazor / Nurse (10 scripts)

| # | Script | Covers |
|---|--------|--------|
| 01 | [BCMA-Medication-Administration](Blazor/Nurses/01-BCMA-Medication-Administration.md) | Barcode med admin |
| 02 | [Vital-Signs-Recording](Blazor/Nurses/02-Vital-Signs-Recording.md) | Vitals capture |
| 03 | [Nursing-Assessment](Blazor/Nurses/03-Nursing-Assessment.md) | Admission/shift assessments |
| 04 | [Nursing-Care-Plan](Blazor/Nurses/04-Nursing-Care-Plan.md) | Care plan authoring |
| 05 | [Nursing-Triage](Blazor/Nurses/05-Nursing-Triage.md) | ESI triage |
| 06 | [Nursing-Task-Worklist](Blazor/Nurses/06-Nursing-Task-Worklist.md) | Task list completion |
| 07 | [Pain-Assessment](Blazor/Nurses/07-Pain-Assessment.md) | Pain scale capture |
| 08 | [Shift-Handoff](Blazor/Nurses/08-Shift-Handoff.md) | SBAR handoff |
| 09 | [Allergy-Documentation](Blazor/Nurses/09-Allergy-Documentation.md) | Allergy capture |
| 10 | [Clinical-Notes](Blazor/Nurses/10-Clinical-Notes.md) | Nursing notes |

### Blazor / Pharmacist (18 scripts)

| # | Script | Covers |
|---|--------|--------|
| 01 | [Prescription-Verification-Fill](Blazor/Pharmacist/01-Prescription-Verification-Fill.md) | Verify & fill workflow |
| 02 | [Drug-Utilization-Review](Blazor/Pharmacist/02-Drug-Utilization-Review.md) | DUR checks |
| 03 | [Interaction-Screening](Blazor/Pharmacist/03-Interaction-Screening.md) | Drug-drug & drug-allergy |
| 04 | [Inpatient-Orders](Blazor/Pharmacist/04-Inpatient-Orders.md) | Unit-dose dispensing |
| 05 | [IV-Admixture](Blazor/Pharmacist/05-IV-Admixture.md) | IV compounding |
| 06 | [Controlled-Substances](Blazor/Pharmacist/06-Controlled-Substances.md) | CII-V dispensing |
| 07 | [Drug-Accountability](Blazor/Pharmacist/07-Drug-Accountability.md) | Inventory tracking |
| 08 | [Pharmacy-Benefits-PA](Blazor/Pharmacist/08-Pharmacy-Benefits-PA.md) | Prior auth |
| 09 | [Dispensing-Counseling-Labels](Blazor/Pharmacist/09-Dispensing-Counseling-Labels.md) | Patient counseling, label prints |
| 10 | [CMOP](Blazor/Pharmacist/10-CMOP.md) | Mail-out pharmacy |
| 11 | [Auto-Refill](Blazor/Pharmacist/11-Auto-Refill.md) | Refill automation |
| 12 | [POS-Claims](Blazor/Pharmacist/12-POS-Claims.md) | Point-of-sale claims |
| 13 | [EPCS](Blazor/Pharmacist/13-EPCS.md) | Electronic prescribing of controlled substances |
| 14 | [Drug-Formulary-Reference](Blazor/Pharmacist/14-Drug-Formulary-Reference.md) | Formulary lookups |
| 15 | [Drug-File](Blazor/Pharmacist/15-Drug-File.md) | Drug file maintenance |
| 16 | [Ward-Stock](Blazor/Pharmacist/16-Ward-Stock.md) | Floor stock replenishment |
| 17 | [Drug-Interaction-Dataset](Blazor/Pharmacist/17-Drug-Interaction-Dataset.md) | Interaction file management |
| 18 | [Lab-Shipping](Blazor/Pharmacist/18-Lab-Shipping.md) | Specimen shipping |

### Blazor / Admin -- INFRASTRUCTURE (NEW)

| # | Script | Covers |
|---|--------|--------|
| 00 | [Federation-Test-Environment](Blazor/Admin/00-Federation-Test-Environment.md) | Two-silo Hub+Spoke setup, ports, certs |
| 01 | [Federation-Dashboard-Smoke](Blazor/Admin/01-Federation-Dashboard-Smoke.md) | `/admin/federation` dashboard panels |
| 02 | [Hub-CA-Spoke-Onboarding](Blazor/Admin/02-Hub-CA-Spoke-Onboarding.md) | Token issuance → CSR → cert install |
| 03 | [Certificate-Renewal](Blazor/Admin/03-Certificate-Renewal.md) | Spoke cert auto-renewal & atomic swap |
| 04 | [Certificate-Revocation](Blazor/Admin/04-Certificate-Revocation.md) | Revoke cert, cache refresh, deny inbound |
| 05 | [Sneakernet-Bundle-Transfer](Blazor/Admin/05-Sneakernet-Bundle-Transfer.md) | Export bundle → copy → import → verify |
| 06 | [Federation-Outbox-Drainer](Blazor/Admin/06-Federation-Outbox-Drainer.md) | SQL outbox + HTTP drainer |
| 07 | [Cluster-Identity-Multi-Cluster](Blazor/Admin/07-Cluster-Identity-Multi-Cluster.md) | Cluster identity stamping & forensic attribution |
| 08 | [Clinical-Event-Sourcing](Blazor/Admin/08-Clinical-Event-Sourcing.md) | Per-patient event stream, replay, hash chain |
| 09 | _Patient-Identity-ICN-Ranges_ (planned) | Per-cluster ICN issuance, federation routing by ICN, link-on-discovery -- see [ADR-001](../Architect-decisions/ADR-001-Patient-Identity-Strategy.md) and [implementation plan](../Implementation/PATIENT_IDENTITY_IMPLEMENTATION_PLAN.md) |

### Patient Portal (1 script)

| # | Script | Covers |
|---|--------|--------|
| 01 | [Patient-Scheduling](PatientPortal/01-Patient-Scheduling.md) | Patient self-scheduling |

### Other UI Surfaces (legacy, lower priority)

- [CharUI/](CharUI/) -- Character-mode terminal UI (CPRS-equivalent)
- [Wpf_UI/](Wpf_UI/) -- WPF rich-client UI

---

## Defect Reporting Template

```
Script:    [e.g. Blazor/Admin/02-Hub-CA-Spoke-Onboarding.md]
Scenario:  [e.g. Scenario 4 -- CSR signing]
Step:      [e.g. Step 3]
Build:     [git commit hash]
Tester:    [initials]
Date:      [YYYY-MM-DD]
Expected:  [verbatim from "Expected Result" section]
Actual:    [what happened]
Logs:      [SiloHost / WebServer / BlazorWeb log excerpts]
Functional test (if any): [class.method]
Severity:  [Blocker / Critical / Major / Minor / Trivial]
```

---

## Sign-Off Matrix

| Test Pass | Tester | Date | Build | Pass / Fail | Notes |
|-----------|--------|------|-------|-------------|-------|
| Smoke | | | | | |
| Clinical Regression | | | | | |
| Federation & Infrastructure | | | | | |
| Pre-Release | | | | | |
