# Non-VA Medication Review -- Pharmacist Human Test Script -- WPF UI

## Prerequisites

- **Login:** PHARM3 (MARTINEZ,CARLOS R -- Ambulatory Pharmacy) / Password: `smythVista1`
- **Patient:** 9
- **Pre-conditions:**
  1. The SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) must all be running.
  2. In the Navigation Panel, select **Medications**.
  3. Enter Patient ID `9` in the Patient ID field in the toolbar.
  4. Load demo medication data (both VA and Non-VA) via the API:
     ```
     POST /api/outpatientpharmacy/demo/load?patientId=9
     POST /api/medications/nonva/demo/load?patientId=9
     ```
  5. After demo load, the Medications view should display three sections separated by headers:
     - **Outpatient Medications** -- active VA prescriptions
     - **Non-VA Medications** -- medications from outside sources
     - **Inpatient Medications** -- active inpatient orders (may be empty)
  6. The Non-VA Medications section should contain at least 4 entries from the demo load:
     - FISH OIL 1000MG CAP (OTC, Patient Self-Report)
     - LISINOPRIL 20MG TAB (Outside Provider, Outside Pharmacy Records)
     - IBUPROFEN 200MG TAB (OTC, Patient Self-Report)
     - GLUCOSAMINE/CHONDROITIN 500-400MG TAB (Herbal/Supplement, Patient Self-Report)

---

## Scenario 1: View Non-VA Medications Profile

### Steps

1. In the Navigation Panel, select **Medications**.
2. Enter Patient ID: `9` in the Patient ID field in the toolbar.
3. Click **Load Medications**.
4. Scroll to the **Non-VA Medications** section header in the medications list.
5. Verify the Non-VA Medications DataGrid displays the following columns:
   - Medication
   - Status
   - Start Date
   - Document Date
   - Source
6. Confirm all 4 demo Non-VA medications are listed.
7. Click on the **LISINOPRIL 20MG TAB** row in the Non-VA DataGrid.
8. The detail panel opens on the right showing:
   - Medication: LISINOPRIL 20MG TAB
   - Status: ACTIVE
   - Source: Outside Pharmacy Records
   - Dosage: 20MG
   - Route: ORAL
   - Schedule: DAILY
   - Start Date: (date from demo load)
   - Document Date: (date recorded in system)
   - Documented By: (provider or pharmacist name)
   - Comments: (any notes about the medication)

### Expected Result

- The Non-VA Medications section is visually distinct from the Outpatient and Inpatient sections (separated by a section header with a different background color).
- All columns are populated for each Non-VA medication.
- The detail panel provides full medication information including source and documentation metadata.
- The date range label above the Non-VA section shows the current filter range.

---

## Scenario 2: Medication Reconciliation Review

### Steps

1. With Patient ID `9` loaded in the **Medications** view, observe both the Outpatient Medications and Non-VA Medications sections.
2. Click the **Reconciliation** button in the toolbar (or right-click the Non-VA Medications header and select **Medication Reconciliation**).
3. A dialog window opens with a side-by-side comparison layout:
   - Left panel: **VA Active Medications** DataGrid
   - Right panel: **Non-VA Medications** DataGrid
4. The system highlights potential issues:
   - **Duplicate Therapy**: LISINOPRIL 20MG (Non-VA) vs. LISINOPRIL 10MG (VA) -- highlighted in yellow with label "Duplicate - Same Drug Class".
   - **Interaction**: IBUPROFEN 200MG (Non-VA) vs. active VA medications -- highlighted in orange if an interaction exists.
   - **No Issues**: FISH OIL 1000MG and GLUCOSAMINE/CHONDROITIN show no highlighting.
5. The summary panel at the top shows:
   - Total VA Meds: (count)
   - Total Non-VA Meds: (count)
   - Duplicates Found: (count)
   - Interactions Found: (count)

### Expected Result

- The reconciliation view clearly identifies therapeutic duplicates by matching drug class.
- LISINOPRIL appears highlighted in both panels as a duplicate (same ACE inhibitor class prescribed by VA and outside provider).
- IBUPROFEN is flagged if the patient has a VA prescription for an anticoagulant or another NSAID.
- Non-flagged medications appear with no highlighting.
- The pharmacist can review each flagged item before closing the dialog.

---

## Scenario 3: Screen Non-VA Medication for Drug Interactions

### Steps

1. In the **Medications** view, scroll to the Non-VA Medications section.
2. Click on the **IBUPROFEN 200MG TAB** row in the Non-VA DataGrid.
3. The detail panel opens. Click the **Screen for Interactions** button.
4. The system runs the interaction check against all active VA medications. A progress indicator appears while the check runs.
5. The Interaction Results dialog window opens displaying:
   - Screened Drug: IBUPROFEN 200MG TAB
   - Checked Against: (count) active VA medications
   - Results DataGrid columns:
     - VA Medication
     - Interaction Severity (Contraindicated, Significant, Moderate, Minimal)
     - Description
     - Clinical Effect
     - Recommendation
6. Review each interaction entry.

### Expected Result

- If interactions are found, they are listed with severity color coding:
  - **Contraindicated**: red foreground
  - **Significant**: orange foreground
  - **Moderate**: yellow foreground
  - **Minimal**: no special formatting
- If no interactions are found, a message displays: "No drug interactions identified."
- The interaction check uses the same screening engine as VA prescription interactions (API: `POST /api/druginteractions/check`).

---

## Scenario 4: Document Non-VA Medication Review

### Steps

1. In the **Medications** view, scroll to the Non-VA Medications section.
2. Select the **FISH OIL 1000MG CAP** row in the Non-VA DataGrid.
3. Click the **Mark as Reviewed** button in the detail panel (or right-click and select **Mark as Reviewed**).
4. A dialog window appears with:
   - Medication: FISH OIL 1000MG CAP (read-only)
   - Review Status: ComboBox with options: Reviewed - No Action Needed, Reviewed - Action Needed, Reviewed - Discontinued by Patient
   - Pharmacist Comments: TextBox (multiline)
   - Review Date: DatePicker (defaults to today)
5. Select Review Status: `Reviewed - No Action Needed`.
6. Enter Comments: `Patient reports taking fish oil for cardiovascular health. No interactions with current VA medications. No concerns.`
7. Click **Save Review**.

### Expected Result

- A success toast notification appears: "Non-VA medication review documented."
- The FISH OIL row in the Non-VA DataGrid now shows a review indicator (checkmark icon in a Reviewed column or green foreground on the status).
- The detail panel shows:
  - Last Reviewed: (current date)
  - Reviewed By: MARTINEZ,CARLOS R
  - Review Status: Reviewed - No Action Needed
- Repeat for each Non-VA medication to mark the full profile as reviewed.

---

## Scenario 5: Flag Non-VA Medication Concern

### Steps

1. In the **Medications** view, scroll to the Non-VA Medications section.
2. Select the **LISINOPRIL 20MG TAB** row (the outside-provider duplicate).
3. Click the **Flag Concern** button in the detail panel.
4. A dialog window appears with:
   - Medication: LISINOPRIL 20MG TAB (read-only)
   - Concern Type: ComboBox with options: Duplicate Therapy, Dangerous Interaction, Contraindicated with Active Condition, Dose Concern, Other
   - Severity: ComboBox with options: Critical, High, Moderate, Low
   - Description: TextBox (multiline)
   - Notify Provider: CheckBox (checked by default)
   - Provider: ComboBox (lists patient's care team providers)
5. Select Concern Type: `Duplicate Therapy`.
6. Select Severity: `High`.
7. Enter Description: `Patient is taking LISINOPRIL 20MG from outside provider while also prescribed LISINOPRIL 10MG by VA. Risk of hypotension and hyperkalemia from duplicate ACE inhibitor therapy. Recommend provider reconcile doses.`
8. Leave Notify Provider checked. Select Provider: `DR. JANE SMITH`.
9. Click **Submit Flag**.

### Expected Result

- A success toast notification appears: "Concern flagged for provider review."
- The LISINOPRIL 20MG row in the Non-VA DataGrid now shows a flag icon (red exclamation mark) in a Flags column.
- The detail panel shows:
  - Flagged: Yes
  - Concern: Duplicate Therapy (High)
  - Flagged By: MARTINEZ,CARLOS R
  - Flagged Date: (current date)
- A notification is queued for the provider (DR. JANE SMITH).
- The flag persists until the provider acknowledges and resolves it.

---

## Scenario 6: Recommend Discontinuation of Non-VA Medication

### Steps

1. In the **Medications** view, scroll to the Non-VA Medications section.
2. Select the **IBUPROFEN 200MG TAB** row.
3. Click the **Recommend Action** button in the detail panel.
4. A dialog window appears with:
   - Medication: IBUPROFEN 200MG TAB (read-only)
   - Recommended Action: ComboBox with options: Discontinue, Dose Adjustment, Switch to VA Formulary Equivalent, Continue as Is, Refer to Provider
   - Rationale: TextBox (multiline)
   - Alternative Medication: TextBox (optional)
   - Urgency: ComboBox with options: Immediate, Routine, Informational
   - Create Recommendation Note: CheckBox (checked by default)
5. Select Recommended Action: `Discontinue`.
6. Enter Rationale: `Patient taking OTC ibuprofen 200mg PRN while on VA-prescribed warfarin. NSAID use increases bleeding risk with anticoagulant therapy. Recommend acetaminophen as alternative for mild pain.`
7. Enter Alternative Medication: `ACETAMINOPHEN 500MG TAB`.
8. Select Urgency: `Immediate`.
9. Leave Create Recommendation Note checked.
10. Click **Submit Recommendation**.

### Expected Result

- A success toast notification appears: "Recommendation submitted."
- A TIU note is created with Document Type: PHARMACY RECOMMENDATION. Verify via:
  ```
  GET /api/patient/9/notes
  ```
- The note body includes the recommendation details: medication name, recommended action, rationale, alternative, and urgency.
- The IBUPROFEN row in the Non-VA DataGrid shows a recommendation indicator (clipboard icon or "Rec" label in the Flags column).
- The detail panel shows:
  - Recommendation: Discontinue (Immediate)
  - Recommended By: MARTINEZ,CARLOS R
  - Alternative: ACETAMINOPHEN 500MG TAB

---

## Scenario 7: Add Missing Non-VA Medication

### Steps

1. In the **Medications** view, scroll to the Non-VA Medications section.
2. Click the **+ Add Non-VA Medication** button below the Non-VA DataGrid header.
3. A dialog window opens with the Non-VA Medication Entry form:
   - Medication Name: TextBox (with autocomplete from drug file)
   - Dosage: TextBox
   - Route: ComboBox (ORAL, TOPICAL, SUBCUTANEOUS, OPHTHALMIC, etc.)
   - Schedule: TextBox (e.g., DAILY, BID, TID, QHS, PRN)
   - Start Date: DatePicker
   - Source: ComboBox with options: Patient Self-Report, Outside Pharmacy Records, Transfer Records, Family/Caregiver Report
   - Status: ComboBox with options: ACTIVE, DISCONTINUED, ON HOLD
   - Comments: TextBox (multiline)
4. Fill in the form:
   - Medication Name: `METOPROLOL SUCCINATE 50MG TAB`
   - Dosage: `50MG`
   - Route: `ORAL`
   - Schedule: `DAILY`
   - Start Date: (6 months ago)
   - Source: `Patient Self-Report`
   - Status: `ACTIVE`
   - Comments: `Patient reports taking metoprolol prescribed by outside cardiologist Dr. Johnson at Regional Heart Center. Patient states taking for 6 months.`
5. Click **Save**.

### Expected Result

- A success toast notification appears: "Non-VA medication added."
- The Non-VA Medications DataGrid refreshes and now shows the new METOPROLOL SUCCINATE 50MG TAB entry.
- The new entry columns show:
  - Medication: METOPROLOL SUCCINATE 50MG TAB
  - Status: ACTIVE
  - Start Date: (entered date)
  - Document Date: (current date)
  - Source: Patient Self-Report
- The detail panel shows Documented By: MARTINEZ,CARLOS R.
- The system automatically runs an interaction screen against active VA medications (if enabled) and displays any findings in a toast or alert.

---

## Scenario 8: Non-VA Medication Expiration/Renewal Review

### Steps

1. In the **Medications** view, scroll to the Non-VA Medications section.
2. Click the **Review Status** button in the Non-VA section toolbar (or right-click the Non-VA header and select **Review Status Report**).
3. A dialog window opens showing the Non-VA Medication Review Status report:
   - DataGrid columns:
     - Medication
     - Source
     - Last Reviewed Date
     - Reviewed By
     - Days Since Review
     - Review Status (Current, Due, Overdue)
4. The system flags medications based on review age:
   - **Current** (green foreground): reviewed within the last 12 months.
   - **Due** (yellow foreground): reviewed 10-12 months ago (approaching 12-month threshold).
   - **Overdue** (red foreground): not reviewed in more than 12 months, or never reviewed.
5. The demo data should include at least one medication in each status category.
6. Click on an **Overdue** row (e.g., GLUCOSAMINE/CHONDROITIN).
7. The detail shows:
   - Last Reviewed: (date more than 12 months ago, or "Never")
   - Alert: "This medication has not been reviewed in over 12 months. Please verify with the patient that they are still taking this medication."
8. Click the **Review Now** button to open the review dialog (same as Scenario 4).
9. Select Review Status: `Reviewed - Discontinued by Patient`.
10. Enter Comments: `Patient reports no longer taking glucosamine/chondroitin. Stopped 3 months ago due to cost. Updating status to discontinued.`
11. Click **Save Review**.

### Expected Result

- A success toast notification appears: "Non-VA medication review documented."
- The GLUCOSAMINE/CHONDROITIN row in the Review Status DataGrid updates:
  - Last Reviewed Date: (current date)
  - Reviewed By: MARTINEZ,CARLOS R
  - Days Since Review: 0
  - Review Status: **Current** (green foreground)
- In the main Non-VA Medications DataGrid, the GLUCOSAMINE/CHONDROITIN status changes to **DISCONTINUED**.
- The review report summary at the top updates:
  - Total Non-VA Meds: (count)
  - Current Reviews: (count, incremented)
  - Due for Review: (count)
  - Overdue: (count, decremented)
