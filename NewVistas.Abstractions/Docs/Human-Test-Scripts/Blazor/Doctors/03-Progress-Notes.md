# Progress Notes (TIU Clinical Notes) -- Physician Human Test Script

## Prerequisites
- Login: DOCTOR1 / Password: smythVista1
- Patient: 9
- Pre-conditions: Demo data loaded. SiloHost, WebServer, and BlazorWeb running.

---

## Scenario 1: Create and Sign a Progress Note (Happy Path)

### Steps
1. Log in as **DOCTOR1** (SMITH,JOHN A / Internal Medicine)
2. Navigate to `/notes`
3. Enter Patient ID: `9`
4. Click **Load Notes** (or press Enter)
5. Click the **+ New Note** button (green)
6. The New Progress Note form appears
7. Fill in:
   - Document Type: **PROGRESS NOTE** (dropdown; options: PROGRESS NOTE, DISCHARGE SUMMARY, CONSULT NOTE, SURGICAL NOTE, CRISIS NOTE, ADVANCE DIRECTIVE)
   - Subject: `Follow-up Hypertension`
   - Author: `SMITH,JOHN A`
   - Location: `PRIMARY CARE CLINIC A`
   - Note Text:
     ```
     SUBJECTIVE:
     Patient returns for follow-up of essential hypertension. Reports good
     medication compliance with Lisinopril 10mg daily. No headaches, chest
     pain, or visual changes. Home BP readings averaging 132/82.

     OBJECTIVE:
     BP: 130/80  HR: 72  Weight: 185 lbs
     General: Well-appearing, no acute distress
     HEENT: Normocephalic, PERRL
     Cardiac: RRR, no murmurs, rubs, or gallops
     Lungs: CTA bilaterally

     ASSESSMENT:
     1. Essential Hypertension (I10) - controlled on current regimen

     PLAN:
     1. Continue Lisinopril 10mg daily
     2. BMP in 3 months to check potassium and creatinine
     3. Return to clinic in 3 months
     4. Reinforce diet and exercise counseling
     ```
8. Click **Save Note**

### Expected Result
- Green success banner: "Note created successfully."
- The New Note form closes
- The notes list reloads
- The new note appears in the table with:
  - Date: today's date and time
  - Type: PROGRESS NOTE
  - Subject: "Follow-up Hypertension"
  - Author: "SMITH,JOHN A"
  - Status: **UNSIGNED** (yellow badge)
  - Location: "PRIMARY CARE CLINIC A"

### Steps (continued -- Sign the Note)
9. Click on the note row in the table to open the detail view
10. The note detail shows:
    - Badge: "PROGRESS NOTE" (blue) and "UNSIGNED" (yellow)
    - Subject, Author, Date, Location
    - Full note text in a pre-formatted block
11. Click the **Sign** button (blue)
12. The Electronic Signature modal appears
13. Fill in:
    - Signer ID: `DOCTOR1`
    - Electronic Signature Code: `smythVista1`
14. Click the **sign** button in the modal

### Expected Result
- Modal closes
- Green success: "Note signed."
- The note detail refreshes
- Status changes to **COMPLETED** (green badge)
- The Sign button disappears from the detail view

---

## Scenario 2: Create a Discharge Summary

### Steps
1. Click **+ New Note**
2. Fill in:
   - Document Type: **DISCHARGE SUMMARY**
   - Subject: `Discharge - Pneumonia Treatment`
   - Author: `SMITH,JOHN A`
   - Location: `WARD MED-3A`
   - Note Text:
     ```
     DISCHARGE SUMMARY

     ADMISSION DATE: 03/22/2026
     DISCHARGE DATE: 03/29/2026

     PRINCIPAL DIAGNOSIS:
     Community-acquired pneumonia (J18.9)

     SECONDARY DIAGNOSES:
     1. Type 2 Diabetes Mellitus (E11.9)
     2. Essential Hypertension (I10)

     HOSPITAL COURSE:
     Patient admitted with fever, productive cough, and right lower lobe
     infiltrate on chest X-ray. Started on IV Ceftriaxone and Azithromycin.
     Blood cultures negative. Transitioned to oral antibiotics on hospital
     day 3. Oxygen requirements resolved by day 4.

     DISCHARGE MEDICATIONS:
     1. Amoxicillin/Clavulanate 875/125mg PO BID x 5 days
     2. Lisinopril 10mg PO daily (home med)
     3. Metformin 1000mg PO BID (home med)

     FOLLOW-UP:
     - PCP in 1 week for repeat chest X-ray
     - Return to ED if fever > 101.5, worsening SOB, or hemoptysis

     CONDITION AT DISCHARGE: Stable, ambulatory, tolerating PO
     ```
3. Click **Save Note**

### Expected Result
- Green success: "Note created successfully."
- Note appears in the list with Type: DISCHARGE SUMMARY, Status: UNSIGNED

---

## Scenario 3: Note Requiring Cosignature (Attending Signs Resident's Note)

### Steps
1. As DOCTOR1, click **+ New Note**
2. Fill in:
   - Document Type: **PROGRESS NOTE**
   - Subject: `Resident H&P - Chest Pain Evaluation`
   - Author: `CHEN,MICHAEL L` (DOCTOR2 -- the resident in this scenario)
   - Location: `EMERGENCY DEPARTMENT`
   - Note Text:
     ```
     HISTORY AND PHYSICAL - RESIDENT NOTE

     CC: Chest pain x 2 hours

     HPI: 58yo male presents with substernal chest pressure radiating to
     left arm, onset at rest. Associated with diaphoresis and nausea.
     No prior cardiac history. Smoker 1 PPD x 30 years.

     [... examination details ...]

     ASSESSMENT: Acute coronary syndrome, NSTEMI
     PLAN: Admit to telemetry, serial troponins, cardiology consult

     RESIDENT: CHEN,MICHAEL L
     ATTENDING COSIGNER: SMITH,JOHN A
     ```
3. Click **Save Note**

### Expected Result
- Note created with Status: UNSIGNED

### Steps (continued -- Resident signs)
4. Click the note to view detail
5. Click **Sign**
6. In the modal:
   - Signer ID: `DOCTOR2`
   - Electronic Signature Code: `smythVista1`
7. Click **sign**

### Expected Result
- If the note has a CosignerId set, status becomes **UNCOSIGNED** (orange badge)
- The **Cosign** button appears in the detail view

### Steps (continued -- Attending cosigns)
8. Log out and log in as **DOCTOR1** (or, if the same session, the Cosign button is visible)
9. Navigate to `/notes`, load patient 9
10. Click the UNCOSIGNED note
11. Click **Cosign**
12. In the modal:
    - Signer ID: `DOCTOR1`
    - Electronic Signature Code: `smythVista1`
13. Click **cosign**

### Expected Result
- Green success: "Note cosigned."
- Status changes to **COMPLETED** (green badge)
- Cosign button disappears

---

## Scenario 4: Amend a Previously Signed Note

### Steps
1. Navigate to `/notes`, load patient 9
2. Locate a note with status **COMPLETED**
3. Click the note row to view detail
4. Note: The current Blazor page does not have an inline "Amend" button in the detail view, but the workflow grain supports `AmendNoteAsync`. If an Amend button is present:
   - Click **Amend**
   - Modify the note text
   - Save
5. If no UI button exists, this scenario documents the expected behavior for future UI implementation.

### Expected Result (when Amend UI is available)
- The note status changes to **AMENDED** (blue badge)
- The original text is preserved; amendment text is appended
- Amendment date and author are recorded

---

## Scenario 5: Add Addendum to Existing Note

### Steps
1. As DOCTOR1, load patient 9 notes
2. Click a COMPLETED note to view its detail
3. Note: The workflow grain supports `AddAddendumAsync`. The detail view shows "Addenda (N)" label if addendumIds exist.
4. If an "Add Addendum" button is available in the UI:
   - Click it
   - Enter addendum text: `Addendum: Lab results reviewed. Troponin negative x3. Patient cleared for discharge.`
   - Save
5. If no UI button exists, this documents expected behavior.

### Expected Result (when Addendum UI is available)
- The addendum is created as a separate TIU document with ParentDocumentId set
- The parent note's AddendumIds list updates
- The detail view shows "Addenda (1)" or similar count
- Addenda are excluded from the top-level notes list (not shown as standalone)

---

## Scenario 6: Create a Crisis Note

### Steps
1. Click **+ New Note**
2. Fill in:
   - Document Type: **CRISIS NOTE**
   - Subject: `Suicidal Ideation Screen Positive`
   - Author: `SMITH,JOHN A`
   - Location: `EMERGENCY DEPARTMENT`
   - Note Text:
     ```
     CRISIS NOTE

     Patient screened positive for suicidal ideation during PHQ-9
     administration. Patient endorses passive SI with plan but no intent.
     No access to firearms confirmed. Support system in place (spouse).

     ACTIONS TAKEN:
     1. 1:1 observation initiated
     2. Psychiatry consult placed (STAT)
     3. Safety plan reviewed with patient
     4. Sharps and ligature risk assessment completed
     5. Attending notified

     RISK LEVEL: HIGH
     ```
3. Click **Save Note**

### Expected Result
- Note appears with Type: CRISIS NOTE, Status: UNSIGNED
- This note type sets the "C" flag in the patient's CWAD on the cover sheet

---

## Scenario 7: Use Note History Search

### Steps
1. On the `/notes` page with patient 9 loaded
2. Click the **Note History** button (outline style)
3. The Note History panel expands with date filters:
   - From: 90 days ago (default)
   - To: Today (default)
   - Max Results: 100 (default)
4. Click **Load History**

### Expected Result
- A results count shows: "[N] note(s) found"
- A table shows historical notes with columns: Date, Type, Subject, Author, Status, Location
- Notes with addenda show "[+]" next to the subject
- Clicking a row opens the note detail (same as main list)

---

## Scenario 8: Validation -- Empty Note Text

### Steps
1. Click **+ New Note**
2. Leave the Note Text field empty
3. Fill in all other fields
4. Click **Save Note**

### Expected Result
- Red error: "Note text is required."
- The note is not created
