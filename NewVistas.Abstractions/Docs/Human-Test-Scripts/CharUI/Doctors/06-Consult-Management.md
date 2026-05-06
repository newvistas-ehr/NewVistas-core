# Consult Management -- Physician CharUI Human Test Script

## Prerequisites

- **Login:** DOCTOR1 / Password: `smythVista1`
- **Security Keys:** PROVIDER, ORES, TIU SIGN, GMRA ALLERGY, GMRV VITALS, GMPL PROBLEM
- **Patient:** Select a patient with demo data loaded.
- **Pre-conditions:** SiloHost and WebServer running. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: List Active Consults (Happy Path)

### Steps

1. At the Main Menu, type: `CO` and press Enter.
2. At the Consults menu, type: `1` (List Active Consults).

### Expected Result

- A table displays with columns: #, To Service, Status, Urgency, Date, Requesting.
- Only consults with active statuses (PENDING, ACTIVE, SCHEDULED) appear.

---

## Scenario 2: List All Consults

### Steps

1. At the Consults menu, type: `2` (List All Consults).

### Expected Result

- A table displays all consults regardless of status (PENDING, ACTIVE, COMPLETED, CANCELLED).
- More rows than the active-only view.

---

## Scenario 3: View Consult Detail

### Steps

1. At the Consults menu, type: `3` (View Consult Detail).
2. A numbered list of consults appears.
3. Select a consult by number (e.g., `1`).

### Expected Result

- The terminal displays the full consult detail:
  ```
  To Service: CARDIOLOGY
  From Service: MEDICINE
  Status: PENDING
  Urgency: Routine
  Date Requested: 03/31/2026 10:00
  Requesting Provider: SMITH,JOHN A
  Attention: (if specified)
  Provisional Dx: Essential Hypertension
  ---
  Reason for Request:
  New onset systolic murmur detected on physical exam.
  Requesting echocardiogram and cardiology evaluation.
  ```
- Returns to the Consults menu.

---

## Scenario 4: Request a New Consult -- Routine (Happy Path)

### Steps

1. At the Consults menu, type: `4` (Request New Consult).
2. Enter the following field-by-field:

| Prompt | Value to Enter |
|--------|----------------|
| To Service (e.g., CARDIOLOGY) | `CARDIOLOGY` |
| Urgency (Routine, STAT, ASAP) | `Routine` |
| Reason for Request | `New onset systolic murmur, grade II/VI, best heard at apex. No prior cardiac history. Requesting echocardiogram and evaluation.` |
| Provisional Diagnosis (optional) | `Heart murmur R01.1` |
| Attention Provider (optional) | `DOCTOR3` |

3. At the confirmation prompt `Submit this consult request?`, type: `Y`.

### Expected Result

- The terminal displays: `Consult requested: [consult-ID]`
- Returns to the Consults menu.
- Verify by listing active consults (option 1) -- the CARDIOLOGY consult appears with Status: PENDING, Urgency: Routine.

---

## Scenario 5: Request a STAT Consult

### Steps

1. At the Consults menu, type: `4` (Request New Consult).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| To Service | `PULMONOLOGY` |
| Urgency | `STAT` |
| Reason for Request | `Acute respiratory distress, SpO2 88% on room air, bilateral rales. Urgent pulmonary evaluation needed.` |
| Provisional Diagnosis (optional) | `Acute respiratory failure J96.00` |
| Attention Provider (optional) | (press Enter to skip) |

3. Confirm: `Y`

### Expected Result

- Consult requested with Urgency = STAT.

---

## Scenario 6: Request a Consult -- Minimal Fields

### Steps

1. At the Consults menu, type: `4` (Request New Consult).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| To Service | `DERMATOLOGY` |
| Urgency | (press Enter for default: Routine) |
| Reason for Request | `Suspicious skin lesion on left forearm, evaluate for biopsy.` |
| Provisional Diagnosis (optional) | (press Enter to skip) |
| Attention Provider (optional) | (press Enter to skip) |

3. Confirm: `Y`

### Expected Result

- Consult requested with defaults applied. No provisional diagnosis or attention provider.

---

## Scenario 7: Cancel Requesting a Consult

### Steps

1. At the Consults menu, type: `4` (Request New Consult).
2. Fill in To Service: `ORTHOPEDICS` and Reason: `Test consult`.
3. At the confirmation prompt `Submit this consult request?`, type: `N`.

### Expected Result

- The consult is NOT submitted.
- Returns to the Consults menu.

---

## Scenario 8: Complete a Consult (Happy Path)

### Steps

1. Pre-condition: An active/pending consult must exist for this patient.
2. At the Consults menu, type: `5` (Complete Consult).
3. A numbered list of active consults appears.
4. Select the consult by number.
5. The terminal displays: `Enter result note text:` (multiline input)
6. Type the result:
   ```
   CONSULT RESULT: Cardiology Evaluation

   Echocardiogram performed. LVEF 60%, no significant valvular disease.
   Mild mitral regurgitation noted, likely physiologic.

   IMPRESSION: Benign flow murmur. No structural heart disease.

   RECOMMENDATIONS:
   1. No further cardiac workup needed at this time
   2. Repeat echo in 2 years if symptoms develop
   3. Continue current antihypertensive regimen
   ```
7. End multiline input.
8. At the confirmation prompt `Complete this consult?`, type: `Y`.

### Expected Result

- The terminal displays: `Consult completed.`
- Verify by listing all consults (option 2) -- the consult now shows Status: COMPLETED.
- The result text is stored as the consult result.

---

## Scenario 9: Cancel Completing a Consult

### Steps

1. At the Consults menu, type: `5` (Complete Consult).
2. Select an active consult.
3. Enter result text.
4. At the confirmation prompt `Complete this consult?`, type: `N`.

### Expected Result

- The consult remains in its current status (PENDING or ACTIVE).
- Returns to the Consults menu.

---

## Scenario 10: Complete Consult -- No Active Consults

### Steps

1. Ensure all consults for this patient are already completed or no consults exist.
2. At the Consults menu, type: `5` (Complete Consult).

### Expected Result

- Empty list or message indicating no active consults.
- Returns to the Consults menu.

---

## Scenario 11: Return to Main Menu

### Steps

1. At the Consults menu, type `Q` or `^` to return.

### Expected Result

- Returns to the Main Menu with patient context preserved.
