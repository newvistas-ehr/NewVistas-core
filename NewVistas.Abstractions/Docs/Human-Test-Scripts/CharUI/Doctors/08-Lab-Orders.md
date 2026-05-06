# Lab Orders & Results -- Physician CharUI Human Test Script

## Prerequisites

- **Login:** DOCTOR1 / Password: `smythVista1`
- **Security Keys:** PROVIDER, ORES, TIU SIGN, GMRA ALLERGY, GMRV VITALS, GMPL PROBLEM
- **Patient:** Select a patient with demo data loaded.
- **Pre-conditions:**
  1. SiloHost and WebServer running.
  2. Demo lab data loaded for the patient: `POST /api/lab/demo/load?patientId={patientId}`
  3. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: View Lab Results (Happy Path)

### Steps

1. At the Main Menu, type: `LA` and press Enter.
2. At the Labs menu, type: `1` (View Lab Results).

### Expected Result

- A table displays with columns: #, Test, Result, Units, Flag, Status, Date.
- Demo data includes 3 panels: CBC (5 tests), BMP (4 tests), LFT (3 tests).
- Abnormal values display with flag indicators (e.g., `*H*` for high, `*L*` for low).
- Example rows:
  ```
  1  WBC           7.2     10^3/uL              COMPLETED   03/31/2026
  2  Hemoglobin    14.5    g/dL                  COMPLETED   03/31/2026
  3  ALT           85      U/L        *H*        COMPLETED   03/31/2026
  ```

---

## Scenario 2: View Lab Summary

### Steps

1. At the Labs menu, type: `2` (View Lab Summary).

### Expected Result

- A summary view showing the latest value for each test type (grouped by LOINC code).
- Includes trend data (last 3 values per test type).
- Shows test name, most recent result, units, and date.

---

## Scenario 3: View Abnormal Results Only

### Steps

1. At the Labs menu, type: `3` (View Abnormal Results).

### Expected Result

- Only lab results with abnormal flags are displayed.
- From demo data, at least 2 abnormal LFT results should appear (e.g., elevated ALT, AST).
- Normal results are excluded from this view.

---

## Scenario 4: View Results -- No Lab Data

### Steps

1. Select a patient with no lab data loaded.
2. Navigate to Labs (LA) and select option 1.

### Expected Result

- Empty table or "(none)" message.
- No errors displayed.

---

## Scenario 5: Order a Lab Test -- Chemistry (Happy Path)

### Steps

1. At the Labs menu, type: `4` (Order Lab Test).
2. Enter the following field-by-field:

| Prompt | Value to Enter |
|--------|----------------|
| Test Name | `Basic Metabolic Panel` |
| LOINC/Test Code (optional) | `24323-8` |
| Specimen Type (optional) | `Blood` |
| Category (Chemistry, Hematology, Micro, Other) | `Chemistry` |

3. At the confirmation prompt `Order this lab test?`, type: `Y`.

### Expected Result

- The terminal displays: `Lab test ordered: [lab-test-ID]`
- Returns to the Labs menu.

---

## Scenario 6: Order a Lab Test -- Hematology

### Steps

1. At the Labs menu, type: `4` (Order Lab Test).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Test Name | `Complete Blood Count` |
| LOINC/Test Code (optional) | `58410-2` |
| Specimen Type (optional) | `Blood` |
| Category | `Hematology` |

3. Confirm: `Y`

### Expected Result

- Lab test ordered with Category = Hematology.

---

## Scenario 7: Order a Lab Test -- Microbiology

### Steps

1. At the Labs menu, type: `4` (Order Lab Test).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Test Name | `Blood Culture` |
| LOINC/Test Code (optional) | `600-7` |
| Specimen Type (optional) | `Blood` |
| Category | `Micro` |

3. Confirm: `Y`

### Expected Result

- Lab test ordered with Category = Micro.

---

## Scenario 8: Order a Lab Test -- Minimal Fields

### Steps

1. At the Labs menu, type: `4` (Order Lab Test).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Test Name | `Hemoglobin A1c` |
| LOINC/Test Code (optional) | (press Enter to skip) |
| Specimen Type (optional) | (press Enter to skip) |
| Category | (press Enter for default: Chemistry) |

3. Confirm: `Y`

### Expected Result

- Lab test ordered with defaults. No LOINC code or specimen type.

---

## Scenario 9: Cancel Ordering a Lab Test

### Steps

1. At the Labs menu, type: `4` (Order Lab Test).
2. Enter Test Name: `Test Lab`.
3. Continue through remaining fields.
4. At the confirmation prompt `Order this lab test?`, type: `N`.

### Expected Result

- The lab test is NOT ordered.
- Returns to the Labs menu.

---

## Scenario 10: Return to Main Menu

### Steps

1. At the Labs menu, type `Q` or `^` to return.

### Expected Result

- Returns to the Main Menu with patient context preserved.
