# Surgery Scheduling -- Physician CharUI Human Test Script

## Prerequisites

- **Login:** DOCTOR1 / Password: `smythVista1`
- **Security Keys:** PROVIDER, ORES, TIU SIGN, GMRA ALLERGY, GMRV VITALS, GMPL PROBLEM
- **Patient:** Select a patient with demo data loaded.
- **Pre-conditions:** SiloHost and WebServer running. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: List Surgeries (Happy Path)

### Steps

1. At the Main Menu, type: `SU` and press Enter.
2. At the Surgery menu, type: `1` (List Surgeries).

### Expected Result

- A table displays with columns: #, Procedure, Date, Surgeon, Status.
- Shows all surgeries for the patient (SCHEDULED, COMPLETED, CANCELLED).

---

## Scenario 2: Schedule a New Surgery (Happy Path)

### Steps

1. At the Surgery menu, type: `2` (Schedule Surgery).
2. Enter the following field-by-field:

| Prompt | Value to Enter |
|--------|----------------|
| Principal Procedure | `Right Total Knee Arthroplasty` |
| CPT Code (optional) | `27447` |
| Date of Operation | `04/15/2026` |
| Surgeon Name | `SMITH,JOHN A` |
| Anesthesia Technique (General, Spinal, Local, MAC) | `Spinal` |
| Surgical Specialty (optional) | `Orthopedic Surgery` |
| Pre-Op Diagnosis (optional) | `Right knee osteoarthritis, M17.11` |
| Comments (optional) | `Patient consented. Pre-op labs ordered. NPO after midnight.` |

3. At the confirmation prompt `Schedule this surgery?`, type: `Y`.

### Expected Result

- The terminal displays: `Surgery scheduled: [surgery-ID]`
- Verify by listing surgeries (option 1) -- "Right Total Knee Arthroplasty" appears with Status: SCHEDULED, Date: 04/15/2026.

---

## Scenario 3: Schedule Surgery -- General Anesthesia

### Steps

1. At the Surgery menu, type: `2` (Schedule Surgery).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Principal Procedure | `Laparoscopic Cholecystectomy` |
| CPT Code (optional) | `47562` |
| Date of Operation | `04/20/2026` |
| Surgeon Name | `SMITH,JOHN A` |
| Anesthesia Technique | `General` (or press Enter for default) |
| Surgical Specialty (optional) | `General Surgery` |
| Pre-Op Diagnosis (optional) | `Cholelithiasis K80.20` |
| Comments (optional) | (press Enter to skip) |

3. Confirm: `Y`

### Expected Result

- Surgery scheduled with Anesthesia = General.

---

## Scenario 4: Schedule Surgery -- Minimal Fields

### Steps

1. At the Surgery menu, type: `2` (Schedule Surgery).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Principal Procedure | `Skin Biopsy Left Forearm` |
| CPT Code (optional) | (press Enter to skip) |
| Date of Operation | `T+7` (7 days from today) |
| Surgeon Name | `SMITH,JOHN A` |
| Anesthesia Technique | `Local` |
| Surgical Specialty (optional) | (press Enter to skip) |
| Pre-Op Diagnosis (optional) | (press Enter to skip) |
| Comments (optional) | (press Enter to skip) |

3. Confirm: `Y`

### Expected Result

- Surgery scheduled with minimal information. No CPT code, specialty, diagnosis, or comments.

---

## Scenario 5: Cancel Scheduling a Surgery

### Steps

1. At the Surgery menu, type: `2` (Schedule Surgery).
2. Fill in Principal Procedure: `Test Surgery`.
3. Continue through remaining fields.
4. At the confirmation prompt `Schedule this surgery?`, type: `N`.

### Expected Result

- The surgery is NOT scheduled.
- Returns to the Surgery menu.

---

## Scenario 6: Complete a Surgery (Happy Path)

### Steps

1. Pre-condition: A surgery with Status = SCHEDULED must exist.
2. At the Surgery menu, type: `3` (Complete Surgery).
3. A numbered list of non-completed/non-cancelled surgeries appears.
4. Select the surgery by number.
5. The terminal displays: `Operative Report:` (multiline input)
6. Type the operative report:
   ```
   OPERATIVE REPORT

   PROCEDURE: Right Total Knee Arthroplasty
   SURGEON: SMITH,JOHN A
   ANESTHESIA: Spinal with sedation

   FINDINGS: Severe tricompartmental osteoarthritis with bone-on-bone
   changes in the medial and patellofemoral compartments.

   TECHNIQUE: Standard medial parapatellar approach. Bone cuts made
   with measured resection technique. Cemented posterior-stabilized
   total knee prosthesis implanted. Good alignment and stability
   confirmed with trial components. Wound closed in layers.

   EBL: 150 mL
   TOURNIQUET TIME: 62 minutes
   COMPLICATIONS: None
   DISPOSITION: Recovery room in stable condition
   ```
7. End multiline input.
8. At the prompt `Post-Op Diagnosis (optional)`, type: `Right knee osteoarthritis, M17.11`
9. At the confirmation prompt `Complete this surgery?`, type: `Y`.

### Expected Result

- The terminal displays: `Surgery completed.`
- Verify by listing surgeries -- the surgery shows Status: COMPLETED.

---

## Scenario 7: Complete Surgery -- Minimal Report

### Steps

1. Select a scheduled surgery via option 3.
2. Enter a brief operative report: `Procedure completed without complications.`
3. Post-Op Diagnosis: (press Enter to skip)
4. Confirm: `Y`

### Expected Result

- Surgery completed with minimal report and no post-op diagnosis.

---

## Scenario 8: Cancel Completing a Surgery

### Steps

1. At the Surgery menu, type: `3` (Complete Surgery).
2. Select a surgery.
3. Enter an operative report.
4. At the confirmation prompt `Complete this surgery?`, type: `N`.

### Expected Result

- The surgery remains SCHEDULED.
- Returns to the Surgery menu.

---

## Scenario 9: Cancel a Scheduled Surgery (Happy Path)

### Steps

1. At the Surgery menu, type: `4` (Cancel Surgery).
2. A numbered list of scheduled/pending surgeries appears.
3. Select the surgery by number.
4. At the prompt `Reason for cancellation`, type: `Patient requests postponement due to family emergency`
5. At the confirmation prompt `Cancel this surgery?`, type: `Y`.

### Expected Result

- The terminal displays: `Surgery cancelled.`
- Verify by listing surgeries -- the surgery shows Status: CANCELLED.

---

## Scenario 10: Cancel Surgery -- Decline Confirmation

### Steps

1. At the Surgery menu, type: `4` (Cancel Surgery).
2. Select a surgery.
3. Enter a reason.
4. At the confirmation prompt `Cancel this surgery?`, type: `N`.

### Expected Result

- The surgery remains SCHEDULED.
- Returns to the Surgery menu.

---

## Scenario 11: Cancel Surgery -- No Scheduled Surgeries

### Steps

1. Ensure no surgeries have SCHEDULED status.
2. At the Surgery menu, type: `4` (Cancel Surgery).

### Expected Result

- Empty list or message indicating no scheduled surgeries.
- Returns to the Surgery menu.

---

## Scenario 12: Return to Main Menu

### Steps

1. At the Surgery menu, type `Q` or `^` to return.

### Expected Result

- Returns to the Main Menu with patient context preserved.
