# Blazor Human-Test-Scripts — Deepened Workflow Run (#1)

This goes beyond the render smoke: for each script with a **create form**, the executor logs in as the role, loads the patient, opens the create tab, **fills the Scenario-1 fields, submits, and verifies the result** (success message / created row, or a real error). Screenshots are under `docs/screenshots/test-runs-deep/<Role>/`.

## ✅ Fully automated & verified (real data created)
| Script | Route | Login | Result |
|---|---|---|---|
| Doctors/05-Problem-List | `/problems` | DOCTOR2 | Added "Essential (primary) hypertension / I10" (row appears in list) |
| Doctors/13-Patient-Demographics | `/patient-edit` | DOCTOR1 | **"Saved successfully."** |
| Doctors/14-Allergy-Documentation | `/allergies` | DOCTOR1 | Created Penicillin / Drug / Severe allergy (row appears) |
| Doctors/10-Mental-Health-Screening | `/mental-health` | **MH1** | **"Screen recorded."** |
| Nurses/02-Vital-Signs-Recording | `/vitals` | NURSE1 | Recorded 8 vitals (rows appear in View Vitals) |
| Nurses/04-Nursing-Care-Plan | `/nursing-careplan` | NURSE1 | **"Diagnosis added: NDP-…"** |
| Pharmacist/10-CMOP | `/cmop` | PHARM1 | **"Added to suspense queue."** |
| Pharmacist/05-IV-Admixture | `/iv-pharmacy` | PHARM1 | "IV admixture order created" (intermittent trigger timing) |

## ⚙️ Feature-flagged OFF (real finding — not a bug)
| Script | Route | Result |
|---|---|---|
| Pharmacist/12-POS-Claims | `/pharmacy-pos` | "Pharmacy Point of Sale is not enabled for this site. Enable PHARMACY_POS in Site Parameters." |
| Pharmacist/13-EPCS | `/epcs` | "EPCS is not enabled for this site. Enable EPCS in Site Parameters." |

These pages **work**; the workflow is gated behind a site feature flag that is off by default.

## ◻️ Deep-test N/A (page uses bespoke / multi-step / location-keyed controls)
`/orders` (multi-step order dialog), `/drug-utilization-review` & `/interaction-blocking` (trigger disabled until a prescription context is set), `/lab-shipping` (non-standard submit). These render fine (render smoke ✅) but don't fit the generic "tab → fill → submit" recipe; they'd need page-specific recipes.

## Notable findings
- **Mental-Health is key-gated:** the script lists login `DOCTOR5`, but that user lacks `YS MH INSTRUMENT`; only MH-role users can use it ("Access denied" with DOCTOR5). Worth correcting the script's stated login.
- **Success banners are transient:** on success these pages reload/switch tabs and clear `.alert-success`, so the executor treats "submitted, no `.alert-error`/`.alert-danger`" as PASS (validated against created rows).
- **UI is heterogeneous:** create forms variously use `button.tab` / `nav-link` / `+ New X` triggers, `btn-primary` / `btn-success` submits, and `.alert-error` / `.alert-danger` / `.error` banners — the executor was generalized to be text-based and class-agnostic.

## Coverage note
~30 of the 51 runnable scripts are **not simple create forms** — they operate on existing/seeded data (verify/fill an existing Rx, dispense from stock, complete a reminder), are reference/lookup pages (Drug Formulary, Drug File), or are multi-step wizards. Those remain covered by the **render smoke** (`../test-runs/RESULTS.md`). Extending the deep pass to them is page-by-page recipe work the framework now supports.
