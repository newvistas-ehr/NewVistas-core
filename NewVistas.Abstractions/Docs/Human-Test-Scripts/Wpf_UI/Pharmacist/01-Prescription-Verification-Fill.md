# Prescription Verification, Fill, and Refill -- Pharmacist Human Test Script -- WPF UI

## Prerequisites

- **Login:** PHARM3 (MARTINEZ,CARLOS R -- Ambulatory Pharmacy) / Password: `smythVista1`
- **Patient:** 4
- **Pre-conditions:**
  1. The SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) must all be running.
  2. In the Navigation Panel, select **Outpatient Pharmacy**.
  3. Enter Patient ID `4` in the Patient ID field in the toolbar and click **Load Demo** (or use the API: `POST /api/outpatientpharmacy/demo/load?patientId=4`).
  4. After demo load succeeds, the prescription list should display 3 medications:
     - LISINOPRIL 10MG TAB (ORAL, DAILY, 90-day supply, 5 refills)
     - METFORMIN 500MG TAB (ORAL, BID, 30-day supply, 11 refills)
     - ATORVASTATIN 40MG TAB (ORAL, QHS, 90-day supply, 3 refills)
  5. All three should show Status: ACTIVE, Verified: checkmark, and a Provider name.

---

## Scenario 1: Happy Path -- Load Prescriptions, Select Rx, Verify, Fill

### Steps

1. In the Navigation Panel, select **Outpatient Pharmacy**.
2. Enter Patient ID: `4` in the Patient ID field in the toolbar.
3. Click **Load Prescriptions**.
4. Verify the prescription DataGrid loads with columns: Drug, Dosage, Status, Priority, Refills Left, Expires, Verified, Counsel, Provider.
5. Create a new unverified prescription via the API:
   ```
   POST /api/outpatientpharmacy/4/prescriptions
   {
     "drugName": "AMLODIPINE 5MG TAB",
     "drugId": "50-AMLODIPINE",
     "dosage": "5MG",
     "route": "ORAL",
     "schedule": "DAILY",
     "sig": "TAKE ONE TABLET BY MOUTH DAILY",
     "daysSupply": 30,
     "quantity": 30,
     "refills": 3,
     "providerId": "PROV-001",
     "providerName": "DR. JANE SMITH",
     "pharmacyId": "PHARM-001",
     "pharmacyName": "MAIN PHARMACY",
     "priority": "ROUTINE"
   }
   ```
6. Click **Load Prescriptions** again. AMLODIPINE should appear with Verified column empty.
7. Click on the AMLODIPINE row in the DataGrid to select it. The detail panel opens.
8. Confirm detail panel shows: Rx ID, Drug: AMLODIPINE 5MG TAB, Status: ACTIVE, Verified: (no checkmark), Priority: ROUTINE.
9. Click the **Verify** button (or right-click and select **Verify**).
10. The action message shows: "Action 'verify' completed successfully."

### Expected Result

- The detail panel now shows: Verified by: RPH-CURRENT on (current date/time).
- The prescription DataGrid refreshes; AMLODIPINE row shows Verified: checkmark.
- Fill button is now available.

---

## Scenario 2: Refill an Eligible Prescription

### Steps

1. In the prescription list, click on **LISINOPRIL 10MG TAB** (already verified and filled from demo load).
2. The detail panel opens showing refill eligibility.
3. Confirm the **Refill Status** section shows:
   - Status: **Eligible** (green status indicator)
   - Refills remaining: some number of 5 authorized
4. Click the **Refill** button (or right-click and select **Refill**).

### Expected Result

- Action message: "Action 'refill' completed successfully."
- Refills Left column decrements by 1.
- In the Refill History DataGrid at the bottom of the detail panel, a new row appears:
  - Fill Number: Refill 1 (or next sequential number)
  - Date: current date
  - Qty: 90
  - Days: 90

---

## Scenario 3: Refill Blocked -- No Refills Remaining (RefillsRemaining=0)

### Steps

1. Create a prescription with 0 refills via the API:
   ```
   POST /api/outpatientpharmacy/4/prescriptions
   {
     "drugName": "PREDNISONE 10MG TAB",
     "drugId": "50-PREDNISONE",
     "dosage": "10MG",
     "route": "ORAL",
     "schedule": "DAILY x7",
     "sig": "TAKE ONE TABLET BY MOUTH DAILY FOR 7 DAYS",
     "daysSupply": 7,
     "quantity": 7,
     "refills": 0,
     "providerId": "PROV-001",
     "providerName": "DR. JANE SMITH",
     "priority": "ROUTINE"
   }
   ```
2. Verify and fill the prescription using the API:
   ```
   POST /api/outpatientpharmacy/4/prescriptions/{rxId}/verify
   { "pharmacistId": "PHARM3" }
   POST /api/outpatientpharmacy/4/prescriptions/{rxId}/fill
   { "fillDate": null }
   ```
3. Click **Load Prescriptions** and select the PREDNISONE row in the DataGrid.
4. The Refill Status section should show: **Not Eligible** (red status indicator).
5. The Reasons list should include: "No refills remaining."
6. The **Refill** button should be disabled (grayed out).

### Expected Result

- Refill Status shows **Not Eligible** with red status indicator.
- Reason displayed: "No refills remaining."
- Refill button is disabled and cannot be clicked.

---

## Scenario 4: Refill Blocked -- Too Early (75% Rule Not Met)

### Steps

1. Select **METFORMIN 500MG TAB** (30-day supply, filled ~10 days ago from demo).
2. The Refill Status should show either Eligible or Not Eligible depending on how much time has passed.
3. If the fill was only 10 days ago on a 30-day supply, the 75% threshold = 22.5 days.
4. The section should show: **Not Eligible** with reason: "Too early to refill."
5. It should display: "XX% consumed" and "Next eligible: MM/DD/YYYY".

### Expected Result

- Refill Status: **Not Eligible** (red status indicator).
- IsTooEarly indicator shows the percentage consumed.
- EarliestRefillDate shows when the refill becomes available.
- Refill button is disabled.

---

## Scenario 5: Refill Blocked -- Expired Prescription

### Steps

1. Create a prescription with a past expiration date via the API:
   ```
   POST /api/outpatientpharmacy/4/prescriptions
   {
     "drugName": "EXPIRED HYDROCHLOROTHIAZIDE 25MG",
     "drugId": "50-HCTZ",
     "dosage": "25MG",
     "route": "ORAL",
     "schedule": "DAILY",
     "sig": "TAKE ONE TABLET DAILY",
     "daysSupply": 30,
     "quantity": 30,
     "refills": 5,
     "providerId": "PROV-001",
     "providerName": "DR. JANE SMITH",
     "priority": "ROUTINE"
   }
   ```
2. The prescription expiration is set based on creation date plus one year by default. To test an expired Rx, you must either wait for expiration or verify via API that the refill eligibility check returns an expired reason. This scenario is primarily verified through the API:
   ```
   GET /api/outpatientpharmacy/4/prescriptions/{rxId}/refill-eligibility
   ```

### Expected Result

- When a prescription's ExpirationDate is in the past, the refill eligibility returns IsEligible: false.
- Reason: "Prescription has expired."

---

## Scenario 6: Refill Blocked -- Schedule II (No Refills Allowed by Law)

### Steps

1. Create a Schedule II prescription (refills must be 0 by law):
   ```
   POST /api/outpatientpharmacy/4/prescriptions
   {
     "drugName": "OXYCODONE 5MG TAB",
     "drugId": "50-OXYCODONE",
     "dosage": "5MG",
     "route": "ORAL",
     "schedule": "Q6H PRN",
     "sig": "TAKE ONE TABLET EVERY 6 HOURS AS NEEDED FOR PAIN",
     "daysSupply": 30,
     "quantity": 120,
     "refills": 0,
     "providerId": "PROV-001",
     "providerName": "DR. JANE SMITH",
     "priority": "ROUTINE",
     "comments": "DEA Schedule II - no refills permitted"
   }
   ```
2. Verify and fill the prescription.
3. Select it in the DataGrid. The Refill Status should show: **Not Eligible**.
4. Reason: "No refills remaining."

### Expected Result

- Schedule II controlled substances are created with Refills=0.
- Refill button is disabled.
- No refills can be processed.

---

## Scenario 7: Fill Blocked -- DUR Not Cleared

### Steps

1. This scenario requires the Drug Utilization Review workflow. Before attempting to fill a prescription, perform a DUR that fails:
   - In the Navigation Panel, select **Drug Utilization Review**.
   - Enter Patient ID: `4` in the Patient ID field in the toolbar.
   - Click **+ Perform DUR**.
   - Fill in: Prescription ID = (the Rx ID from a new prescription), Drug Name = `WARFARIN 5MG TAB`, Controlled Substance = No, Days Supply = 90, Max Days Supply = 30.
   - Click **Run DUR**.
2. The DUR should return a FAILED result with DaysSupplyExceeded check.
3. Return to the **Outpatient Pharmacy** view by selecting it in the Navigation Panel, select the prescription, and attempt to **Fill**.

### Expected Result

- If the system enforces DUR before fill, the fill attempt returns an error: "DUR assessment has not been cleared."
- The prescription remains unfilled until the DUR is either overridden or the parameters corrected.
- See Script 02 (Drug Utilization Review) for the full DUR workflow.

---

## Scenario 8: Fill Blocked -- Interaction Screening Not Cleared

### Steps

1. This scenario requires the Interaction Blocking workflow. Before fill:
   - In the Navigation Panel, select **Interaction Blocking**.
   - Enter Patient ID: `4` in the Patient ID field in the toolbar.
   - Click **+ Screen Rx**.
   - Prescription ID: (the Rx ID), Drug Name: `WARFARIN 5MG TAB`.
   - New Drug Ingredient IEN: `1190`, New Drug Ingredient Name: `WARFARIN`.
   - Existing Med Ingredient IEN: `3345`, Existing Med Ingredient Name: `ASPIRIN`.
   - Click **Run Screen**.
2. If a Significant or Contraindicated interaction is found, the status shows **Blocked**.
3. Return to the **Outpatient Pharmacy** view by selecting it in the Navigation Panel and attempt to fill.

### Expected Result

- The fill attempt returns an error indicating the prescription is blocked by a drug interaction.
- The pharmacist must override the interaction in the Interaction Blocking view before fill.
- See Script 03 (Interaction Screening) for override steps.

---

## Scenario 9: Hold Prescription with Reason, Then Resume

### Steps

1. In the prescription list, click on **ATORVASTATIN 40MG TAB** in the DataGrid.
2. The detail panel shows Status: ACTIVE.
3. Click the **Hold** button (or right-click and select **Hold**).
4. The action message shows: "Action 'hold' completed successfully."
5. The detail panel refreshes. Status changes to **HOLD** (orange status indicator).
6. The Hold Reason field shows: "Placed on hold by pharmacist".
7. Confirm the action bar now shows only the **Resume** button (not Fill, Refill, Discontinue, Hold).
8. Click the **Resume** button (or right-click and select **Resume**).

### Expected Result

- After Hold: Status = HOLD, Hold Reason displayed, only Resume button visible.
- After Resume: Status = ACTIVE, Hold Reason cleared, all action buttons restored.
- Action message: "Action 'resume' completed successfully."

---

## Scenario 10: Discontinue Prescription with Reason

### Steps

1. In the prescription list, click on **METFORMIN 500MG TAB** in the DataGrid.
2. Click the **Discontinue** button (or right-click and select **Discontinue**).
3. A dialog window appears with a "Discontinue reason" TextBox and Confirm D/C and Cancel buttons.
4. Enter reason: `Patient developed lactic acidosis - discontinue metformin per provider order`
5. Click **Confirm D/C**.

### Expected Result

- Action message: "Action 'discontinue' completed successfully."
- Status changes to **DISCONTINUED** (red status indicator).
- The D/C Reason field in detail shows: "Patient developed lactic acidosis - discontinue metformin per provider order".
- No action buttons remain (Fill, Refill, Hold, Verify are all hidden for discontinued prescriptions).
- The prescription DataGrid row shows Status: DISCONTINUED displayed with red foreground.
