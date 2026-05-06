# Reports & Clinical Review -- Pharmacist CharUI Human Test Script

## Prerequisites

- **Login:** PHARM1 / Password: `smythVista1`
- **Security Keys:** PSO PHARMACY, PSJ RPHARM, PSA ORDERS, PSB MANAGER
- **Patient:** Select a patient with demo data loaded.
- **Pre-conditions:** SiloHost and WebServer running. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: Access Reports Menu

### Steps

1. At the Main Menu, type: `RP` and press Enter.

### Expected Result

- Reports menu displays 14 report options. All are read-only and accessible to pharmacists.

---

## Scenario 2: Health Summary Report

### Steps

1. At the Reports menu, type: `1` (Health Summary).

### Expected Result

- Summary with counts of problems, allergies, medications, vitals, orders, notes, consults.
- CWAD flags and SC status displayed.

---

## Scenario 3: Allergies Report (Critical for Pharmacist)

### Steps

1. At the Reports menu, type: `3` (Allergies Report).

### Expected Result

- Full allergy table with allergen, type, severity, reactions.
- **Pharmacist focus:** Primary safety check before any dispensing activity.

---

## Scenario 4: Active Medications Report (Primary Pharmacist View)

### Steps

1. At the Reports menu, type: `4` (Active Medications).

### Expected Result

- Complete medication profile: drug, sig, status, fill date, refills.
- **Pharmacist review checklist:**
  - Duplicate therapy check
  - Dosing appropriateness
  - Drug-drug interactions
  - Drug-allergy conflicts
  - Medication adherence (refill patterns)

---

## Scenario 5: Lab Summary Report (Dose Adjustment Basis)

### Steps

1. At the Reports menu, type: `5` (Lab Summary).

### Expected Result

- Lab results with values, units, and flags.
- **Pharmacist focus:**
  - Creatinine/eGFR for renal dose adjustments
  - LFTs for hepatic metabolism considerations
  - Therapeutic drug levels (vancomycin, aminoglycosides)
  - INR for warfarin management
  - HbA1c for diabetes medication efficacy

---

## Scenario 6: Abnormal Lab Results Report

### Steps

1. At the Reports menu, type: `6` (Lab Results - Abnormal).

### Expected Result

- Only abnormal values displayed.
- **Pharmacist focus:** May indicate adverse drug reactions or need for therapy adjustment.

---

## Scenario 7: Problem List Report

### Steps

1. At the Reports menu, type: `2` (Problem List).

### Expected Result

- All problems with ICD codes.
- **Pharmacist focus:** Verify medications align with documented diagnoses (indication checking).

---

## Scenario 8: Active Orders Report

### Steps

1. At the Reports menu, type: `8` (Active Orders).

### Expected Result

- Active orders table.
- **Pharmacist focus:** Identify pending medication orders requiring verification.

---

## Scenario 9: Vital Signs Report

### Steps

1. At the Reports menu, type: `7` (Vital Signs).

### Expected Result

- Current vital signs.
- **Pharmacist focus:** Blood pressure (antihypertensive efficacy), weight (dose calculations), pain level (analgesic therapy).

---

## Scenario 10: Consults Report

### Steps

1. At the Reports menu, type: `9` (Consults).

### Expected Result

- Consult requests.
- **Pharmacist focus:** Pharmacy consults awaiting response.

---

## Scenario 11: Clinical Notes Report

### Steps

1. At the Reports menu, type: `10` (Clinical Notes).

### Expected Result

- Paged list of clinical notes.
- **Pharmacist focus:** Provider notes with therapy rationale, medication changes, and clinical decisions.

---

## Scenario 12: Run All 14 Reports Sequentially

### Steps

1. Run each report (1 through 14) for a patient with full demo data.

### Expected Result

- All reports display data without errors or access denied messages.
- Pharmacists have unrestricted read access to all reports.

---

## Scenario 13: Reports for Empty Patient

### Steps

1. Select a patient with no demo data.
2. Run reports 1, 3, 4, 5, 6.

### Expected Result

- Empty/none messages for each report. No errors.

---

## Scenario 14: Pharmacist Quick Clinical Review Workflow

### Steps

This scenario simulates a pharmacist's typical clinical review before verifying an order:

1. View Cover Sheet (Main Menu > `CV`) -- get the big picture.
2. Check Allergies (Main Menu > `AL` > `1`) -- safety check.
3. Review Medications (Main Menu > `ME` > `1`) -- current profile.
4. Check Lab Results (Main Menu > `LA` > `3`) -- abnormal values.
5. View Problem List (Main Menu > `PL` > `1`) -- indication check.
6. Review Active Orders (Main Menu > `OR` > `1`) -- pending items.

### Expected Result

- All 6 steps complete without errors.
- Pharmacist has reviewed: allergies, medications, labs, problems, and orders.
- This provides sufficient clinical context for informed dispensing decisions.
- **Note:** For the actual verification/dispensing workflow, use the Blazor UI at `/outpatientpharmacy` or `/inpatientpharmacy`.

---

## Scenario 15: Return to Main Menu from Reports

### Steps

1. At the Reports menu, type `Q` or `^`.

### Expected Result

- Returns to the Main Menu.
