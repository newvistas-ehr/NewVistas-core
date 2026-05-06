# ADT (Admission/Discharge/Transfer) -- Physician CharUI Human Test Script

## Prerequisites

- **Login:** DOCTOR1 / Password: `smythVista1`
- **Security Keys:** PROVIDER, ORES, TIU SIGN, GMRA ALLERGY, GMRV VITALS, GMPL PROBLEM
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

- A table displays with columns: #, Type, Date/Time, Ward, Room-Bed, Physician, Status.
- Shows all ADT movements: ADMISSION, TRANSFER, DISCHARGE.
- Example:
  ```
  #  Type         Date/Time            Ward         Room-Bed  Physician       Status
  1  ADMISSION    03/28/2026 14:00     WARD-MED-3A  3A-12     SMITH,JOHN A    ADMITTED
  2  TRANSFER     03/30/2026 08:00     WARD-ICU-1   ICU-4     SMITH,JOHN A    TRANSFERRED
  ```

---

## Scenario 2: Record an Admission (Happy Path)

### Steps

1. At the ADT menu, type: `2` (Record Admission).
2. Enter the following field-by-field:

| Prompt | Value to Enter |
|--------|----------------|
| Ward Location | `WARD-MED-3A` |
| Room-Bed (e.g., 3A-12) | `3A-12` |
| Treating Specialty (optional) | `Internal Medicine` |
| Attending Physician | `SMITH,JOHN A` |
| Admission Diagnosis | `Acute exacerbation of CHF, I50.9` |
| Comments (optional) | `Admitted from ED. IV furosemide started.` |

3. At the confirmation prompt `Record this admission?`, type: `Y`.

### Expected Result

- The terminal displays: `Admission recorded: [movement-ID]`
- Returns to the ADT menu.
- Verify by listing movements (option 1) -- new ADMISSION appears with Status: ADMITTED.

---

## Scenario 3: Record Admission -- Minimal Fields

### Steps

1. At the ADT menu, type: `2`.
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Ward Location | `WARD-SURG-2C` |
| Room-Bed | `2C-8` |
| Treating Specialty (optional) | (press Enter to skip) |
| Attending Physician | `SURGEON1` |
| Admission Diagnosis | `Right knee osteoarthritis for TKA` |
| Comments (optional) | (press Enter to skip) |

3. Confirm: `Y`

### Expected Result

- Admission recorded with no treating specialty or comments.

---

## Scenario 4: Cancel Recording an Admission

### Steps

1. At the ADT menu, type: `2`.
2. Enter ward and other fields.
3. At the confirmation prompt `Record this admission?`, type: `N`.

### Expected Result

- The admission is NOT recorded.
- Returns to the ADT menu.

---

## Scenario 5: Record a Discharge (Happy Path)

### Steps

1. Pre-condition: An active admission or transfer must exist (Status: ADMITTED or TRANSFERRED).
2. At the ADT menu, type: `3` (Record Discharge).
3. A numbered list of active admissions/transfers appears.
4. Select one by number.
5. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Discharge Diagnosis (optional) | `CHF exacerbation, resolved. EF 35%.` |
| Disposition (optional) | `HOME WITH HOME HEALTH` |
| Comments (optional) | `Patient stable. Follow-up cardiology in 1 week.` |

6. At the confirmation prompt `Record this discharge?`, type: `Y`.

### Expected Result

- The terminal displays: `Discharge recorded.`
- The movement status changes to DISCHARGED.
- Verify by listing movements -- the discharge appears in the movement list.

---

## Scenario 6: Record Discharge -- Minimal Fields

### Steps

1. Select an active admission.
2. At the ADT menu, type: `3`.
3. Select the admission.
4. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Discharge Diagnosis (optional) | (press Enter to skip) |
| Disposition (optional) | (press Enter to skip) |
| Comments (optional) | (press Enter to skip) |

5. Confirm: `Y`

### Expected Result

- Discharge recorded with no diagnosis, disposition, or comments.

---

## Scenario 7: Cancel Recording a Discharge

### Steps

1. At the ADT menu, type: `3`.
2. Select an active admission.
3. Enter some fields.
4. At the confirmation prompt `Record this discharge?`, type: `N`.

### Expected Result

- The admission remains active.
- Returns to the ADT menu.

---

## Scenario 8: Record a Transfer (Happy Path)

### Steps

1. Pre-condition: An active admission or transfer must exist.
2. At the ADT menu, type: `4` (Record Transfer).
3. A numbered list of active admissions/transfers appears.
4. Select one by number.
5. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| To Ward | `WARD-ICU-1` |
| To Room-Bed | `ICU-4` |
| To Specialty (optional) | `Critical Care Medicine` |
| Attending Physician (optional) | `DOCTOR2` |
| Comments (optional) | `Transferred for ICU-level monitoring. Hemodynamic instability.` |

6. At the confirmation prompt `Record this transfer?`, type: `Y`.

### Expected Result

- The terminal displays: `Transfer recorded.`
- A new TRANSFER movement is created (separate record from the original admission).
- Verify by listing movements -- both the original admission and the new transfer appear.

---

## Scenario 9: Record Transfer -- Minimal Fields

### Steps

1. Select an active admission.
2. At the ADT menu, type: `4`.
3. Select the admission.
4. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| To Ward | `WARD-MED-4B` |
| To Room-Bed | `4B-6` |
| To Specialty (optional) | (press Enter to skip) |
| Attending Physician (optional) | (press Enter to skip) |
| Comments (optional) | (press Enter to skip) |

5. Confirm: `Y`

### Expected Result

- Transfer recorded with only the required ward and room-bed.

---

## Scenario 10: Cancel Recording a Transfer

### Steps

1. At the ADT menu, type: `4`.
2. Select an active admission.
3. Fill in To Ward.
4. At the confirmation prompt `Record this transfer?`, type: `N`.

### Expected Result

- No transfer recorded.
- Returns to the ADT menu.

---

## Scenario 11: Discharge/Transfer -- No Active Admissions

### Steps

1. Ensure no active admissions exist for the patient.
2. At the ADT menu, type: `3` (Record Discharge) or `4` (Record Transfer).

### Expected Result

- Empty list or message indicating no active admissions.
- Returns to the ADT menu.

---

## Scenario 12: Return to Main Menu

### Steps

1. At the ADT menu, type `Q` or `^` to return.

### Expected Result

- Returns to the Main Menu with patient context preserved.
