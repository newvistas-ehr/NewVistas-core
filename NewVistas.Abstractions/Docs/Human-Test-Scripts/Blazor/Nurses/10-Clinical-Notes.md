# Clinical Notes (TIU / Nursing Notes) -- Human Test Script

## Prerequisites

- **Login:** NURSE1 / Password: `smythVista1`
- **Patient:** 9
- **Pre-conditions:**
  1. The SiloHost, WebServer, and BlazorWeb projects must all be running.
  2. Navigate to `/notes` in the browser.
  3. Enter Patient ID `9` in the Patient ID field and click **Load Notes**.
  4. If no notes exist, the page shows "No notes found for this patient."

---

## Scenario 1: Create a Nursing Progress Note and Sign It (Happy Path)

### Steps

1. Navigate to `/notes`.
2. Enter Patient ID: `9`
3. Click **Load Notes**.
4. Click the **+ New Note** button in the lookup bar.
5. The "New Progress Note" form appears. Fill in:
   - Document Type: `PROGRESS NOTE`
   - Subject: `Nursing Shift Assessment - Day Shift`
   - Author: `JOHNSON,MARY R`
   - Location: `Ward 3A`
   - Note Text:
     ```
     NURSING PROGRESS NOTE - DAY SHIFT

     SUBJECTIVE:
     Patient reports feeling "better than yesterday." Pain at surgical site rated 3/10, down from 6/10 yesterday. Slept well overnight with one interruption for vitals. Appetite improving -- ate 75% of breakfast.

     OBJECTIVE:
     Alert and oriented x4. VS: T 98.4F, P 74, R 16, BP 128/78, SpO2 97% on RA.
     Lungs: CTA bilaterally. Heart: RRR, no murmurs.
     Abdomen: Soft, non-distended, positive bowel sounds all quadrants.
     Surgical incision: Right lower quadrant, approximated, Steri-strips intact, no erythema/drainage.
     IV: Left forearm 20g, patent, no signs of infiltration.
     Foley: D/C'd this AM, patient voided 350mL clear yellow urine.
     Mobility: Ambulated 200ft in hallway with rolling walker, steady gait.
     Braden: 19. Morse: 30.

     ASSESSMENT:
     Patient progressing well post-operatively. Pain well-controlled on PO analgesics.
     Fall risk moderate -- continue precautions.

     PLAN:
     1. Continue current pain management regimen.
     2. Advance diet to regular as tolerated.
     3. Continue ambulation TID with PT.
     4. Anticipate discharge planning conference tomorrow.
     5. IV antibiotics to complete day 3 of 5.
     ```
6. Click **Save Note**.

### Expected Result

- Green success banner: "Note created successfully."
- The New Note form closes.
- The notes list reloads and shows the new note:
  - Date: current date/time
  - Type: PROGRESS NOTE
  - Subject: Nursing Shift Assessment - Day Shift
  - Author: JOHNSON,MARY R
  - Status: **UNSIGNED** (yellow/amber badge)
  - Location: Ward 3A

7. **Sign the note:**
   a. Click the note row to view it.
   b. The note detail panel opens showing the full note text, type badge, and status badge.
   c. The **Sign** button is visible (because Status is UNSIGNED).
   d. Click **Sign**.
   e. The Electronic Signature modal appears:
      - Signer ID: `NURSE1`
      - Electronic Signature Code: `smythVista1` (or any non-empty value)
   f. Click the **sign** button in the modal.

### Expected Result

- Green success banner: "Note signed."
- The note detail reloads.
- Status changes from UNSIGNED to **COMPLETED** (green badge).
- The Sign button disappears.
- In the notes list, the Status column shows COMPLETED.

---

## Scenario 2: Note Requiring Cosignature (Student Nurse Scenario)

### Steps

1. Click **+ New Note**.
2. Fill in:
   - Document Type: `PROGRESS NOTE`
   - Subject: `Nursing Student Documentation - Wound Assessment`
   - Author: `STUDENT,NURSE J` (a student nurse)
   - Location: `Ward 3A`
   - Note Text:
     ```
     NURSING STUDENT NOTE - WOUND ASSESSMENT

     Performed wound assessment on surgical incision per instructor guidance.

     Incision: Right lower quadrant, approximately 8cm.
     Wound edges: Well-approximated with Steri-strips.
     Surrounding skin: Pink, warm, dry. No erythema extending > 1cm from edges.
     Drainage: None observed.
     Dressing: Changed to clean dry gauze per protocol.

     Instructor notified. Patient tolerated procedure well.

     [Note: This documentation requires cosignature by supervising RN]
     ```
3. Click **Save Note**.

### Expected Result

- Note created with Status: UNSIGNED.

4. **Sign the note (as student):**
   a. Click the note row.
   b. Click **Sign**.
   c. In the Electronic Signature modal:
      - Signer ID: `STUDENT1`
      - Electronic Signature Code: `studentpass`
   d. Click **sign**.

### Expected Result

- Note status changes to **UNCOSIGNED** (orange badge) if a cosigner was set on the note, OR to **COMPLETED** if no cosigner was configured.
- In a real scenario where a CosignerId was set at note creation, the note would require cosignature. The current Blazor UI creates notes without a CosignerId by default, so the note will go to COMPLETED. To test the cosign flow:

5. **Cosign flow (via API):**
   - Create a note via API with a CosignerId set:
     - `POST /api/patient/9/notes` with `cosignerId: "NURSE1"` in the request body.
   - The note status after author signature will be UNCOSIGNED.
   - On the Blazor page, load the patient's notes and click the UNCOSIGNED note.
   - The **Cosign** button appears.
   - Click **Cosign**.
   - In the Electronic Signature modal:
     - Signer ID: `NURSE1`
     - Electronic Signature Code: `smythVista1`
   - Click **cosign**.

### Expected Result

- The note status changes from UNCOSIGNED to **COMPLETED** (green badge).
- The Cosign button disappears.

---

## Scenario 3: Add an Addendum to an Existing Note

### Steps

1. Ensure you have a signed (COMPLETED) note from Scenario 1.
2. The addendum functionality is available via the workflow grain method `AddAddendumAsync`. Use the API:
   - `POST /api/patient/9/notes/{documentId}/addendum`
   - Body:
     ```json
     {
       "text": "ADDENDUM: Patient developed mild nausea at 1430. Ondansetron 4mg ODT administered. Nausea resolved within 20 minutes. No emesis. Continued to tolerate PO intake at dinner. - JOHNSON,MARY R RN",
       "authorId": "NURSE1",
       "authorName": "JOHNSON,MARY R"
     }
     ```
3. Reload the notes list on the Blazor page.

### Expected Result

- The original note row in the notes list now shows `[+]` after the Subject, indicating addenda are present.
- Click the note to view it.
- In the note detail, a label "Addenda (1)" appears at the bottom (styled with amber background -- addenda-label class).
- The original note text is unchanged.

---

## Scenario 4: Amend a Signed Note

### Steps

1. Ensure you have a COMPLETED note from Scenario 1.
2. Use the API to amend the note:
   - The `AmendNoteAsync` workflow method amends the note text.
   - Call: `POST /api/patient/9/notes/{documentId}/amend`
   - Body:
     ```json
     {
       "text": "[AMENDED] Original note above. CORRECTION: Urine output was 350mL, not 250mL as initially documented. Corrected per I&O sheet review. - JOHNSON,MARY R RN, amended on [date]"
     }
     ```
3. Reload the notes list on the Blazor page.

### Expected Result

- The note's Status changes to **AMENDED** (blue badge -- status-amended CSS class).
- Clicking the note shows the amended text.
- The original note content is updated with the amendment.

---

## Scenario 5: Note Text Required Validation

### Steps

1. Click **+ New Note**.
2. Fill in:
   - Document Type: `PROGRESS NOTE`
   - Subject: `Test note`
   - Author: `JOHNSON,MARY R`
   - Location: `Ward 3A`
   - Note Text: (leave blank)
3. Click **Save Note**.

### Expected Result

- Red error banner: "Note text is required."
- The note is not saved.
- The form remains open.

---

## Scenario 6: Electronic Signature Validation

### Steps

1. Create and save a new note (with valid text).
2. Click the note row to view it.
3. Click **Sign**.
4. In the Electronic Signature modal:
   - Leave Signer ID blank.
   - Leave Electronic Signature Code blank.
5. Click **sign**.

### Expected Result

- Error message in the modal: "Electronic signature code is required." or "Signer ID is required."
- The note remains UNSIGNED.

---

## Scenario 7: View Notes in Note History Tab

### Steps

1. Click the **Note History** button in the lookup bar.
2. The Note History panel expands with filter fields.
3. Set:
   - From: (90 days ago -- default)
   - To: (today -- default)
   - Max Results: `100`
4. Click **Load History**.

### Expected Result

- The history table shows all notes for Patient 9 within the date range.
- Each row shows: Date, Type, Subject, Author, Status (badge), Location.
- Clicking a row opens the note detail.
- The result count is displayed (e.g., "4 note(s) found").

---

## Scenario 8: Create Different Document Types

### Steps

1. Click **+ New Note**.
2. Fill in:
   - Document Type: `DISCHARGE SUMMARY`
   - Subject: `Discharge Summary - Appendectomy`
   - Author: `JOHNSON,MARY R`
   - Location: `Ward 3A`
   - Note Text:
     ```
     NURSING DISCHARGE SUMMARY

     Patient: [Patient 9]
     Admission Date: [3 days ago]
     Discharge Date: [today]
     Discharge Diagnosis: Acute appendicitis, status post laparoscopic appendectomy

     DISCHARGE CONDITION: Stable, improved
     DISCHARGE DISPOSITION: Home with self-care

     DISCHARGE INSTRUCTIONS PROVIDED:
     1. Wound care: Keep incision sites clean and dry. Steri-strips will fall off in 7-10 days.
     2. Activity: No heavy lifting > 10 lbs for 2 weeks. May resume normal activities as tolerated.
     3. Diet: Regular diet. Increase fiber and fluids.
     4. Medications: Acetaminophen 500mg PO Q6H PRN pain. Prescriptions provided.
     5. Follow-up: Surgeon office in 2 weeks. PCP in 1 week.
     6. Return to ER if: Fever > 101.5F, increasing pain, redness/drainage at incision, nausea/vomiting.

     Patient verbalized understanding. Written instructions provided.
     ```
3. Click **Save Note**.

### Expected Result

- Note created with Type badge showing "DISCHARGE SUMMARY" (blue badge-type styling).
- Status: UNSIGNED until signed.

---

## Reference: Document Types and Statuses

### Document Types (dropdown options)

| Document Type      | Typical Use                          |
|--------------------|--------------------------------------|
| PROGRESS NOTE      | Shift reports, assessments           |
| DISCHARGE SUMMARY  | Discharge documentation              |
| CONSULT NOTE       | Consultation response/request        |
| SURGICAL NOTE      | Pre-op/post-op nursing notes         |
| CRISIS NOTE        | Behavioral health crisis             |
| ADVANCE DIRECTIVE  | Living will, healthcare proxy        |

### Note Status Lifecycle

| Status      | Badge Color   | Description                                    |
|-------------|---------------|------------------------------------------------|
| UNSIGNED    | Yellow/Amber  | Note saved but not signed                      |
| UNCOSIGNED  | Orange        | Signed by author, awaiting cosignature         |
| COMPLETED   | Green         | Fully signed (and cosigned if required)        |
| AMENDED     | Blue          | Note has been amended after completion         |
| RETRACTED   | Red           | Note has been retracted (entered in error)     |

### Electronic Signature Modal Fields

| Field                    | Required | Description                          |
|--------------------------|----------|--------------------------------------|
| Signer ID                | Yes      | User ID of the signing clinician     |
| Electronic Signature Code| Yes      | Password/PIN for authentication      |
