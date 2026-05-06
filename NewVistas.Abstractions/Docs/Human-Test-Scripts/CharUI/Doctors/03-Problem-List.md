# Problem List Management -- Physician CharUI Human Test Script

## Prerequisites

- **Login:** DOCTOR1 / Password: `smythVista1`
- **Security Keys:** PROVIDER, ORES, TIU SIGN, GMRA ALLERGY, GMRV VITALS, GMPL PROBLEM
- **Patient:** Select a patient with demo data loaded.
- **Pre-conditions:** SiloHost and WebServer running. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: List Active Problems (Happy Path)

### Steps

1. At the Main Menu, type: `PL` and press Enter.
2. At the Problem List menu, type: `1` (List Active Problems).

### Expected Result

- A table displays with columns: #, ICD, Problem, Status, Onset
- Only problems with Status = ACTIVE appear.
- If demo data is loaded, at least 1 active problem should be listed.

---

## Scenario 2: List All Problems (Active + Inactive)

### Steps

1. At the Problem List menu, type: `2` (List All Problems).

### Expected Result

- A table displays all problems regardless of status.
- Both ACTIVE and INACTIVE problems appear.
- Inactive problems show their resolved date (if set).

---

## Scenario 3: Add a New Problem (Happy Path)

### Steps

1. At the Problem List menu, type: `3` (Add New Problem).
2. At the prompts, enter the following field-by-field:

| Prompt | Value to Enter |
|--------|----------------|
| Diagnosis | `Essential Hypertension` |
| ICD-10 Code (optional) | `I10` |
| Priority (A=Acute, C=Chronic) | `C` |
| Date of Onset | `T` (VistA shorthand for Today) |
| Service Connected? (Y/N) | `N` |
| Comments (optional) | `Newly diagnosed, start lifestyle modifications` |

3. At the confirmation prompt `Save this problem?`, type: `Y`.

### Expected Result

- The terminal displays: `Problem added: [problem-ID]`
- Return to the Problem List menu.
- Verify by selecting option `1` (List Active Problems) -- the new problem "Essential Hypertension" with ICD I10 appears in the list.

---

## Scenario 4: Add a New Problem -- Minimal Fields

### Steps

1. At the Problem List menu, type: `3` (Add New Problem).
2. Enter the following (accept defaults for optional fields):

| Prompt | Value to Enter |
|--------|----------------|
| Diagnosis | `Type 2 Diabetes Mellitus` |
| ICD-10 Code (optional) | (press Enter to skip) |
| Priority (A=Acute, C=Chronic) | (press Enter for default: C) |
| Date of Onset | (press Enter for default: Today) |
| Service Connected? (Y/N) | (press Enter for default: N) |
| Comments (optional) | (press Enter to skip) |

3. At the confirmation prompt `Save this problem?`, type: `Y`.

### Expected Result

- The terminal displays: `Problem added: [problem-ID]`
- The problem is saved with default values: Priority=Chronic, Onset=Today, SC=No, no ICD code, no comments.

---

## Scenario 5: Add a New Problem -- Acute Priority

### Steps

1. At the Problem List menu, type: `3` (Add New Problem).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Diagnosis | `Acute Bronchitis` |
| ICD-10 Code (optional) | `J20.9` |
| Priority (A=Acute, C=Chronic) | `A` |
| Date of Onset | `T-3` (3 days ago) |
| Service Connected? (Y/N) | `N` |
| Comments (optional) | `Productive cough x 3 days, no fever` |

3. Confirm: `Y`

### Expected Result

- Problem added with Acute priority and onset date 3 days in the past.

---

## Scenario 6: Add a New Problem -- Service Connected

### Steps

1. Type: `3` (Add New Problem).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Diagnosis | `Post-Traumatic Stress Disorder` |
| ICD-10 Code (optional) | `F43.10` |
| Priority (A=Acute, C=Chronic) | `C` |
| Date of Onset | `01/15/2020` |
| Service Connected? (Y/N) | `Y` |
| Comments (optional) | `Combat-related PTSD, receiving ongoing therapy` |

3. Confirm: `Y`

### Expected Result

- Problem added with ServiceConnected = Yes.

---

## Scenario 7: Cancel Adding a Problem

### Steps

1. At the Problem List menu, type: `3` (Add New Problem).
2. Fill in the Diagnosis: `Test Problem`
3. Continue through remaining fields with any values.
4. At the confirmation prompt `Save this problem?`, type: `N`.

### Expected Result

- The problem is NOT saved.
- Returns to the Problem List menu.
- Verify the "Test Problem" does NOT appear in the active or all problems list.

---

## Scenario 8: Inactivate a Problem (Happy Path)

### Steps

1. At the Problem List menu, type: `4` (Inactivate Problem).
2. The terminal displays a numbered list of active problems:
   ```
   Active Problems:
   1  I10    Essential Hypertension
   2  E11.9  Type 2 Diabetes Mellitus
   ```
3. At the prompt `Select problem (1-N)`, type: `1`.
4. At the prompt `Date Resolved`, type: `T` (Today) or press Enter for default.
5. At the confirmation prompt `Inactivate 'Essential Hypertension'?`, type: `Y`.

### Expected Result

- The terminal displays: `Problem inactivated.`
- Verify by listing active problems (option 1) -- "Essential Hypertension" no longer appears.
- Verify by listing all problems (option 2) -- "Essential Hypertension" appears with INACTIVE status.

---

## Scenario 9: Cancel Inactivating a Problem

### Steps

1. Type: `4` (Inactivate Problem).
2. Select a problem from the list.
3. Enter a date resolved.
4. At the confirmation prompt, type: `N`.

### Expected Result

- The problem remains ACTIVE.
- Returns to the Problem List menu.

---

## Scenario 10: Inactivate When No Active Problems Exist

### Steps

1. Ensure all problems for the patient are already inactivated.
2. Type: `4` (Inactivate Problem).

### Expected Result

- The terminal displays an empty list or a message indicating no active problems are available.
- Returns to the Problem List menu without prompting for selection.

---

## Scenario 11: Return to Main Menu

### Steps

1. At the Problem List menu, type `Q` or `^` to return.

### Expected Result

- Returns to the Main Menu with the patient context preserved.
