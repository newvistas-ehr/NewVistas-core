# Point of Sale Pharmacy Claims -- Pharmacist Human Test Script -- WPF UI

## Prerequisites

- **Login:** PHARM3 (MARTINEZ,CARLOS R -- Ambulatory Pharmacy) / Password: `smythVista1`
- **Patient:** 35
- **Pre-conditions:**
  1. The SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) must all be running.
  2. In the Navigation Panel, select **Pharmacy POS**.
  3. Ensure Patient 35 has active prescriptions and a benefit plan (from Script 08 demo data).

---

## Scenario 1: Submit B1 Billing Claim

### Steps

1. In the Navigation Panel, select **Pharmacy POS**.
2. The **POS Claims** TabItem (tab 0) should be active.
3. Enter Patient ID: `35` in the Patient ID field in the toolbar and click **Load** (may show no claims initially).
4. Click **New Claim**.
5. Fill in the claim form:
   - Transaction Type: **B1 -- Billing**
   - BIN: `610014`
   - PCN: `OHCARD`
   - NCPDP Version: `D.0`
   - Prescription ID: `RX-POS-001`
   - Group Number: `VAGRP001`
   - Cardholder ID: `MBR-35-001`
   - Relationship Code: `01`
   - Insurer ID: `INS-TRICARE`
   - Insurer Name: `TRICARE`
   - NDC: `00071-0155-23`
   - Drug Name: `LISINOPRIL 10MG TAB`
   - Qty Dispensed: `90`
   - Days Supply: `90`
   - Date of Service: (today's date, using the DatePicker)
   - Ingredient Cost: `12.50`
   - Dispensing Fee: `2.50`
   - U&C (Usual and Customary): `22.00`
   - Pharmacy NCPDP ID: `1234567`
   - Pharmacist Name: `MARTINEZ,CARLOS R`
   - Prescriber NPI: `1234567890`
   - Prescriber Name: `DR. JANE SMITH`
6. Click **Submit Claim**.

### Expected Result

- The claim appears in the POS Claims DataGrid:
  - Date of Service: today's date
  - Drug: LISINOPRIL 10MG TAB
  - Type: **B1** status indicator (blue)
  - Status: **Pending** (yellow status indicator)
  - Insurer: TRICARE
  - Ins Paid: (dash -- not yet adjudicated)
  - Patient Resp: (dash -- not yet adjudicated)
  - Action buttons: **View** and **Adjudicate**

---

## Scenario 2: Adjudicate Claim -- Approved with Copay

### Steps

1. In the POS Claims DataGrid, find the PENDING claim from Scenario 1.
2. Click the **Adjudicate** button (or right-click and select **Adjudicate**).
3. A dialog window appears with the Adjudicate Claim form:
   - Status: select **Paid** from the ComboBox
   - Insurance Paid Amount: enter `7.50`
   - Patient Responsibility: enter `5.00`
   - Copay Amount: enter `5.00`
   - Authorization Number: enter `AUTH-2026-001234`
   - Rejection Codes: (leave blank)
   - Notes: enter `Claim approved. Tier 1 copay applied.`
4. Click **Adjudicate**.

### Expected Result

- The claim status changes to **Paid** (green status indicator).
- Ins Paid column shows: $7.50
- Patient Resp column shows: $5.00
- Click **View** to see full claim details:
  - Authorization Number: AUTH-2026-001234
  - Copay: $5.00
  - All submitted fields are preserved.

---

## Scenario 3: Adjudicate Claim -- Denied with Rejection Codes

### Steps

1. Submit a new B1 claim (repeat Scenario 1 steps with different Rx):
   - Prescription ID: `RX-POS-002`
   - NDC: `00093-7180-01`
   - Drug Name: `ATORVASTATIN 40MG TAB`
   - Qty Dispensed: `90`
   - Days Supply: `90`
   - Ingredient Cost: `45.00`
   - Dispensing Fee: `2.50`
   - U&C: `65.00`
2. Click **Submit Claim**.
3. On the PENDING claim, click **Adjudicate** (or right-click and select **Adjudicate**).
4. Fill in:
   - Status: select **Rejected** from the ComboBox
   - Insurance Paid Amount: `0`
   - Patient Responsibility: `0`
   - Copay Amount: `0`
   - Authorization Number: (leave blank)
   - Rejection Codes: `75,76` (NCPDP rejection codes: 75=Prior Auth Required, 76=Plan Limitations Exceeded)
   - Notes: `Prior authorization required for brand name statin. Generic alternative available.`
5. Click **Adjudicate**.

### Expected Result

- The claim status changes to **Rejected** (red status indicator).
- Ins Paid: $0.00
- Patient Resp: $0.00
- Click **View** to see:
  - Rejection Codes: 75, 76
  - Notes with the denial reason.

---

## Scenario 4: Submit B2 Reversal Claim

### Steps

1. Click **New Claim**.
2. Fill in:
   - Transaction Type: **B2 -- Reversal**
   - BIN: `610014`
   - PCN: `OHCARD`
   - NCPDP Version: `D.0`
   - Prescription ID: `RX-POS-001` (the originally paid claim)
   - Group Number: `VAGRP001`
   - Cardholder ID: `MBR-35-001`
   - Insurer ID: `INS-TRICARE`
   - Insurer Name: `TRICARE`
   - NDC: `00071-0155-23`
   - Drug Name: `LISINOPRIL 10MG TAB`
   - Qty Dispensed: `90`
   - Days Supply: `90`
   - Ingredient Cost: `12.50`
   - Dispensing Fee: `2.50`
   - Original Claim ID: (the Claim ID from Scenario 1, visible in the View detail)
3. Click **Submit Claim**.

### Expected Result

- The reversal claim appears in the DataGrid:
  - Type: **B2** status indicator
  - Status: **Pending**
- After adjudication (mark as Reversed or Paid), the original claim's insurance payment is effectively reversed.

---

## Scenario 5: View Claim History by Status

### Steps

1. On the POS Claims TabItem, use the status filter ComboBox:
   - Select **Pending** and observe the filter applies (view reloads).
   - Select **Paid** to see only approved claims.
   - Select **Rejected** to see only denied claims.
   - Select **Reversed** to see reversal claims.
   - Select (blank/All Statuses) to see all claims.

### Expected Result

- Each filter shows only claims matching the selected status.
- Status options in the ComboBox: Pending, Transmitted, Paid, Rejected, Reversed, DuplicatePaid, PartialPay, Cancelled.
- The claim count updates with each filter change.

---

## Scenario 6: Configure POS Insurer (System-Level)

### Steps

1. Click the **Insurers** TabItem (tab 2).
2. The TabItem loads the list of configured POS insurers.
3. If no insurers are configured, the list may be empty.
4. Configure a new insurer via the API:
   ```
   POST /api/pharmacypos/insurers
   {
     "insurerId": "INS-TRICARE",
     "insurerName": "TRICARE",
     "bin": "610014",
     "pcn": "OHCARD",
     "ncpdpVersion": "D.0",
     "processorName": "Express Scripts",
     "helpDeskPhone": "1-800-555-0199",
     "isActive": true
   }
   ```
5. Refresh the Insurers TabItem.

### Expected Result

- The insurer list shows the configured entry in the DataGrid:
  - Insurer: TRICARE
  - BIN: 610014
  - PCN: OHCARD
  - NCPDP Version: D.0
  - Processor: Express Scripts
  - Status: Active
- These BIN/PCN values are used when submitting claims and must match the insurer configuration.
