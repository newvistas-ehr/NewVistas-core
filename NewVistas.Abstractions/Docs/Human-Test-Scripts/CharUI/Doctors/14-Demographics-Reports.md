# Demographics & Reports -- Physician CharUI Human Test Script

## Prerequisites

- **Login:** DOCTOR1 / Password: `smythVista1`
- **Security Keys:** PROVIDER, ORES, TIU SIGN, GMRA ALLERGY, GMRV VITALS, GMPL PROBLEM
- **Patient:** Select a patient with demo data loaded.
- **Pre-conditions:** SiloHost and WebServer running. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## PART A: Demographics

## Scenario 1: View Patient Demographics (Happy Path)

### Steps

1. At the Main Menu, type: `DM` and press Enter.

### Expected Result

- The terminal displays read-only demographics:
  ```
  PATIENT DEMOGRAPHICS

  Patient Name: SMITH,JOHN
  Sex: M
  Date of Birth: 01/15/1955
  Age: 71
  SSN: xxx-xx-1234

  ---
  ADMISSION STATUS
  ---
  Admitted: YES (or NO)
  Room/Bed: 3A-12 (if admitted)
  Location: WARD-MED-3A (if admitted)

  ---
  ELIGIBILITY
  ---
  Service Connected: YES (or NO)
  SC Percent: 30 (if applicable)
  Veteran: YES

  ---
  CWAD FLAGS
  ---
  Crisis Note: YES/No
  Warning: YES/No
  Allergy: YES/No
  Adv. Directive: YES/No

  ---
  ALLERGIES
  ---
  PENICILLIN (Severe) - RASH, HIVES, ANAPHYLAXIS
  SHELLFISH (Moderate) - HIVES, THROAT SWELLING
  ```
- If no allergies: "No Known Allergies (NKA)"
- Returns to the Main Menu after display (read-only, no input prompts).

---

## Scenario 2: Demographics for Non-Admitted Patient

### Steps

1. Select a patient who is NOT currently admitted.
2. At the Main Menu, type: `DM`.

### Expected Result

- Admission Status section shows:
  ```
  Admitted: NO
  Room/Bed: (blank)
  Location: (blank)
  ```

---

## Scenario 3: Demographics for Non-Service-Connected Patient

### Steps

1. Select a patient who is NOT service-connected.
2. At the Main Menu, type: `DM`.

### Expected Result

- Eligibility section shows:
  ```
  Service Connected: NO
  SC Percent: (blank or 0)
  ```

---

## PART B: Reports

## Scenario 4: Access Reports Menu

### Steps

1. At the Main Menu, type: `RP` and press Enter.

### Expected Result

- The Reports menu displays 14 report options:
  ```
  1  Health Summary (Cover Sheet)
  2  Problem List
  3  Allergies Report
  4  Active Medications
  5  Lab Summary
  6  Lab Results (Abnormal)
  7  Vital Signs
  8  Active Orders
  9  Consults
  10 Clinical Notes
  11 Appointments
  12 ADT Movements
  13 Radiology
  14 Surgery
  ```

---

## Scenario 5: Health Summary Report

### Steps

1. At the Reports menu, type: `1` (Health Summary).

### Expected Result

- Displays a summary with counts and key indicators:
  - Number of active problems
  - Number of allergies
  - Number of active medications
  - Number of recent vitals
  - Number of active orders
  - Number of notes
  - Number of consults
  - CWAD flags status
  - SC status

---

## Scenario 6: Problem List Report

### Steps

1. At the Reports menu, type: `2` (Problem List).

### Expected Result

- Full table of all problems (active and inactive) with ICD codes, status, onset dates.

---

## Scenario 7: Allergies Report

### Steps

1. At the Reports menu, type: `3` (Allergies Report).

### Expected Result

- Table showing: Allergen, Type, Severity, Reactions.
- Or "No Known Allergies (NKA)" if none documented.

---

## Scenario 8: Active Medications Report

### Steps

1. At the Reports menu, type: `4` (Active Medications).

### Expected Result

- Table of active medications with drug name, sig, status, fill date, refills.

---

## Scenario 9: Lab Summary Report

### Steps

1. At the Reports menu, type: `5` (Lab Summary).

### Expected Result

- Lab test results with values, units, and abnormal flags.
- Abnormal values marked with `*H*` or `*L*`.

---

## Scenario 10: Abnormal Lab Results Report

### Steps

1. At the Reports menu, type: `6` (Lab Results - Abnormal).

### Expected Result

- Only abnormal lab results displayed.
- If no abnormal results: empty or "(none)" message.

---

## Scenario 11: Vital Signs Report

### Steps

1. At the Reports menu, type: `7` (Vital Signs).

### Expected Result

- Formatted vital signs list with type, value, units, date/time, and abnormal flags.

---

## Scenario 12: Active Orders Report

### Steps

1. At the Reports menu, type: `8` (Active Orders).

### Expected Result

- Table of active orders with order text, type, status, date.

---

## Scenario 13: Consults Report

### Steps

1. At the Reports menu, type: `9` (Consults).

### Expected Result

- Table of consult requests with service, status, urgency, date, requesting provider.

---

## Scenario 14: Clinical Notes Report

### Steps

1. At the Reports menu, type: `10` (Clinical Notes).

### Expected Result

- Paged list of all notes with type, author, status, date.
- If many notes exist, the list is paged for readability.

---

## Scenario 15: Appointments Report

### Steps

1. At the Reports menu, type: `11` (Appointments).

### Expected Result

- Table of scheduled appointments with date/time, clinic, provider, status.

---

## Scenario 16: ADT Movements Report

### Steps

1. At the Reports menu, type: `12` (ADT Movements).

### Expected Result

- Table of admissions, discharges, and transfers with type, date, ward, room-bed, physician, status.

---

## Scenario 17: Radiology Report

### Steps

1. At the Reports menu, type: `13` (Radiology).

### Expected Result

- Table of radiology studies with procedure, type, status, date, provider.

---

## Scenario 18: Surgery Report

### Steps

1. At the Reports menu, type: `14` (Surgery).

### Expected Result

- Table of surgical procedures with procedure name, date, surgeon, status.

---

## Scenario 19: Reports with No Data

### Steps

1. Select a patient with no demo data loaded.
2. Run through each of the 14 report options.

### Expected Result

- Each report shows an empty table or "(none)" message.
- No errors are displayed.

---

## Scenario 20: Return to Main Menu from Reports

### Steps

1. At the Reports menu, type `Q` or `^` to return.

### Expected Result

- Returns to the Main Menu with patient context preserved.
