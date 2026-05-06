# Progress Notes -- Physician CharUI Human Test Script

## Prerequisites

- **Login:** DOCTOR1 / Password: `smythVista1`
- **Security Keys Required:** PROVIDER (write/amend/addendum), TIU SIGN (sign), ORES
- **Patient:** Select a patient with demo data loaded.
- **Pre-conditions:**
  1. SiloHost and WebServer running.
  2. Electronic signature must be set for DOCTOR1 via `POST /api/auth/signature/set` with body `{ "signatureCode": "1Doctor1!" }` (or your chosen code).
  3. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: List Recent Notes (Happy Path)

### Steps

1. At the Main Menu, type: `NO` and press Enter.
2. At the Notes menu, type: `1` (List Recent Notes).

### Expected Result

- A table displays with columns: #, Type, Subject, Author, Status, Date.
- Notes are listed in reverse chronological order (newest first).
- If demo data loaded, at least 1 note should appear.

---

## Scenario 2: View Note Detail

### Steps

1. At the Notes menu, type: `2` (View Note Detail).
2. A numbered list of notes appears.
3. Select a note by typing its number (e.g., `1`).

### Expected Result

- The terminal displays the full note detail:
  ```
  Document Type: PROGRESS NOTE
  Subject: Follow-up Visit
  Author: SMITH,JOHN A
  Cosigner: (if any)
  Status: COMPLETED
  Date: 03/31/2026 10:00
  Location: MEDICINE CLINIC
  ---
  [Full report text body]

  0 addendum(a) attached.
  ```
- Returns to the Notes menu after display.

---

## Scenario 3: Write a New Progress Note (Happy Path)

### Steps

1. At the Notes menu, type: `3` (Write New Note).
2. Enter the following field-by-field:

| Prompt | Value to Enter |
|--------|----------------|
| Document Type | `PROGRESS NOTE` (or press Enter for default) |
| Subject (optional) | `Hypertension Follow-Up` |
| Location (optional) | `MEDICINE CLINIC` |
| Cosigner (optional) | (press Enter to skip -- not a trainee) |

3. The terminal displays: `Enter note text:` (multiline input mode)
4. Type the note body:
   ```
   SUBJECTIVE: Patient reports compliance with Lisinopril 10mg daily.
   No headaches, dizziness, or chest pain. Diet modifications ongoing.

   OBJECTIVE: BP 128/82, HR 72, RR 16. No peripheral edema.
   Heart RRR, no murmurs. Lungs CTA bilaterally.

   ASSESSMENT: Essential Hypertension (I10) - well controlled.

   PLAN:
   1. Continue Lisinopril 10mg PO daily
   2. Repeat BMP in 3 months
   3. Return to clinic in 6 months
   ```
5. End multiline input (press Enter on an empty line, or as instructed by the terminal).
6. At the confirmation prompt `Save this note?`, type: `Y`.

### Expected Result

- The terminal displays: `Note created: [document-ID]`
- Returns to the Notes menu.
- Verify by listing notes (option 1) -- the new note appears with Status: UNSIGNED, Type: PROGRESS NOTE.

---

## Scenario 4: Write a Note -- All Document Types

### Steps

Repeat Scenario 3 with each document type. At the `Document Type` prompt, enter each of:

| Document Type | Subject Example |
|---------------|-----------------|
| `PROGRESS NOTE` | `Daily Progress Note` |
| `CONSULT NOTE` | `Cardiology Consult` |
| `H&P` | `Admission H&P` |
| `DISCHARGE SUMMARY` | `Final Discharge Summary` |
| `CRISIS NOTE` | `Acute Suicidal Ideation` |
| `ADVANCE DIRECTIVE` | `Living Will Documentation` |

### Expected Result

- Each note is created successfully with the corresponding document type.
- All appear in the notes list with UNSIGNED status.

---

## Scenario 5: Write a Note with Cosigner (Trainee Scenario)

### Steps

1. At the Notes menu, type: `3` (Write New Note).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Document Type | `PROGRESS NOTE` |
| Subject (optional) | `Resident Note - Requires Cosign` |
| Location (optional) | `MEDICINE CLINIC` |
| Cosigner (optional) | `DOCTOR2` |

3. Enter note text and confirm.

### Expected Result

- Note created with a cosigner assigned.
- When signed, status will go to UNCOSIGNED (not COMPLETED) until cosigned.

---

## Scenario 6: Cancel Writing a Note

### Steps

1. At the Notes menu, type: `3` (Write New Note).
2. Fill in Document Type and Subject.
3. Enter note text.
4. At the confirmation prompt `Save this note?`, type: `N`.

### Expected Result

- The note is NOT saved.
- Returns to the Notes menu.

---

## Scenario 7: Sign a Note (Happy Path -- No Cosigner)

### Steps

1. Pre-condition: An UNSIGNED note must exist for this patient (write one if needed).
2. At the Notes menu, type: `4` (Sign Note).
3. A numbered list of unsigned notes appears.
4. Select the note to sign by number.
5. At the prompt `SIGNATURE CODE:`, type the electronic signature code (masked input).

### Expected Result

- The terminal displays: `Note signed.`
- The note status changes from UNSIGNED to COMPLETED.
- Verify by listing notes -- the signed note shows Status: COMPLETED.

---

## Scenario 8: Sign a Note -- With Cosigner (Goes to UNCOSIGNED)

### Steps

1. Pre-condition: An UNSIGNED note with a cosigner assigned must exist.
2. At the Notes menu, type: `4` (Sign Note).
3. Select the note with the cosigner.
4. Enter the signature code.

### Expected Result

- The terminal displays: `Note signed.`
- The note status changes from UNSIGNED to UNCOSIGNED (because a cosigner is required).
- The note will remain UNCOSIGNED until the cosigner completes their cosignature.

---

## Scenario 9: Sign a Note -- Invalid Signature

### Steps

1. At the Notes menu, type: `4` (Sign Note).
2. Select an unsigned note.
3. At the `SIGNATURE CODE:` prompt, type: `BADCODE`

### Expected Result

- The terminal displays: `*** INVALID SIGNATURE CODE ***`
- The note remains UNSIGNED.

---

## Scenario 10: Cosign a Note (Happy Path)

### Steps

1. Pre-condition: A note with Status = UNCOSIGNED must exist, and the cosigner must be the current user or DOCTOR1 must hold TIU COSIGN.
2. At the Notes menu, type: `5` (Cosign Note).
3. A numbered list of notes awaiting cosignature appears.
4. Select the note by number.
5. At the prompt `SIGNATURE CODE:`, type the electronic signature code.

### Expected Result

- The terminal displays: `Note cosigned.`
- The note status changes from UNCOSIGNED to COMPLETED.

---

## Scenario 11: Cosign Note -- No Notes Awaiting Cosignature

### Steps

1. Ensure no UNCOSIGNED notes exist for this patient.
2. At the Notes menu, type: `5` (Cosign Note).

### Expected Result

- Empty list or message indicating no notes require cosignature.
- Returns to the Notes menu.

---

## Scenario 12: Add an Addendum to a Note (Happy Path)

### Steps

1. At the Notes menu, type: `6` (Add Addendum).
2. A numbered list of notes appears.
3. Select a note by number.
4. The terminal displays: `Enter addendum text:` (multiline input)
5. Type the addendum:
   ```
   ADDENDUM: Lab results reviewed. BMP within normal limits.
   eGFR 85 mL/min. Continue current medication regimen.
   ```
6. End multiline input.

### Expected Result

- The terminal displays: `Addendum added: [addendum-ID]`
- The addendum is created as a separate TIU document linked to the parent note.
- When viewing the parent note detail (option 2), it now shows "1 addendum(a) attached."

---

## Scenario 13: Amend a Completed Note (Happy Path)

### Steps

1. Pre-condition: A note with Status = COMPLETED must exist.
2. At the Notes menu, type: `7` (Amend Note).
3. A numbered list of completed notes appears.
4. Select the note by number.
5. The terminal displays: `Enter amendment text:` (multiline input)
6. Type the amendment:
   ```
   AMENDMENT: Corrected medication dosage. Lisinopril should be 20mg,
   not 10mg as originally documented. Dose was increased during visit.
   ```
7. End multiline input.
8. At the prompt `SIGNATURE CODE:`, type the electronic signature code.

### Expected Result

- The terminal displays: `Note amended.`
- The note status changes to AMENDED.
- The amendment text is appended to the document.

---

## Scenario 14: Amend Note -- Invalid Signature

### Steps

1. At the Notes menu, type: `7` (Amend Note).
2. Select a completed note.
3. Enter amendment text.
4. At the `SIGNATURE CODE:` prompt, type: `WRONGCODE`

### Expected Result

- The terminal displays: `*** INVALID SIGNATURE CODE ***`
- The note is NOT amended.

---

## Scenario 15: Write Note Without PROVIDER Key (Access Denied)

### Steps

1. Log out and log in as a user without the PROVIDER key (e.g., CLERK1).
2. Select a patient.
3. Navigate to Notes menu (NO).
4. Type: `3` (Write New Note).

### Expected Result

- The terminal displays: `You do not hold the PROVIDER key. Note entry is not permitted.`
- Returns to the Notes menu. No note is created.

---

## Scenario 16: Return to Main Menu

### Steps

1. At the Notes menu, type `Q` or `^` to return.

### Expected Result

- Returns to the Main Menu with patient context preserved.
