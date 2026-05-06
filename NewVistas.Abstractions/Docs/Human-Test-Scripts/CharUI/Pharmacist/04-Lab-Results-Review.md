# Lab Results Review -- Pharmacist CharUI Human Test Script

## Prerequisites

- **Login:** PHARM1 / Password: `smythVista1`
- **Security Keys:** PSO PHARMACY, PSJ RPHARM, PSA ORDERS, PSB MANAGER
- **Patient:** Select a patient with demo lab data loaded.
- **Pre-conditions:**
  1. SiloHost and WebServer running.
  2. Demo lab data loaded: `POST /api/lab/demo/load?patientId={patientId}`
  3. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: View Lab Results (Happy Path)

### Steps

1. At the Main Menu, type: `LA` and press Enter.
2. At the Labs menu, type: `1` (View Lab Results).

### Expected Result

- Table displays: #, Test, Result, Units, Flag, Status, Date.
- Demo data includes CBC, BMP, and LFT panels.
- Abnormal values display with `*H*` or `*L*` flags.
- **Pharmacist review focus:**
  - Renal function (BUN, Creatinine, eGFR) -- dosage adjustments
  - Hepatic function (ALT, AST, Alk Phos) -- drug metabolism
  - CBC (WBC, Platelets) -- drug-induced hematologic effects
  - Electrolytes (K+, Na+, Ca2+) -- drug-electrolyte interactions

---

## Scenario 2: View Lab Summary

### Steps

1. At the Labs menu, type: `2` (View Lab Summary).

### Expected Result

- Summary showing latest value per test type with trend data (last 3 values).
- Useful for tracking drug efficacy (e.g., trending HbA1c for diabetes management, INR for warfarin).

---

## Scenario 3: View Abnormal Results Only

### Steps

1. At the Labs menu, type: `3` (View Abnormal Results).

### Expected Result

- Only abnormal results displayed.
- **Pharmacist focus:** Abnormal labs may indicate adverse drug reactions:
  - Elevated ALT/AST -- statin-induced hepatotoxicity
  - Elevated creatinine -- nephrotoxic drug effects
  - Low WBC -- drug-induced leukopenia
  - Elevated potassium -- ACE inhibitor or potassium-sparing diuretic effect

---

## Scenario 4: Order a Lab Test (Permitted -- No Key Required)

### Steps

1. At the Labs menu, type: `4` (Order Lab Test).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Test Name | `Drug Level - Vancomycin Trough` |
| LOINC/Test Code (optional) | `4090-0` |
| Specimen Type (optional) | `Blood` |
| Category | `Chemistry` |

3. Confirm: `Y`

### Expected Result

- `Lab test ordered: [lab-test-ID]`
- **Pharmacist workflow:** Ordering therapeutic drug level monitoring (e.g., vancomycin trough, aminoglycoside levels) is a common pharmacist task.

---

## Scenario 5: Order a Renal Function Panel

### Steps

1. At the Labs menu, type: `4`.
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Test Name | `Basic Metabolic Panel` |
| LOINC/Test Code (optional) | `24323-8` |
| Specimen Type (optional) | `Blood` |
| Category | `Chemistry` |

3. Confirm: `Y`

### Expected Result

- Lab ordered. Pharmacists commonly order BMPs to monitor renal function for dose adjustments.

---

## Scenario 6: Cancel Lab Order

### Steps

1. At the Labs menu, type: `4`.
2. Fill in fields.
3. At confirmation, type: `N`.

### Expected Result

- Lab NOT ordered.

---

## Scenario 7: Lab Results -- No Data

### Steps

1. Select a patient with no lab data.
2. View lab results (option 1).

### Expected Result

- Empty table or "(none)" message.

---

## Scenario 8: Return to Main Menu

### Steps

1. At the Labs menu, type `Q` or `^`.

### Expected Result

- Returns to the Main Menu.
