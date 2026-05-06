# Cover Sheet & Reports -- Nurse CharUI Human Test Script

## Prerequisites

- **Login:** NURSE1 / Password: `smythVista1`
- **Security Keys:** ORELSE, GMRV VITALS, GMRA ALLERGY, GMPL PROBLEM, SD SCHEDULING
- **Patient:** Select a patient with demo data loaded.
- **Pre-conditions:** SiloHost and WebServer running. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## PART A: Cover Sheet

## Scenario 1: View Cover Sheet (Happy Path)

### Steps

1. At the Main Menu, type: `CV` and press Enter.

### Expected Result

- Cover Sheet displays all 8 sections:
  1. **Active Problems** -- table of active diagnoses
  2. **Allergies** -- allergen table (or "NKA" if none)
  3. **Active Medications** -- drug/sig/status table
  4. **Recent Vitals** -- latest vitals by type with abnormal flags
  5. **Active Orders** -- pending/active orders
  6. **Recent Notes** -- recent clinical notes
  7. **Active Consults** -- pending consult requests
  8. **Upcoming Appointments** -- future scheduled visits
- Patient banner at top with name, DOB, sex, age, admission status, SC status, CWAD flags.

---

## Scenario 2: Cover Sheet After Recording Vitals

### Steps

1. Record a set of vitals (Main Menu > VT > option 2).
2. Return to Main Menu.
3. View Cover Sheet (CV).

### Expected Result

- The Recent Vitals section shows the vitals just recorded.
- Timestamps match the recording time.

---

## Scenario 3: Cover Sheet After Recording Allergy

### Steps

1. Record a new allergy (Main Menu > AL > option 2).
2. Return to Main Menu.
3. View Cover Sheet (CV).

### Expected Result

- The Allergies section shows the newly recorded allergy.
- CWAD flags in the patient banner include "A" for Allergy.

---

## Scenario 4: Cover Sheet for Empty Patient

### Steps

1. Select a patient with no demo data.
2. View Cover Sheet (CV).

### Expected Result

- All sections show their empty state: "(none)", "No Known Allergies (NKA)", etc.
- No errors.

---

## PART B: Reports

## Scenario 5: Access Reports Menu

### Steps

1. At the Main Menu, type: `RP` and press Enter.

### Expected Result

- Reports menu displays 14 report options (all read-only, no restrictions).

---

## Scenario 6: Health Summary Report

### Steps

1. At the Reports menu, type: `1` (Health Summary).

### Expected Result

- Summary with counts of problems, allergies, medications, vitals, orders, notes, consults, and CWAD/SC status.

---

## Scenario 7: Vital Signs Report

### Steps

1. At the Reports menu, type: `7` (Vital Signs).

### Expected Result

- Formatted vital signs list -- key nursing reference for tracking trends.

---

## Scenario 8: Active Medications Report

### Steps

1. At the Reports menu, type: `4` (Active Medications).

### Expected Result

- Table of active medications -- useful for medication reconciliation.

---

## Scenario 9: Allergies Report

### Steps

1. At the Reports menu, type: `3` (Allergies Report).

### Expected Result

- Table of all allergies with type, severity, and reactions.

---

## Scenario 10: Lab Summary and Abnormal Results Reports

### Steps

1. At the Reports menu, type: `5` (Lab Summary).
2. Review results.
3. Return to Reports menu, type: `6` (Lab Results - Abnormal).

### Expected Result

- Lab Summary: All lab results with values and flags.
- Abnormal Results: Only flagged values displayed.

---

## Scenario 11: Appointments Report

### Steps

1. At the Reports menu, type: `11` (Appointments).

### Expected Result

- Table of all appointments with date, clinic, provider, and status.

---

## Scenario 12: ADT Movements Report

### Steps

1. At the Reports menu, type: `12` (ADT Movements).

### Expected Result

- Table of admission, discharge, and transfer movements.

---

## Scenario 13: Run All 14 Reports for a Patient with Demo Data

### Steps

1. Run each report option (1 through 14) sequentially for a patient with full demo data loaded.

### Expected Result

- Every report displays data without errors.
- No access denied messages for any report.
- All reports are read-only and accessible to nurses.

---

## Scenario 14: Reports for Empty Patient

### Steps

1. Select a patient with no demo data.
2. Run several reports (options 1, 2, 3, 7, 8).

### Expected Result

- Each report shows empty data or "(none)" messages.
- No errors.

---

## Scenario 15: Return to Main Menu from Reports

### Steps

1. At the Reports menu, type `Q` or `^`.

### Expected Result

- Returns to the Main Menu.

---

## PART C: Demographics (Read-Only)

## Scenario 16: View Demographics

### Steps

1. At the Main Menu, type: `DM`.

### Expected Result

- Demographics displayed: Name, Sex, DOB, Age, SSN (masked), Admission Status, Eligibility (SC), CWAD flags, Allergies.
- Read-only, no input prompts.

---

## PART D: System Modules

## Scenario 17: Access System Modules

### Steps

1. At the Main Menu, type: `SM` (System Modules).
2. Select any module option (e.g., `IC` for Infection Control).

### Expected Result

- Message: `This module is not yet implemented in the Character UI. Use the Blazor or WPF interface for this functionality.`
- Returns to System Modules menu.
