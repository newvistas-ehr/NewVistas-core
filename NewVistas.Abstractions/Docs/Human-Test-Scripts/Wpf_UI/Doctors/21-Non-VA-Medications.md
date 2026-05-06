# Non-VA Medications -- Physician Human Test Script -- WPF UI

## Prerequisites
- Login: DOCTOR1 / Password: smythVista1
- Patient: 4
- Pre-conditions: Demo data loaded. Outpatient pharmacy demo data loaded for patient 4 (Navigation Panel > **Outpatient Pharmacy**, load patient 4, click **Load Demo** if needed). SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) are running.

---

## Scenario 1: View Non-VA Medications List (Happy Path)

### Steps
1. Log in as **DOCTOR1** (SMITH,JOHN A / Internal Medicine)
2. In the Navigation Panel, select **Medications** (or **Outpatient Pharmacy**)
3. Enter Patient ID in the toolbar: `4`
4. Click **Load Prescriptions** (or press Enter)

### Expected Result
- The Medications view displays three sections separated by horizontal splitters:
  - **Outpatient Medications** DataGrid (top section) -- VA-prescribed outpatient medications
  - **Non-VA Medications** DataGrid (middle section) -- medications from outside sources
  - **Inpatient Medications** DataGrid (bottom section) -- active inpatient orders
- Each section has a bold header label (e.g., "Non-VA Medications")
- The **Non-VA Medications** DataGrid has columns:
  - Medication (drug name and strength)
  - Status (Active, Discontinued, Expired)
  - Start Date
  - Document Date (when it was recorded in the system)
  - Source (Outside Provider, Patient Self-Report, OTC)
  - Comments (truncated; full text on hover tooltip)
- If no Non-VA medications exist: the section shows italic text "No non-VA medications documented"
- A **+ Add Non-VA Med** button appears in the Non-VA Medications section header

---

## Scenario 2: Add a Non-VA Medication (Happy Path)

### Steps
1. With patient 4 loaded on the Medications view
2. Click the **+ Add Non-VA Med** button in the Non-VA Medications section header
3. A **Non-VA Medication Entry** dialog window appears with the following fields:
   - Drug Name: TextBox with type-ahead search (searches drug formulary)
   - Dosage: TextBox
   - Route: ComboBox (Oral, Topical, Subcutaneous, Intramuscular, Inhalation, Ophthalmic, Otic, Rectal, Transdermal, Other)
   - Schedule: ComboBox (DAILY, BID, TID, QID, QHS, Q4H, Q6H, Q8H, Q12H, WEEKLY, PRN, OTHER)
   - Start Date: DatePicker (defaults to today)
   - Source: ComboBox:
     - Outside Provider
     - Patient Self-Report
     - OTC (Over-the-Counter)
   - Outside Provider Name (optional, enabled when Source = "Outside Provider"): TextBox
   - Comments: TextBox (multi-line)
4. Fill in:
   - Drug Name: type `Atorvastatin` -- select **ATORVASTATIN 20MG TAB** from the dropdown
   - Dosage: `20mg`
   - Route: **Oral**
   - Schedule: **QHS** (every night at bedtime)
   - Start Date: `01/15/2026`
   - Source: **Outside Provider**
   - Outside Provider Name: `DR. GARCIA, COMMUNITY CARDIOLOGY`
   - Comments: `Patient reports starting this medication after cardiology consult at community hospital in January.`
5. Click **Save**

### Expected Result
- The dialog closes
- A green notification appears in the status bar: "Non-VA medication recorded: ATORVASTATIN 20MG TAB"
- The new medication appears in the Non-VA Medications DataGrid:
  - Medication: "ATORVASTATIN 20MG TAB"
  - Status: "Active" (green status indicator)
  - Start Date: 01/15/2026
  - Document Date: today's date
  - Source: "Outside Provider"
- API call: `POST /api/outpatientpharmacy/4/prescriptions` (with non-VA flag)

---

## Scenario 3: Edit a Non-VA Medication

### Steps
1. With patient 4 loaded, locate the Atorvastatin entry in the Non-VA Medications DataGrid
2. Double-click the row (or right-click and select **Edit**)
3. The **Edit Non-VA Medication** dialog window opens with all fields pre-populated
4. Update:
   - Dosage: change from `20mg` to `40mg`
   - Comments: append ` Dose increased per outside cardiologist 03/2026.`
5. Click **Save**

### Expected Result
- The dialog closes
- A green notification appears in the status bar: "Non-VA medication updated."
- The DataGrid row updates to show:
  - Medication: "ATORVASTATIN 20MG TAB" (drug name unchanged; dosage field shows 40mg in detail)
  - The Comment column tooltip shows the updated text
- The Document Date updates to today (most recent documentation date)

---

## Scenario 4: Discontinue a Non-VA Medication

### Steps
1. With patient 4 loaded, locate an active Non-VA medication in the DataGrid
2. Right-click the row and select **Discontinue** (or select the row and click the **Discontinue** button)
3. A **Discontinue Non-VA Medication** dialog window appears:
   - Reason: ComboBox with options:
     - No Longer Taking
     - Duplicate Therapy
     - Adverse Reaction
     - Replaced by VA Medication
     - Per Outside Provider
     - Patient Deceased
     - Other
   - Effective Date: DatePicker (defaults to today)
   - Comment: TextBox (optional)
4. Fill in:
   - Reason: **No Longer Taking**
   - Comment: `Patient reports stopping after experiencing muscle aches. Will discuss with outside cardiologist.`
5. Click **Discontinue**

### Expected Result
- The dialog closes
- A green notification appears in the status bar: "Non-VA medication discontinued."
- The medication row updates:
  - Status changes to "Discontinued" (gray status indicator with strikethrough text or dimmed row)
  - The reason is visible on hover tooltip
- The medication remains in the list (not deleted) for historical record

---

## Scenario 5: Document Non-VA Medication Sources

### Steps
1. Click the **+ Add Non-VA Med** button
2. Add an OTC medication:
   - Drug Name: type `Ibuprofen` -- select **IBUPROFEN 200MG TAB** from the dropdown
   - Dosage: `400mg`
   - Route: **Oral**
   - Schedule: **TID PRN** (select **PRN** and add note)
   - Start Date: `06/01/2025`
   - Source: **OTC (Over-the-Counter)**
   - Outside Provider Name: field is disabled (grayed out) since Source is OTC
   - Comments: `Takes 2 tablets three times daily as needed for knee pain. Self-purchased.`
3. Click **Save**

### Expected Result
- Ibuprofen appears in the Non-VA Medications DataGrid with Source: "OTC"
- A green notification appears in the status bar: "Non-VA medication recorded: IBUPROFEN 200MG TAB"

### Steps (continued)
4. Click **+ Add Non-VA Med** again
5. Add a herbal supplement:
   - Drug Name: type `Fish Oil` -- if not found in formulary, the TextBox allows free-text entry with a note: "(Not in formulary -- free text entry)"
   - Dosage: `1000mg`
   - Route: **Oral**
   - Schedule: **DAILY**
   - Start Date: `01/01/2025`
   - Source: **Patient Self-Report**
   - Comments: `Patient reports taking fish oil supplement daily for cholesterol. Brand unknown.`
6. Click **Save**

### Expected Result
- Fish Oil appears in the Non-VA Medications DataGrid with Source: "Patient Self-Report"
- Since the drug was entered as free text (not from formulary), the Medication column shows the text with an italic style or footnote: "(non-formulary)"

---

## Scenario 6: View Non-VA Medication Detail

### Steps
1. With patient 4 loaded, click on the **ATORVASTATIN 20MG TAB** row in the Non-VA Medications DataGrid (or the active/discontinued version)
2. A detail panel expands below the DataGrid (or a detail dialog opens)

### Expected Result
- The detail panel shows all medication metadata:
  - **Drug Name**: ATORVASTATIN 20MG TAB
  - **Dosage**: 40mg (as updated in Scenario 3)
  - **Route**: Oral
  - **Schedule**: QHS
  - **Status**: Active or Discontinued (with reason if discontinued)
  - **Start Date**: 01/15/2026
  - **Document Date**: most recent documentation date
  - **Source**: Outside Provider
  - **Outside Provider**: DR. GARCIA, COMMUNITY CARDIOLOGY
  - **Comments**: full comment text (all appended comments shown chronologically)
  - **Documented By**: SMITH,JOHN A (the user who recorded it)
  - **Last Modified**: date/time of most recent update
- Action buttons in the detail panel:
  - **Edit** -- opens the edit dialog
  - **Discontinue** -- opens the discontinue dialog (only if status is Active)
  - **Print** -- prints medication detail
- If the medication is discontinued, the detail panel shows the discontinue reason, date, and comment in a separate "Discontinuation" section with a gray background
