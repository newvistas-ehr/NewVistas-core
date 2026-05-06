# Dispensing, Counseling, and Labels -- Pharmacist Human Test Script -- WPF UI

## Prerequisites

- **Login:** PHARM3 (MARTINEZ,CARLOS R -- Ambulatory Pharmacy) / Password: `smythVista1`
- **Patient:** 4
- **Pre-conditions:**
  1. The SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) must all be running.
  2. In the Navigation Panel, select **Outpatient Pharmacy**.
  3. Load demo data: enter Patient ID `4` in the toolbar and use the API: `POST /api/outpatientpharmacy/demo/load?patientId=4`
  4. After demo load, prescriptions should show as ACTIVE, verified, and filled.

---

## Scenario 1: Record Dispense with NDC and Lot Number

### Steps

1. In the Navigation Panel, select **Outpatient Pharmacy**.
2. Enter Patient ID: `4` in the Patient ID field in the toolbar and click **Load Prescriptions**.
3. Click on **LISINOPRIL 10MG TAB** in the DataGrid to select it.
4. The detail panel opens. Confirm the prescription is ACTIVE and has a FillDate.
5. Click the **Record Dispense** button (visible because FillDate is set), or right-click and select **Record Dispense**.
6. The dispense form fields appear. This form may be inline or in a dialog window. Fill in:
   - NDC: `68180-0598-01`
   - Lot#: `2026A-0329`
   - Pharmacist: `PHARM3`
7. Click **Submit** (or the dispense confirmation button).

### Expected Result

- Action message: "Action 'dispense' completed successfully."
- The detail panel refreshes and now shows:
  - **NDC dispensed:** 68180-0598-01 Lot: 2026A-0329
- The LastDispenseDate is updated to the current date/time.

---

## Scenario 2: Print Prescription Label

### Steps

1. Select **LISINOPRIL 10MG TAB** in the DataGrid (or any verified, active prescription).
2. Click the **Print Label** button in the action bar (or right-click and select **Print Label**). WPF also supports Ctrl+P for print where applicable.

### Expected Result

- Action message: "Action 'printlabel' completed successfully."
- The detail panel refreshes and shows:
  - **Label printed:** (current date/time)
- The IsLabelPrinted flag is now true.
- An Rx Number may be auto-assigned (e.g., RX20260329001).

---

## Scenario 3: Generate and View Label Content (Structured Data)

### Steps

1. Select **LISINOPRIL 10MG TAB** in the DataGrid (must be verified first for label generation).
2. Click the **View Label** button (appears after verification), or right-click and select **View Label**.
3. The label content opens in a dialog window with structured label data.

### Expected Result

- The label content includes structured fields such as:
  - Patient Name
  - Rx Number
  - Drug Name and Dosage
  - SIG (directions): "TAKE ONE TABLET BY MOUTH DAILY FOR BLOOD PRESSURE"
  - Quantity
  - Refills remaining
  - Provider name
  - Pharmacy name and address
  - Issue date
  - Expiration date
  - Warnings/auxiliary labels (if applicable)
- This structured data represents what would be printed on the prescription label.

---

## Scenario 4: Set Counseling Required Flag

### Steps

1. Select **METFORMIN 500MG TAB** in the prescription DataGrid.
2. Check if the Counsel column shows a flag icon. If not, the CounselingRequired flag is false.
3. Click the **Require Counseling** button (in the action bar), or right-click and select **Require Counseling**.
4. The counseling flag toggles.

### Expected Result

- Action message: "Action 'counseling' completed successfully."
- The detail panel refreshes:
  - The counseling flag section now shows: "Patient counseling required"
- In the prescription DataGrid, the Counsel column now shows the flag icon.
- The button text changes to **Clear Counseling** (toggle).
- Clicking **Clear Counseling** removes the flag.

---

## Scenario 5: Complete Patient Counseling Session with Notes

### Steps

1. Select a prescription with CounselingRequired = true (from Scenario 4) in the DataGrid.
2. The detail panel shows "Patient counseling required" with a **Complete Counseling** button visible.
3. Click **Complete Counseling**.
4. A dialog window appears with the counseling form:
   - Pharmacist ID: enter `PHARM3`
   - Notes: enter `Counseled patient on metformin administration: take with meals to reduce GI side effects. Avoid excessive alcohol. Report any symptoms of lactic acidosis (muscle pain, weakness, difficulty breathing). Patient verbalized understanding.`
5. Click **Submit** (or the counseling completion button).

### Expected Result

- Action message: "Action 'counseling-complete' completed successfully."
- The detail panel refreshes and now shows:
  - "Counseling completed MM/DD/YYYY by PHARM3"
- The **Complete Counseling** button disappears (counseling already completed).
- The CounselingCompleted flag is true.

---

## Scenario 6: View Refill History

### Steps

1. Select **LISINOPRIL 10MG TAB** in the DataGrid (a prescription that has been filled and/or refilled).
2. Scroll down in the detail panel to the **Refill History** section.

### Expected Result

- The Refill History DataGrid shows columns: #, Date, Qty, Days, Rx#, Pharmacist.
- The first entry shows:
  - #: Original (FillNumber = 0)
  - Date: the original fill date
  - Qty: 90
  - Days: 90
  - Rx#: the assigned Rx number
  - Pharmacist: RPH-001 (from demo load)
- If refills have been processed, subsequent rows show:
  - #: Refill 1, Refill 2, etc.
  - Date: the refill date
  - Qty and Days matching the prescription
- Also accessible via API: `GET /api/outpatientpharmacy/4/prescriptions/{rxId}/refillhistory`

---

## Scenario 7: Check Refill Eligibility (Eligible)

### Steps

1. Select **ATORVASTATIN 40MG TAB** in the DataGrid (filled ~10 days ago with 90-day supply, 3 refills authorized).
2. The Refill Status section should show refill eligibility information.
3. If sufficient time has passed (>75% of days supply consumed), the status shows Eligible.
4. Alternatively, check via API:
   ```
   GET /api/outpatientpharmacy/4/prescriptions/{rxId}/refill-eligibility
   ```

### Expected Result

- Refill Status section displays:
  - **Eligible** (green status indicator) -- if enough time has passed
  - Refills remaining: X of 3 authorized
  - No blocking reasons listed
- The **Refill** button is enabled (not grayed out).

---

## Scenario 8: Check Refill Eligibility (Not Eligible -- Reasons Displayed)

### Steps

1. Select **METFORMIN 500MG TAB** in the DataGrid (30-day supply, filled recently from demo).
2. The Refill Status section shows eligibility information.
3. If the fill was recent (less than 75% consumed), the section displays:

### Expected Result

- Refill Status section displays:
  - **Not Eligible** (red status indicator)
  - Reasons list (displayed with red foreground, bulleted):
    - "Too early to refill" (if IsTooEarly is true)
    - The percentage consumed is displayed (e.g., "33% consumed")
    - EarliestRefillDate shows when the refill becomes available (e.g., "Next eligible: MM/DD/YYYY")
  - Refills remaining: X of 11 authorized
- The **Refill** button is disabled (grayed out).
- Additional possible reasons if applicable:
  - "No refills remaining" (if RefillsRemaining = 0)
  - "Prescription has expired" (if past ExpirationDate)
  - "Prescription is not active" (if status is not ACTIVE)
