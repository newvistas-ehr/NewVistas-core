# Discharge Summaries -- Physician CharUI Human Test Script

## Prerequisites

- **Login:** DOCTOR1 / Password: `smythVista1`
- **Security Keys Required:** PROVIDER (create summary), TIU SIGN (sign summary)
- **Patient:** Select a patient with demo data loaded (ideally an admitted patient).
- **Pre-conditions:**
  1. SiloHost and WebServer running.
  2. Electronic signature must be set for DOCTOR1 via `POST /api/auth/signature/set`.
  3. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: List Discharge Summaries (Happy Path)

### Steps

1. At the Main Menu, type: `DC` and press Enter.
2. At the D/C Summary menu, type: `1` (List D/C Summaries).

### Expected Result

- A table displays with columns: #, Subject, Author, Status, Date.
- Only notes with DocumentType = "DISCHARGE SUMMARY" appear.

---

## Scenario 2: View Discharge Summary Detail

### Steps

1. At the D/C Summary menu, type: `2` (View Summary Detail).
2. A numbered list of discharge summaries appears.
3. Select one by number.

### Expected Result

- The terminal displays the full summary:
  ```
  Document Type: DISCHARGE SUMMARY
  Subject: CHF Exacerbation
  Author: SMITH,JOHN A
  Cosigner: (if any)
  Status: COMPLETED
  Date: 03/31/2026 10:00
  Location: WARD-MED-3A
  ---
  [Full discharge summary text]

  0 addendum(a) attached.
  ```

---

## Scenario 3: Create a New Discharge Summary (Happy Path)

### Steps

1. At the D/C Summary menu, type: `3` (Create New D/C Summary).
2. Enter the following field-by-field:

| Prompt | Value to Enter |
|--------|----------------|
| Subject (e.g., Primary Diagnosis) | `Congestive Heart Failure Exacerbation` |
| Discharge Location | `HOME WITH HOME HEALTH SERVICES` |
| Cosigner (optional) | (press Enter to skip) |

3. The terminal displays:
   ```
   Enter summary sections:
   (Admitting Diagnosis, Hospital Course, Discharge Medications,
    Follow-Up Instructions, Condition at Discharge)
   ```
4. At the `Enter summary text:` multiline prompt, type:
   ```
   ADMITTING DIAGNOSIS:
   Acute exacerbation of congestive heart failure (I50.9)

   HOSPITAL COURSE:
   Patient admitted with dyspnea on exertion and bilateral lower
   extremity edema. Started on IV furosemide with good diuresis.
   BNP trended down from 1200 to 340. Echo showed EF 35%.
   Transitioned to oral diuretics on hospital day 3.

   DISCHARGE MEDICATIONS:
   1. Furosemide 40mg PO BID
   2. Lisinopril 20mg PO daily
   3. Metoprolol Succinate 50mg PO daily
   4. Spironolactone 25mg PO daily

   FOLLOW-UP INSTRUCTIONS:
   1. Cardiology clinic in 1 week
   2. PCP follow-up in 2 weeks
   3. Daily weight monitoring
   4. Fluid restriction 1.5L/day
   5. Low sodium diet

   CONDITION AT DISCHARGE: Stable, improved
   ```
5. End multiline input.
6. At the confirmation prompt `Save discharge summary?`, type: `Y`.

### Expected Result

- The terminal displays: `D/C Summary created: [document-ID]`
- Returns to the D/C Summary menu.
- Verify by listing summaries (option 1) -- the new summary appears with Status: UNSIGNED.

---

## Scenario 4: Create D/C Summary with Cosigner (Trainee)

### Steps

1. At the D/C Summary menu, type: `3` (Create New D/C Summary).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Subject | `Post-Operative Recovery - TKA` |
| Discharge Location | `SKILLED NURSING FACILITY` |
| Cosigner (optional) | `DOCTOR2` |

3. Enter summary text and confirm.

### Expected Result

- Summary created with cosigner assigned.
- When signed, status will be UNCOSIGNED until DOCTOR2 cosigns.

---

## Scenario 5: Cancel Creating a D/C Summary

### Steps

1. At the D/C Summary menu, type: `3`.
2. Enter Subject and Location.
3. Enter summary text.
4. At the confirmation prompt `Save discharge summary?`, type: `N`.

### Expected Result

- The summary is NOT saved.
- Returns to the D/C Summary menu.

---

## Scenario 6: Sign a Discharge Summary (Happy Path)

### Steps

1. Pre-condition: An UNSIGNED discharge summary must exist.
2. At the D/C Summary menu, type: `4` (Sign Summary).
3. A numbered list of unsigned discharge summaries appears.
4. Select one by number.
5. At the prompt `SIGNATURE CODE:`, type the electronic signature code (masked input).

### Expected Result

- The terminal displays: `D/C Summary signed.`
- The summary status changes from UNSIGNED to COMPLETED (or UNCOSIGNED if cosigner assigned).

---

## Scenario 7: Sign D/C Summary -- Invalid Signature

### Steps

1. At the D/C Summary menu, type: `4` (Sign Summary).
2. Select an unsigned summary.
3. At the `SIGNATURE CODE:` prompt, type: `BADCODE`

### Expected Result

- The terminal displays: `*** INVALID SIGNATURE CODE ***`
- The summary remains UNSIGNED.

---

## Scenario 8: Sign D/C Summary -- No Unsigned Summaries

### Steps

1. Ensure all discharge summaries are signed.
2. At the D/C Summary menu, type: `4` (Sign Summary).

### Expected Result

- Empty list or message indicating no unsigned summaries.
- Returns to the D/C Summary menu.

---

## Scenario 9: Add an Addendum to a D/C Summary (Happy Path)

### Steps

1. At the D/C Summary menu, type: `5` (Add Addendum).
2. A numbered list of discharge summaries appears.
3. Select one by number.
4. At the `Enter addendum text:` multiline prompt, type:
   ```
   ADDENDUM: Post-discharge lab results reviewed.
   Creatinine improved to 1.1 (from 1.8 at admission).
   Potassium 4.2 on current regimen. Continue medications as prescribed.
   ```
5. End multiline input.

### Expected Result

- The terminal displays: `Addendum added: [addendum-ID]`
- When viewing the parent summary detail, it shows "1 addendum(a) attached."

---

## Scenario 10: Create D/C Summary Without PROVIDER Key (Access Denied)

### Steps

1. Log out and log in as a user without the PROVIDER key (e.g., CLERK1 / `smythVista1`).
2. Select a patient.
3. Navigate to D/C Summaries (DC).
4. Type: `3` (Create New D/C Summary).

### Expected Result

- The terminal displays: `You do not hold the PROVIDER key.`
- Returns to the D/C Summary menu. No summary is created.

---

## Scenario 11: Return to Main Menu

### Steps

1. At the D/C Summary menu, type `Q` or `^` to return.

### Expected Result

- Returns to the Main Menu with patient context preserved.
