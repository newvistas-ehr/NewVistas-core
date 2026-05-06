# PDMP Integration -- Pharmacist Human Test Script -- WPF UI

## Prerequisites

- **Login:** PHARM1 (WILLIAMS,ROBERT L -- Clinical Pharmacy) / Password: `smythVista1`
- **Patient:** 4
- **Pre-conditions:**
  1. The SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) must all be running.
  2. In the Navigation Panel, select **Outpatient Pharmacy**.
  3. Enter Patient ID `4` in the Patient ID field in the toolbar and click **Load Prescriptions** to confirm the patient has active controlled substance prescriptions.
  4. Ensure the patient demographic record includes a valid SSN and Date of Birth (required for PDMP query matching). Verify via:
     ```
     GET /api/patient/4
     ```
  5. At least one active prescription for a Schedule II-IV controlled substance should be present.

---

## Scenario 1: Request PDMP Data

### Steps

1. In the Navigation Panel, select **Outpatient Pharmacy**.
2. Enter Patient ID: `4` in the Patient ID field in the toolbar.
3. Click **Load Prescriptions** to confirm the patient context.
4. From the menu bar, select **Tools > PDMP > Request PDMP Data**.
5. A dialog window appears with the following pre-populated fields:
   - Patient Name: (auto-populated from patient context)
   - Patient SSN (last 4): (auto-populated, masked)
   - Date of Birth: (auto-populated)
   - State: ComboBox defaulting to the patient's home state
6. Verify all fields are correct.
7. Click **Submit Request**.

### Expected Result

- A success toast notification appears: "PDMP request submitted successfully."
- The dialog closes.
- A PDMP Request ID is assigned (e.g., `PDMP-REQ-XXXXXXXX`).
- The request appears in the PDMP Review History with Status: **PENDING** (displayed with yellow foreground).

---

## Scenario 2: View PDMP Results

### Steps

1. After submitting a PDMP request (Scenario 1), allow the simulated response to return (or load demo PDMP data via the API):
   ```
   POST /api/pdmp/demo/load?patientId=4
   ```
2. From the menu bar, select **Tools > PDMP > Review PDMP Data**.
3. A dialog window opens displaying the PDMP Results panel.
4. Verify the DataGrid displays the following columns:
   - Drug Name
   - Prescriber
   - Pharmacy
   - Date Filled
   - Quantity
   - Days Supply
   - DEA Schedule
5. Scroll through the returned prescription history entries.
6. Verify the header summary shows: Total Prescriptions, Date Range, Number of Prescribers, Number of Pharmacies.

### Expected Result

- The PDMP Results DataGrid shows all controlled substance prescriptions from the state PDMP database.
- Each row shows the dispensing pharmacy name and address, prescriber name and DEA number, drug name with strength, fill date, quantity, days supply, and DEA schedule (II, III, IV, or V).
- Entries are sorted by Date Filled (most recent first).
- The summary header provides aggregate counts for quick review.

---

## Scenario 3: Review PDMP for Controlled Substance Concern

### Steps

1. Open the PDMP Results dialog (Tools > PDMP > Review PDMP Data).
2. The demo data includes entries that trigger concern indicators:
   - Multiple prescribers (3 or more) for the same drug class within a 90-day window.
   - Multiple pharmacies (3 or more) filling controlled substances within a 90-day window.
   - Overlapping prescriptions for the same drug class (overlapping date ranges based on fill date + days supply).
3. Observe the **Concern Indicators** panel at the top of the dialog:
   - Multiple Prescribers: **Yes** (displayed with red foreground) -- lists prescriber names.
   - Multiple Pharmacies: **Yes** (displayed with red foreground) -- lists pharmacy names.
   - Overlapping Prescriptions: **Yes** (displayed with red foreground) -- lists overlapping drug pairs.
4. In the DataGrid, rows that contribute to a concern are highlighted in yellow.
5. Click on a highlighted row to see the overlap detail in the lower detail panel:
   - Overlapping Drug: (drug name)
   - Overlap Period: (start date -- end date)
   - Other Prescriber: (name, DEA #)
   - Other Pharmacy: (name, address)

### Expected Result

- Concern indicators are prominently displayed when criteria are met.
- Highlighted rows clearly identify the prescriptions contributing to concern.
- The detail panel provides sufficient information for the pharmacist to assess risk.
- If no concerns are detected, the Concern Indicators panel shows all indicators as **No** (displayed with green foreground).

---

## Scenario 4: Cancel a Pending PDMP Request

### Steps

1. Submit a new PDMP request via **Tools > PDMP > Request PDMP Data** (follow Scenario 1 steps).
2. Before results return (while Status is still PENDING), select **Tools > PDMP > Cancel PDMP Request**.
3. A dialog window appears listing all pending PDMP requests for the current patient in a DataGrid:
   - Request ID
   - Submitted Date
   - Submitted By
   - Status: PENDING
4. Select the pending request row in the DataGrid.
5. Click **Cancel Request**.
6. A confirmation dialog appears: "Are you sure you want to cancel PDMP request {Request ID}?"
7. Click **Yes**.

### Expected Result

- A success toast notification appears: "PDMP request cancelled."
- The request status changes to **CANCELLED** (displayed with gray foreground).
- The cancelled request no longer appears in the active pending list.
- The cancellation is recorded in the PDMP Review History (Scenario 7).

---

## Scenario 5: Document PDMP Review in Note

### Steps

1. Open the PDMP Results dialog (Tools > PDMP > Review PDMP Data).
2. Review the PDMP results (Scenario 2 or 3).
3. At the bottom of the PDMP Results dialog, click **Document Review**.
4. A new dialog window appears with:
   - Review Outcome: ComboBox with options: No Concerns Identified, Concerns Identified - Provider Notified, Concerns Identified - Prescription Held, Concerns Identified - Law Enforcement Notified
   - Reviewer Comments: TextBox (multiline)
   - Attach to Note: CheckBox (checked by default)
   - Note Title: TextBox (pre-populated: "PDMP Review - [Patient Name] - [Date]")
   - Cosigner: ComboBox (optional, lists available cosigners)
5. Select Review Outcome: `Concerns Identified - Provider Notified`.
6. Enter Comments: `Multiple prescribers identified for opioid class. Contacted Dr. Smith at 555-0100 to discuss findings. Provider aware and will address at next visit.`
7. Leave Attach to Note checked.
8. Select Cosigner: `DR. JANE SMITH` from the ComboBox (required when documenting concerns).
9. Click **Save Review**.

### Expected Result

- A success toast notification appears: "PDMP review documented successfully."
- A TIU note is created with the PDMP review content. Verify via:
  ```
  GET /api/patient/4/notes
  ```
- The note title contains "PDMP Review" and the note body includes the review outcome, comments, and PDMP summary data.
- The note status shows UNCOSIGNED (pending cosigner signature).
- The PDMP Review History (Scenario 7) shows the review entry with outcome and reviewer name.

---

## Scenario 6: PDMP Review Required Before Fill

### Steps

1. In the Navigation Panel, select **Outpatient Pharmacy**.
2. Enter Patient ID: `4` in the Patient ID field in the toolbar and click **Load Prescriptions**.
3. Create a new Schedule II prescription via the API:
   ```
   POST /api/outpatientpharmacy/4/prescriptions
   {
     "drugName": "HYDROCODONE/APAP 5-325MG TAB",
     "drugId": "50-HYDROCODONE",
     "dosage": "5-325MG",
     "route": "ORAL",
     "schedule": "Q6H PRN",
     "sig": "TAKE ONE TABLET EVERY 6 HOURS AS NEEDED FOR PAIN",
     "daysSupply": 30,
     "quantity": 120,
     "refills": 0,
     "providerId": "PROV-001",
     "providerName": "DR. JANE SMITH",
     "priority": "ROUTINE",
     "comments": "DEA Schedule II - PDMP review required before dispensing"
   }
   ```
4. Click **Load Prescriptions** to refresh. Select the HYDROCODONE row.
5. Verify the prescription via the **Verify** button.
6. Attempt to click the **Fill** button.

### Expected Result

- A red error notification appears in the status bar: "PDMP review required before dispensing Schedule II-IV controlled substances."
- The Fill action is blocked.
- A warning banner appears on the prescription detail panel: "PDMP Review Not Completed" (displayed with orange foreground).
- After completing a PDMP review (Scenarios 1-5) for this patient, return to the Outpatient Pharmacy view and attempt Fill again. The fill proceeds normally.

---

## Scenario 7: View PDMP Review History

### Steps

1. From the menu bar, select **Tools > PDMP > Review PDMP Data**.
2. In the PDMP Results dialog, click the **Review History** TabItem.
3. The Review History DataGrid displays all previous PDMP queries and reviews for the current patient.
4. Verify the columns:
   - Request Date
   - Reviewer
   - Review Outcome
   - State Queried
   - Results Count
   - Concerns Found (Yes/No)
   - Documented in Note (Yes/No)
5. Click on a row to view the full review detail in the lower panel, including the reviewer comments and any linked note ID.

### Expected Result

- All previous PDMP requests and reviews are listed chronologically (most recent first).
- Each entry shows who performed the review, when, and what outcome was documented.
- Entries with concerns are highlighted in yellow.
- Cancelled requests (Scenario 4) appear with Status: CANCELLED and gray foreground.
- The Review History provides a complete audit trail of PDMP activity for the patient.

---

## Scenario 8: PDMP Data Unavailable

### Steps

1. From the menu bar, select **Tools > PDMP > Request PDMP Data**.
2. In the request dialog, select a State where the PDMP system is simulated as unavailable (e.g., select `XX - Test State Unavailable` from the State ComboBox).
3. Click **Submit Request**.
4. The system attempts to connect and returns an error after timeout.
5. A dialog window appears with the message: "PDMP data is currently unavailable for the selected state. Reason: State PDMP system is not responding."
6. The dialog presents options:
   - **Document Reason and Proceed**: RadioButton -- allows the pharmacist to document the unavailability and proceed with dispensing.
   - **Retry Request**: RadioButton -- attempts the query again.
   - **Cancel**: RadioButton -- cancels the request.
7. Select **Document Reason and Proceed**.
8. A Reason TextBox appears. Enter: `State PDMP system down per IT notification. Verified patient history via pharmacy records. No concerns identified.`
9. Click **Confirm**.

### Expected Result

- A success toast notification appears: "PDMP unavailability documented."
- The PDMP Review History records an entry with:
  - Review Outcome: **PDMP Unavailable - Documented**
  - Reviewer Comments: the entered reason text
  - Results Count: 0
- The controlled substance prescription can now be filled without the PDMP block (Scenario 6 enforcement is bypassed for this fill only).
- A note is created documenting the PDMP unavailability and the pharmacist's rationale for proceeding.
