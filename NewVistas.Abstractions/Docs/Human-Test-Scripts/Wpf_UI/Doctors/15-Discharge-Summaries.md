# Discharge Summaries (D/C Summ) -- Physician Human Test Script -- WPF UI

## Prerequisites
- Login: DOCTOR1 / Password: smythVista1
- Patient: 9
- Pre-conditions: Demo data loaded. Patient 9 should have at least one admission (use ADT demo load if needed: Navigation Panel > **ADT**, load patient 9, click **Load Demo**). SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) are running.

---

## Scenario 1: View Discharge Summary List (Happy Path)

### Steps
1. Log in as **DOCTOR1** (SMITH,JOHN A / Internal Medicine)
2. In the Navigation Panel, select **D/C Summaries**
3. Enter Patient ID in the toolbar: `9`
4. Click **Load Summaries** (or press Enter)

### Expected Result
- The view title shows "Discharge Summaries"
- A **Patient Banner** appears at the top with patient name, SSN (last 4), age, and ward/location
- The left pane contains a **TreeView** with discharge summaries organized by:
  - **By Date** (default) -- grouped by year/month, most recent first
  - **By Author** -- grouped alphabetically by authoring provider
  - **Custom** -- user-defined filter
- Each TreeView node shows: date, title, author, and status icon (green = COMPLETED, yellow = UNSIGNED, orange = UNCOSIGNED)
- The right pane is empty until a summary is selected, showing italic placeholder text: "Select a discharge summary to view"
- A toolbar row beneath the banner contains:
  - **+ New Summary** button (green)
  - **Print** button (outline)
  - **View:** ComboBox with options: By Date, By Author, Custom

---

## Scenario 2: Create a New Discharge Summary (Happy Path)

### Steps
1. With patient 9 loaded on the D/C Summaries view, click **+ New Summary** (green button)
2. A **New Discharge Summary** dialog window appears with the following fields:
   - Title: ComboBox with options (DISCHARGE SUMMARY is default; other options: INTERIM SUMMARY, TRANSFER SUMMARY)
   - Author: TextBox pre-filled with `SMITH,JOHN A`
   - Cosigner (optional): TextBox (leave blank for this scenario)
   - Location: ComboBox (click **Load Locations** if not populated; select `WARD MED-3A`)
   - Admission Date: DatePicker (select 03/22/2026)
   - Discharge Date: DatePicker (select 03/29/2026)
3. Click **OK** to create the summary shell
4. The right pane now shows a rich text editor with template sections pre-populated:
   ```
   DISCHARGE SUMMARY

   ADMISSION DATE:
   DISCHARGE DATE:

   PRINCIPAL DIAGNOSIS:

   SECONDARY DIAGNOSES:

   HOSPITAL COURSE:

   PROCEDURES PERFORMED:

   DISCHARGE CONDITION:

   DISCHARGE MEDICATIONS:

   FOLLOW-UP INSTRUCTIONS:

   ATTENDING PHYSICIAN:
   ```
5. Fill in the template:
   - PRINCIPAL DIAGNOSIS: `Community-acquired pneumonia (J18.9)`
   - SECONDARY DIAGNOSES:
     ```
     1. Type 2 Diabetes Mellitus (E11.9)
     2. Essential Hypertension (I10)
     ```
   - HOSPITAL COURSE:
     ```
     Patient admitted with fever (101.8F), productive cough, and right
     lower lobe infiltrate on chest X-ray. Started on IV Ceftriaxone 1g
     Q24H and Azithromycin 500mg daily. Blood cultures drawn on admission
     returned negative at 48 hours. WBC normalized by hospital day 3.
     Transitioned to oral antibiotics on hospital day 3. Oxygen
     requirements resolved by day 4. Ambulating without difficulty.
     ```
   - DISCHARGE CONDITION: `Stable, ambulatory, tolerating PO, afebrile x48h`
   - DISCHARGE MEDICATIONS:
     ```
     1. Amoxicillin/Clavulanate 875/125mg PO BID x 5 days (new)
     2. Lisinopril 10mg PO daily (home med, continued)
     3. Metformin 1000mg PO BID (home med, continued)
     ```
   - FOLLOW-UP INSTRUCTIONS:
     ```
     1. PCP follow-up in 1 week for repeat chest X-ray
     2. Return to ED if fever > 101.5F, worsening shortness of breath,
        or hemoptysis
     ```
   - ATTENDING PHYSICIAN: `SMITH,JOHN A`
6. Click **Save** (or press Ctrl+S)

### Expected Result
- A green notification appears in the status bar: "Discharge summary created successfully."
- The summary appears in the TreeView on the left under today's date
- The TreeView node shows: today's date, "DISCHARGE SUMMARY", "SMITH,JOHN A", yellow UNSIGNED icon
- The right pane displays the full text with a status indicator: "UNSIGNED" (yellow)
- API call: `POST /api/patient/9/notes` with DocumentType "DISCHARGE SUMMARY"

---

## Scenario 3: Sign a Discharge Summary

### Steps
1. With patient 9 loaded, click the UNSIGNED discharge summary created in Scenario 2 in the TreeView
2. The right pane displays the summary text and metadata
3. Click the **Sign** button (blue) in the right pane toolbar
4. The **Electronic Signature** dialog window appears:
   - Signer ID: TextBox -- enter `DOCTOR1`
   - Electronic Signature Code: TextBox (password masked) -- enter `smythVista1`
5. Click the **Sign** button in the dialog window

### Expected Result
- The dialog window closes
- A green notification appears in the status bar: "Discharge summary signed."
- The status indicator changes from "UNSIGNED" (yellow) to "COMPLETED" (green)
- The TreeView node icon updates to green (COMPLETED)
- The Sign button disappears from the right pane toolbar
- API call: `POST /api/patient/9/notes/{documentId}/sign`

---

## Scenario 4: Cosign a Discharge Summary (Attending Cosigns Resident's Summary)

### Steps
1. Click **+ New Summary**
2. In the dialog window:
   - Title: **DISCHARGE SUMMARY**
   - Author: `CHEN,MICHAEL L` (DOCTOR2 -- resident)
   - Cosigner: `SMITH,JOHN A` (attending)
   - Location: `WARD MED-4B`
3. Click **OK**
4. Enter summary text in the editor:
   ```
   DISCHARGE SUMMARY

   PRINCIPAL DIAGNOSIS: Acute exacerbation of COPD (J44.1)

   HOSPITAL COURSE:
   68yo male admitted with acute COPD exacerbation. Treated with
   nebulized bronchodilators, systemic corticosteroids, and supplemental
   O2. Improved over 3-day admission. FEV1 improved from 35% to 52%
   predicted at discharge.

   DISCHARGE MEDICATIONS:
   1. Prednisone 40mg PO daily x 5 days (taper)
   2. Albuterol MDI 2 puffs Q4H PRN
   3. Tiotropium 18mcg INH daily (continued)

   RESIDENT: CHEN,MICHAEL L
   ATTENDING: SMITH,JOHN A
   ```
5. Click **Save**
6. Click the new summary in the TreeView
7. Click **Sign** -- enter Signer ID: `DOCTOR2`, Signature Code: `smythVista1`
8. Click **Sign** in the dialog

### Expected Result
- Status changes to **UNCOSIGNED** (orange status indicator) because a Cosigner is set
- The **Cosign** button appears in the right pane toolbar
- The Sign button is no longer shown

### Steps (continued -- Attending cosigns)
9. Click the **Cosign** button (orange)
10. In the Electronic Signature dialog window:
    - Signer ID: `DOCTOR1`
    - Electronic Signature Code: `smythVista1`
11. Click **Cosign** in the dialog

### Expected Result
- A green notification appears in the status bar: "Discharge summary cosigned."
- Status changes to **COMPLETED** (green status indicator)
- The Cosign button disappears
- API call: `POST /api/patient/9/notes/{documentId}/cosign`

---

## Scenario 5: Amend a Discharge Summary

### Steps
1. With patient 9 loaded on D/C Summaries, locate a **COMPLETED** discharge summary in the TreeView
2. Click it to display in the right pane
3. Click the **Amend** button (outline style) in the right pane toolbar
4. The editor becomes editable with the existing text
5. Append the following at the bottom:
   ```

   AMENDMENT (03/30/2026 -- SMITH,JOHN A):
   Correction: Discharge weight was 182 lbs, not 185 lbs as originally
   documented. Repeat chest X-ray on discharge showed interval
   improvement with residual right basilar opacity.
   ```
6. Click **Save Amendment**

### Expected Result
- A green notification appears in the status bar: "Discharge summary amended."
- The status indicator changes to **AMENDED** (blue status indicator)
- The amendment text appears at the bottom of the summary with a visual separator
- The original text is preserved above the amendment
- API call: `POST /api/patient/9/notes/{documentId}/amend`

---

## Scenario 6: Add an Addendum to a Discharge Summary

### Steps
1. With patient 9 loaded, click a **COMPLETED** or **AMENDED** discharge summary in the TreeView
2. Click the **Add Addendum** button in the right pane toolbar
3. An addendum text editor appears below the main summary text
4. Enter:
   ```
   ADDENDUM (SMITH,JOHN A):
   Post-discharge follow-up call completed. Patient reports feeling well.
   Completing antibiotic course without difficulty. No fever, dyspnea, or
   chest pain. Repeat CXR appointment confirmed for 04/05/2026.
   ```
5. Click **Save Addendum**

### Expected Result
- A green notification appears in the status bar: "Addendum added."
- The addendum appears beneath the main summary text, visually separated
- The TreeView node for the parent summary shows an addendum count indicator: "(1 addendum)"
- The addendum is a separate document linked to the parent (not shown as a standalone entry in the TreeView)
- API call: `POST /api/patient/9/notes/{documentId}/addendum`

---

## Scenario 7: Print a Discharge Summary

### Steps
1. With patient 9 loaded, click a COMPLETED discharge summary in the TreeView
2. Click the **Print** button (outline style) in the right pane toolbar (or the main toolbar)
3. A **Print** dialog window appears with:
   - Device: ComboBox listing available printers / "Win Printer" / "PDF Export"
   - Copies: numeric spinner (default 1)
   - Include Addenda: CheckBox (checked by default)
   - Preview: CheckBox (checked by default)
4. Select Device: **PDF Export**
5. Ensure Preview is checked
6. Click **Print**

### Expected Result
- A print preview window opens showing the formatted discharge summary
- The summary includes:
  - Patient header (name, SSN last 4, DOB)
  - Document title and dates
  - Full summary text
  - Addenda (if any and checkbox was checked)
  - Signature block with signer name and date
- Close the preview window to return to the D/C Summaries view
- If PDF Export was selected, a file save dialog appears for the PDF location

---

## Scenario 8: View Unsigned Discharge Summaries Only

### Steps
1. With patient 9 loaded on the D/C Summaries view
2. In the View ComboBox (below the banner), select **Custom**
3. A filter panel expands with:
   - Status: ComboBox with options: All, Unsigned, Uncosigned, Completed, Amended
   - Author: TextBox (optional filter)
   - Date From: DatePicker
   - Date To: DatePicker
4. Set Status to **Unsigned**
5. Click **Apply Filter**

### Expected Result
- The TreeView refreshes to show only discharge summaries with UNSIGNED status
- Each node has a yellow UNSIGNED icon
- The TreeView header shows a filter indicator: "Filtered: Unsigned only"
- If no unsigned summaries exist, the TreeView shows: "No unsigned discharge summaries found"
- To clear the filter, change the View ComboBox back to **By Date** or click **Clear Filter**
