# Problem List -- Nurse CharUI Human Test Script

## Prerequisites

- **Login:** NURSE1 / Password: `smythVista1`
- **Security Keys:** ORELSE, GMRV VITALS, GMRA ALLERGY, GMPL PROBLEM, SD SCHEDULING
- **Patient:** Select a patient with demo data loaded.
- **Pre-conditions:** SiloHost and WebServer running. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: List Active Problems (Happy Path)

### Steps

1. At the Main Menu, type: `PL` and press Enter.
2. At the Problem List menu, type: `1` (List Active Problems).

### Expected Result

- Table displays: #, ICD, Problem, Status, Onset.
- Only ACTIVE problems shown.

---

## Scenario 2: List All Problems

### Steps

1. At the Problem List menu, type: `2` (List All Problems).

### Expected Result

- All problems displayed regardless of status (ACTIVE and INACTIVE).

---

## Scenario 3: Add a New Problem (Happy Path)

### Steps

1. At the Problem List menu, type: `3` (Add New Problem).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Diagnosis | `Fall Risk` |
| ICD-10 Code (optional) | `Z91.81` |
| Priority (A=Acute, C=Chronic) | `A` |
| Date of Onset | `T` (Today) |
| Service Connected? (Y/N) | `N` |
| Comments (optional) | `Patient unsteady on feet, uses walker. Fall prevention protocol initiated.` |

3. At the confirmation prompt `Save this problem?`, type: `Y`.

### Expected Result

- The terminal displays: `Problem added: [problem-ID]`
- Verify by listing active problems -- "Fall Risk" appears.

---

## Scenario 4: Add a Problem -- Nursing-Specific Diagnosis

### Steps

1. At the Problem List menu, type: `3`.
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Diagnosis | `Impaired Skin Integrity` |
| ICD-10 Code (optional) | `L89.90` |
| Priority | `A` |
| Date of Onset | `T-1` (yesterday) |
| Service Connected? | `N` |
| Comments | `Stage 2 pressure ulcer on sacrum. Wound care per protocol.` |

3. Confirm: `Y`

### Expected Result

- Problem added successfully.

---

## Scenario 5: Cancel Adding a Problem

### Steps

1. At the Problem List menu, type: `3`.
2. Fill in fields.
3. At confirmation, type: `N`.

### Expected Result

- Problem NOT saved.

---

## Scenario 6: Inactivate a Problem (Happy Path)

### Steps

1. At the Problem List menu, type: `4` (Inactivate Problem).
2. Select a problem from the active list.
3. Enter Date Resolved: `T` (Today).
4. Confirm: `Y`

### Expected Result

- `Problem inactivated.`
- The problem no longer appears in active list but shows in all problems as INACTIVE.

---

## Scenario 7: Cancel Inactivating a Problem

### Steps

1. At the Problem List menu, type: `4`.
2. Select a problem.
3. Enter date.
4. At confirmation, type: `N`.

### Expected Result

- Problem remains ACTIVE.

---

## Scenario 8: Return to Main Menu

### Steps

1. At the Problem List menu, type `Q` or `^`.

### Expected Result

- Returns to the Main Menu.
