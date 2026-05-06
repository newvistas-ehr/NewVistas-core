# ADT Management -- Nurse CharUI Human Test Script

## Prerequisites

- **Login:** NURSE1 / Password: `smythVista1`
- **Security Keys:** ORELSE, GMRV VITALS, GMRA ALLERGY, GMPL PROBLEM, SD SCHEDULING
- **Patient:** Select a patient with demo data loaded.
- **Pre-conditions:**
  1. SiloHost and WebServer running.
  2. Demo ADT data loaded: `POST /api/adt/demo/load?patientId={patientId}`
  3. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: List ADT Movements (Happy Path)

### Steps

1. At the Main Menu, type: `AT` and press Enter.
2. At the ADT menu, type: `1` (List Movements).

### Expected Result

- Table displays: #, Type, Date/Time, Ward, Room-Bed, Physician, Status.
- Shows ADMISSION, TRANSFER, and DISCHARGE movements.

---

## Scenario 2: Record an Admission (Happy Path)

### Steps

1. At the ADT menu, type: `2` (Record Admission).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Ward Location | `WARD-MED-3A` |
| Room-Bed (e.g., 3A-12) | `3A-8` |
| Treating Specialty (optional) | `Medical-Surgical Nursing` |
| Attending Physician | `DOCTOR1` |
| Admission Diagnosis | `Pneumonia, Community Acquired` |
| Comments (optional) | `Admitted from ED. O2 via nasal cannula 2L. IV antibiotics started.` |

3. Confirm: `Y`

### Expected Result

- `Admission recorded: [movement-ID]`
- Verify in movements list -- ADMISSION appears.

---

## Scenario 3: Record Admission -- Minimal Fields

### Steps

1. At the ADT menu, type: `2`.
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Ward Location | `WARD-OBS-1` |
| Room-Bed | `OBS-3` |
| Treating Specialty | (Enter to skip) |
| Attending Physician | `DOCTOR2` |
| Admission Diagnosis | `Chest pain, observation` |
| Comments | (Enter to skip) |

3. Confirm: `Y`

### Expected Result

- Admission recorded with minimal data.

---

## Scenario 4: Cancel Recording an Admission

### Steps

1. At the ADT menu, type: `2`.
2. Fill in fields.
3. At confirmation, type: `N`.

### Expected Result

- Admission NOT recorded.

---

## Scenario 5: Record a Discharge (Happy Path)

### Steps

1. Pre-condition: Active admission exists.
2. At the ADT menu, type: `3` (Record Discharge).
3. Select the active admission.
4. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Discharge Diagnosis (optional) | `Pneumonia, resolved` |
| Disposition (optional) | `HOME` |
| Comments (optional) | `Patient ambulatory, tolerating PO diet. Discharge instructions provided.` |

5. Confirm: `Y`

### Expected Result

- `Discharge recorded.`
- Movement status changes to DISCHARGED.

---

## Scenario 6: Record Discharge -- Minimal Fields

### Steps

1. Select an active admission.
2. At the ADT menu, type: `3`.
3. Select the admission.
4. Skip all optional fields.
5. Confirm: `Y`

### Expected Result

- Discharge recorded with no optional data.

---

## Scenario 7: Record a Transfer (Happy Path)

### Steps

1. Pre-condition: Active admission exists.
2. At the ADT menu, type: `4` (Record Transfer).
3. Select the active admission.
4. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| To Ward | `WARD-ICU-1` |
| To Room-Bed | `ICU-2` |
| To Specialty (optional) | `Critical Care` |
| Attending Physician (optional) | `DOCTOR3` |
| Comments (optional) | `Rapid response called. Deteriorating respiratory status. Requires ICU monitoring.` |

5. Confirm: `Y`

### Expected Result

- `Transfer recorded.`
- New TRANSFER movement created.

---

## Scenario 8: Record Transfer -- Ward to Ward

### Steps

1. Select an active admission.
2. At the ADT menu, type: `4`.
3. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| To Ward | `WARD-MED-4B` |
| To Room-Bed | `4B-1` |
| To Specialty | (Enter to skip) |
| Attending Physician | (Enter to skip) |
| Comments | `Bed management transfer, no change in clinical status.` |

4. Confirm: `Y`

### Expected Result

- Transfer recorded with only ward/room-bed and comment.

---

## Scenario 9: Cancel a Transfer

### Steps

1. At the ADT menu, type: `4`.
2. Select an admission.
3. Fill in To Ward.
4. At confirmation, type: `N`.

### Expected Result

- Transfer NOT recorded.

---

## Scenario 10: Discharge/Transfer -- No Active Admissions

### Steps

1. Ensure no active admissions exist.
2. Type: `3` or `4`.

### Expected Result

- Empty list, no admissions available for discharge/transfer.

---

## Scenario 11: Return to Main Menu

### Steps

1. At the ADT menu, type `Q` or `^`.

### Expected Result

- Returns to the Main Menu.
