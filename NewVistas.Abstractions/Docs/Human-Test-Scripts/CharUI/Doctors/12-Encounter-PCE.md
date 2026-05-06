# Encounter / PCE (Patient Care Encounter) -- Physician CharUI Human Test Script

## Prerequisites

- **Login:** DOCTOR1 / Password: `smythVista1`
- **Security Keys Required:** PROVIDER (create encounter), ORES, TIU SIGN, GMRA ALLERGY, GMRV VITALS, GMPL PROBLEM
- **Patient:** Select a patient with demo data loaded.
- **Pre-conditions:** SiloHost and WebServer running. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: List Encounters (Happy Path)

### Steps

1. At the Main Menu, type: `EN` and press Enter.
2. At the Encounter menu, type: `1` (List Encounters).

### Expected Result

- A table displays with columns: #, Date/Time, Category, Location, Status, Dx, Px.
- Shows all encounters for the patient (OPEN, CHECKED OUT, CANCELLED).
- Category codes: A=Ambulatory, T=Telehealth, H=Hospitalization, C=Consult, E=Event, O=Observation, R=Referral.

---

## Scenario 2: View Encounter Detail

### Steps

1. At the Encounter menu, type: `2` (View Encounter Detail).
2. A numbered list of encounters appears.
3. Select an encounter by number (e.g., `1`).

### Expected Result

- The terminal displays:
  ```
  Visit ID: PCE-VISIT-xxxx
  Date/Time: 03/31/2026 10:00
  Category: A (Ambulatory)
  Location: MEDICINE CLINIC
  Status: OPEN

  DIAGNOSES:
    I10    Essential Hypertension    PRIMARY
    E11.9  Type 2 Diabetes Mellitus

  PROCEDURES:
    99214  Office Visit Level 4      Qty: 1

  PROVIDERS:
    SMITH,JOHN A    PRIMARY
  ```
- Returns to the Encounter menu.

---

## Scenario 3: Create a New Encounter -- Ambulatory (Happy Path)

### Steps

1. At the Encounter menu, type: `3` (Create New Encounter).
2. The terminal displays:
   ```
   Service Categories: A=Ambulatory, T=Telehealth, H=Hospitalization,
                       C=Consult, E=Event, O=Observation, R=Referral
   ```
3. Enter the following field-by-field:

| Prompt | Value to Enter |
|--------|----------------|
| Service Category | `A` |
| Location Name | `MEDICINE CLINIC` |
| Visit Date/Time | (press Enter for default: Now) |

4. At the confirmation prompt `Create this encounter?`, type: `Y`.

### Expected Result

- The terminal displays: `Encounter created: [visit-ID]`
- Returns to the Encounter menu.
- Verify by listing encounters (option 1) -- the new encounter appears with Category: A, Status: OPEN.

---

## Scenario 4: Create a Telehealth Encounter

### Steps

1. At the Encounter menu, type: `3` (Create New Encounter).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Service Category | `T` |
| Location Name | `TELEHEALTH CLINIC` |
| Visit Date/Time | (press Enter for default: Now) |

3. Confirm: `Y`

### Expected Result

- Encounter created with Category: T (Telehealth).

---

## Scenario 5: Create a Hospitalization Encounter

### Steps

1. At the Encounter menu, type: `3` (Create New Encounter).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Service Category | `H` |
| Location Name | `WARD-MED-3A` |
| Visit Date/Time | `03/31/2026 08:00` |

3. Confirm: `Y`

### Expected Result

- Encounter created with Category: H (Hospitalization) and the specified date/time.

---

## Scenario 6: Create Encounter -- All Service Categories

### Steps

Repeat Scenario 3 for each service category:

| Category Code | Location Example |
|---------------|------------------|
| `A` | `PRIMARY CARE CLINIC` |
| `T` | `TELEHEALTH CLINIC` |
| `H` | `WARD-ICU-1` |
| `C` | `CARDIOLOGY CONSULT` |
| `E` | `PROCEDURE SUITE` |
| `O` | `OBSERVATION UNIT` |
| `R` | `REFERRAL CLINIC` |

### Expected Result

- Each encounter created with the corresponding service category.

---

## Scenario 7: Cancel Creating an Encounter

### Steps

1. At the Encounter menu, type: `3`.
2. Enter a Service Category and Location.
3. At the confirmation prompt `Create this encounter?`, type: `N`.

### Expected Result

- The encounter is NOT created.
- Returns to the Encounter menu.

---

## Scenario 8: Add a Primary Diagnosis to an Encounter (Happy Path)

### Steps

1. At the Encounter menu, type: `4` (Add Diagnosis to Encounter).
2. A numbered list of OPEN encounters appears.
3. Select an open encounter by number.
4. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| ICD-10 Code | `I10` |
| Diagnosis Description | `Essential Hypertension` |
| Primary diagnosis? (Y/N) | `Y` |

### Expected Result

- The terminal displays: `Diagnosis added.`
- The diagnosis is marked as PRIMARY.
- View the encounter detail (option 2) to verify the diagnosis appears with the PRIMARY label.

---

## Scenario 9: Add a Secondary Diagnosis

### Steps

1. At the Encounter menu, type: `4` (Add Diagnosis to Encounter).
2. Select the same encounter as Scenario 8.
3. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| ICD-10 Code | `E11.9` |
| Diagnosis Description | `Type 2 Diabetes Mellitus without complications` |
| Primary diagnosis? (Y/N) | `N` |

### Expected Result

- Diagnosis added as secondary (IsPrimary = false).
- The encounter now has 2 diagnoses: I10 (PRIMARY) and E11.9 (secondary).

---

## Scenario 10: Add a New Primary Diagnosis (Demotes Existing Primary)

### Steps

1. At the Encounter menu, type: `4` (Add Diagnosis to Encounter).
2. Select the same encounter.
3. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| ICD-10 Code | `J18.9` |
| Diagnosis Description | `Community-Acquired Pneumonia` |
| Primary diagnosis? (Y/N) | `Y` |

### Expected Result

- Diagnosis added. The new diagnosis (J18.9) is now PRIMARY.
- The previous primary (I10) is demoted to secondary (IsPrimary = false).
- View encounter detail to confirm: J18.9 shows PRIMARY, I10 and E11.9 do not.

---

## Scenario 11: Add a Procedure to an Encounter (Happy Path)

### Steps

1. At the Encounter menu, type: `5` (Add Procedure to Encounter).
2. Select an open encounter.
3. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| CPT Code | `99214` |
| Procedure Description | `Office or Other Outpatient Visit, Level 4` |
| Quantity | `1` (or press Enter for default: 1) |

### Expected Result

- The terminal displays: `Procedure added.`
- View encounter detail to see the procedure listed.

---

## Scenario 12: Add Multiple Procedures

### Steps

1. Add a first procedure (as in Scenario 11).
2. Type `5` again, select the same encounter.
3. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| CPT Code | `93000` |
| Procedure Description | `Electrocardiogram, 12-lead` |
| Quantity | `1` |

### Expected Result

- Both procedures appear in the encounter detail.

---

## Scenario 13: Check Out an Encounter (Happy Path)

### Steps

1. At the Encounter menu, type: `6` (Check Out Encounter).
2. A numbered list of OPEN encounters appears.
3. Select an encounter by number.
4. At the confirmation prompt `Check out this encounter?`, type: `Y`.

### Expected Result

- The terminal displays: `Encounter checked out.`
- The encounter status changes from OPEN to CHECKED OUT.
- Verify by listing encounters -- the status column shows CHECKED OUT.

---

## Scenario 14: Cancel Checking Out an Encounter

### Steps

1. At the Encounter menu, type: `6`.
2. Select an open encounter.
3. At the confirmation prompt `Check out this encounter?`, type: `N`.

### Expected Result

- The encounter remains OPEN.
- Returns to the Encounter menu.

---

## Scenario 15: Check Out -- No Open Encounters

### Steps

1. Ensure all encounters for this patient are CHECKED OUT or CANCELLED.
2. At the Encounter menu, type: `6`.

### Expected Result

- Empty list or message indicating no open encounters.
- Returns to the Encounter menu.

---

## Scenario 16: Return to Main Menu

### Steps

1. At the Encounter menu, type `Q` or `^` to return.

### Expected Result

- Returns to the Main Menu with patient context preserved.
