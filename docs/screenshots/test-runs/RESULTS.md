# Blazor UI Human-Test-Script Smoke Run — Results

**What this is:** an automated load-and-render pass over every Blazor Human-Test-Script. For each script the harness logs in as the script's role, navigates to the screen in-app, loads a patient where the screen exposes the standard lookup, screenshots it, and checks the Blazor error UI is not shown. It confirms each screen **loads, renders, and is error-free** — it does not yet drive every multi-step data-entry scenario (that's a follow-up).

**Environment:** full stack (SiloHost + WebServer + BlazorWeb) in a single Linux container, demo dataset (patients P1–P50) + rich patient P9001 seeded; headless Edge via Playwright.

**Screenshots:** one per script under `docs/screenshots/test-runs/<Role>/<script>.png`.

## Summary

- ✅ **51 render cleanly** (no Blazor error UI)
- 🚫 **9 blocked** — need federation/multi-cluster/CLI infrastructure not present on a single node
- ❌ **0 hard failures**

## Doctors (17)

| Script | Route | Login | Result | Screenshot | Notes |
|---|---|---|---|---|---|
| 01-Cover-Sheet-Review | `/cover-sheet` | DOCTOR1 | ✅ PASS | [png](Doctors/01-Cover-Sheet-Review.png) | Heading: 📋 Cover Sheet |
| 02-Order-Entry | `/orders` | DOCTOR1 | ✅ PASS | [png](Doctors/02-Order-Entry.png) | Heading: Order Entry / Results Reporting |
| 03-Progress-Notes | `/notes` | DOCTOR1 | ✅ PASS | [png](Doctors/03-Progress-Notes.png) | Heading: Progress Notes |
| 04-Consult-Management | `/consults` | DOCTOR1 | ✅ PASS | [png](Doctors/04-Consult-Management.png) | Heading: Consults |
| 05-Problem-List | `/problems` | DOCTOR2 | ✅ PASS | [png](Doctors/05-Problem-List.png) | Heading: Problem List |
| 06-Laboratory-Orders | `/labs` | DOCTOR1 | ✅ PASS | [png](Doctors/06-Laboratory-Orders.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |
| 07-Prescribing | `/outpatientpharmacy` | DOCTOR2 | ✅ PASS | [png](Doctors/07-Prescribing.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |
| 08-Surgery-Scheduling | `/surgery` | SURGEON1 | ✅ PASS | [png](Doctors/08-Surgery-Scheduling.png) | Heading: Surgery |
| 09-Radiology-Orders | `/radiology` | DOCTOR3 | ✅ PASS | [png](Doctors/09-Radiology-Orders.png) | Heading: Radiology |
| 10-Mental-Health-Screening | `/mental-health` | DOCTOR5 | ✅ PASS | [png](Doctors/10-Mental-Health-Screening.png) | Heading: Mental Health Screening |
| 11-Diet-Orders | `/dietetics` | DOCTOR1 | ✅ PASS | [png](Doctors/11-Diet-Orders.png) | Heading: Dietetics |
| 12-Clinical-Reminders | `/reminders` | DOCTOR2 | ✅ PASS | [png](Doctors/12-Clinical-Reminders.png) | Heading: ⏰ Clinical Reminders |
| 13-Patient-Demographics | `/patient-edit` | DOCTOR1 | ✅ PASS | [png](Doctors/13-Patient-Demographics.png) | Heading: Edit Patient |
| 14-Allergy-Documentation | `/allergies` | DOCTOR1 | ✅ PASS | [png](Doctors/14-Allergy-Documentation.png) | Heading: Allergies |
| 15-Scheduling-Enhancements | `/scheduling` | DOCTOR1 | ✅ PASS | [png](Doctors/15-Scheduling-Enhancements.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |
| 16-Diabetes-Registry | `/clinical-registries` | DOCTOR1 | ✅ PASS | [png](Doctors/16-Diabetes-Registry.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |
| 17-Inpatient-Stay-End-to-End | `/adt` | DOCTOR1 | ✅ PASS | [png](Doctors/17-Inpatient-Stay-End-to-End.png) | Heading: ADT — Admit / Discharge / Transfer |

## Nurses (10)

| Script | Route | Login | Result | Screenshot | Notes |
|---|---|---|---|---|---|
| 01-BCMA-Medication-Administration | `/bcma` | NURSE2 | ✅ PASS | [png](Nurses/01-BCMA-Medication-Administration.png) | Heading: BCMA — Bar Code Medication Administration |
| 02-Vital-Signs-Recording | `/vitals` | NURSE1 | ✅ PASS | [png](Nurses/02-Vital-Signs-Recording.png) | Heading: Vitals |
| 03-Nursing-Assessment | `/nursing` | NURSE1 | ✅ PASS | [png](Nurses/03-Nursing-Assessment.png) | Shell + patient-load control render; assessment body loads via the page's own **Load Patient** button (verified, 9 controls). |
| 04-Nursing-Care-Plan | `/nursing-careplan` | NURSE1 | ✅ PASS | [png](Nurses/04-Nursing-Care-Plan.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |
| 05-Nursing-Triage | `/nursing-triage` | NURSE3 | ✅ PASS | [png](Nurses/05-Nursing-Triage.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |
| 06-Nursing-Task-Worklist | `/nursing-tasks` | NURSE2 | ✅ PASS | [png](Nurses/06-Nursing-Task-Worklist.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |
| 07-Pain-Assessment | `/pain-assessment` | NURSE4 | ✅ PASS | [png](Nurses/07-Pain-Assessment.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |
| 08-Shift-Handoff | `/shift-handoff` | NURSE5 | ✅ PASS | [png](Nurses/08-Shift-Handoff.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |
| 09-Allergy-Documentation | `/allergies` | NURSE4 | ✅ PASS | [png](Nurses/09-Allergy-Documentation.png) | Heading: Allergies |
| 10-Clinical-Notes | `/notes` | NURSE1 | ✅ PASS | [png](Nurses/10-Clinical-Notes.png) | Heading: Progress Notes |

## Pharmacist (18)

| Script | Route | Login | Result | Screenshot | Notes |
|---|---|---|---|---|---|
| 01-Prescription-Verification-Fill | `/outpatientpharmacy` | PHARM1 | ✅ PASS | [png](Pharmacist/01-Prescription-Verification-Fill.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |
| 02-Drug-Utilization-Review | `/drug-utilization-review` | PHARM1 | ✅ PASS | [png](Pharmacist/02-Drug-Utilization-Review.png) | Heading: Drug Utilization Review |
| 03-Interaction-Screening | `/interaction-blocking` | PHARM1 | ✅ PASS | [png](Pharmacist/03-Interaction-Screening.png) | Heading: Drug Interaction Blocking |
| 04-Inpatient-Orders | `/inpatientpharmacy` | PHARM1 | ✅ PASS | [png](Pharmacist/04-Inpatient-Orders.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |
| 05-IV-Admixture | `/iv-pharmacy` | PHARM1 | ✅ PASS | [png](Pharmacist/05-IV-Admixture.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |
| 06-Controlled-Substances | `/controlled-substances` | PHARM1 | ✅ PASS | [png](Pharmacist/06-Controlled-Substances.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |
| 07-Drug-Accountability | `/drugaccountability` | PHARM1 | ✅ PASS | [png](Pharmacist/07-Drug-Accountability.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |
| 08-Pharmacy-Benefits-PA | `/pharmacybenefits` | PHARM1 | ✅ PASS | [png](Pharmacist/08-Pharmacy-Benefits-PA.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |
| 09-Dispensing-Counseling-Labels | `/outpatientpharmacy` | PHARM1 | ✅ PASS | [png](Pharmacist/09-Dispensing-Counseling-Labels.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |
| 10-CMOP | `/cmop` | PHARM1 | ✅ PASS | [png](Pharmacist/10-CMOP.png) | Heading: Consolidated Mail Outpatient Pharmacy |
| 11-Auto-Refill | `/auto-refill` | PHARM1 | ✅ PASS | [png](Pharmacist/11-Auto-Refill.png) | Heading: Automated Prescription Refill |
| 12-POS-Claims | `/pharmacy-pos` | PHARM1 | ✅ PASS | [png](Pharmacist/12-POS-Claims.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |
| 13-EPCS | `/epcs` | PHARM1 | ✅ PASS | [png](Pharmacist/13-EPCS.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |
| 14-Drug-Formulary-Reference | `/drugformulary` | PHARM1 | ✅ PASS | [png](Pharmacist/14-Drug-Formulary-Reference.png) | Heading: 💊 Drug Formulary — National Drug File (NDF) |
| 15-Drug-File | `/drugfile` | PHARM1 | ✅ PASS | [png](Pharmacist/15-Drug-File.png) | Heading: Pharmacy Data Management — Drug File |
| 16-Ward-Stock | `/ward-stock` | PHARM1 | ✅ PASS | [png](Pharmacist/16-Ward-Stock.png) | Heading: Ward Stock / Auto Replenishment |
| 17-Drug-Interaction-Dataset | `/drug-interactions` | PHARM1 | ✅ PASS | [png](Pharmacist/17-Drug-Interaction-Dataset.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |
| 18-Lab-Shipping | `/lab-shipping` | PHARM1 | ✅ PASS | [png](Pharmacist/18-Lab-Shipping.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |

## Admin (15)

| Script | Route | Login | Result | Screenshot | Notes |
|---|---|---|---|---|---|
| 00-Federation-Test-Environment | `-` | - | 🚫 BLOCKED |  | environment setup doc, not a UI test |
| 01-Federation-Dashboard-Smoke | `/admin/federation` | ADMIN1 | ✅ PASS | [png](Admin/01-Federation-Dashboard-Smoke.png) | Heading: 🔐 Federation security |
| 02-Hub-CA-Spoke-Onboarding | `-` | - | 🚫 BLOCKED |  | needs a Spoke cluster + CSR signing |
| 03-Certificate-Renewal | `-` | - | 🚫 BLOCKED |  | needs Hub-CA + issued cert |
| 04-Certificate-Revocation | `/admin/federation` | ADMIN1 | ✅ PASS | [png](Admin/04-Certificate-Revocation.png) | Heading: 🔐 Federation security |
| 05-Sneakernet-Bundle-Transfer | `-` | - | 🚫 BLOCKED |  | CLI bundle transfer between clusters |
| 06-Federation-Outbox-Drainer | `/admin/federation` | ADMIN1 | ✅ PASS | [png](Admin/06-Federation-Outbox-Drainer.png) | Heading: 🔐 Federation security |
| 07-Cluster-Identity-Multi-Cluster | `-` | - | 🚫 BLOCKED |  | needs >1 cluster |
| 08-Clinical-Event-Sourcing | `-` | - | 🚫 BLOCKED |  | API/replay script, no dedicated Blazor page |
| 10-Patient-Merge | `/patient-merge` | ADMIN1 | ✅ PASS | [png](Admin/10-Patient-Merge.png) | Heading: Patient Merge |
| 11-CHS-Authorization | `-` | - | 🚫 BLOCKED |  | no dedicated Blazor page in this build |
| 12-GPRA-Submission | `/gpra-reporting` | ADMIN1 | ✅ PASS | [png](Admin/12-GPRA-Submission.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |
| 13-Tribal-Demo-Data | `-` | - | 🚫 BLOCKED |  | data-seeding script (operator CLI) |
| 14-NDW-Export | `-` | - | 🚫 BLOCKED |  | batch export job, no dedicated Blazor page |
| 15-Federation-MPI-Propagation | `/mpi` | ADMIN1 | ✅ PASS | [png](Admin/15-Federation-MPI-Propagation.png) | Renders (uses an h2 header); patient-load uses a page-specific control on some screens. |
