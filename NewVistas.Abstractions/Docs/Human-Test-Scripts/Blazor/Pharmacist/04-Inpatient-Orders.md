# Inpatient Pharmacy Order Verification -- Pharmacist Human Test Script

## Prerequisites

- **Login:** PHARM4 (KIM,JENNY H -- Inpatient Pharmacy) / Password: `smythVista1`
- **Patient:** 22
- **Pre-conditions:**
  1. The SiloHost, WebServer, and BlazorWeb projects must all be running.
  2. Navigate to `/inpatientpharmacy` in the browser.
  3. Enter Patient ID `22` and click **Load Demo** (or use the API: `POST /api/inpatientpharmacy/demo/load?patientId=22`).
  4. After demo load, the order list should display multiple orders with types UNIT_DOSE, IV, and LVP.
  5. Ensure at least one order is in PENDING status (unverified).

---

## Scenario 1: Create UNIT_DOSE Order and Verify

### Steps

1. Create a new UNIT_DOSE order via the API:
   ```
   POST /api/inpatientpharmacy/22/orders
   {
     "drugName": "VANCOMYCIN 1G INJ",
     "drugId": "50-VANCOMYCIN",
     "orderType": "UNIT_DOSE",
     "dosage": "1G",
     "doseUnit": "GM",
     "route": "IV",
     "schedule": "Q12H",
     "priority": "ROUTINE",
     "wardId": "WARD-MED-3A",
     "wardName": "MEDICAL 3A",
     "roomBed": "301-A",
     "providerId": "PROV-001",
     "providerName": "DR. JANE SMITH",
     "durationDays": 14,
     "quantityPerDose": 1,
     "comments": "Monitor trough levels before 4th dose"
   }
   ```
2. Navigate to `/inpatientpharmacy`, enter Patient ID: `22`, and click **Load Orders**.
3. The new VANCOMYCIN order should appear with:
   - Type badge: UD (blue)
   - Status: PENDING (yellow badge)
   - Verified: "Pending" (warning icon)
4. Click on the VANCOMYCIN row to open the detail panel.
5. Confirm the detail shows: Order Type: UNIT_DOSE, Drug: VANCOMYCIN 1G INJ, Status: PENDING, Route: IV, Schedule: Q12H.
6. Click the **Verify (RPh)** button.

### Expected Result

- Action message: "Action 'verify' completed."
- The detail panel refreshes:
  - Status changes to **ACTIVE** (green badge).
  - Verified by: RPH-CURRENT on (current date/time).
- In the order table, the Verified column changes from "Pending" to the checkmark.

---

## Scenario 2: Create IV Order with Additives

### Steps

1. Create a new IV order via the API:
   ```
   POST /api/inpatientpharmacy/22/orders
   {
     "drugName": "POTASSIUM CHLORIDE 20MEQ IN NS 1000ML",
     "drugId": "50-KCL",
     "orderType": "IV",
     "dosage": "20MEQ",
     "doseUnit": "MEQ",
     "route": "IV",
     "schedule": "Q8H",
     "priority": "ROUTINE",
     "wardId": "WARD-MED-3A",
     "wardName": "MEDICAL 3A",
     "roomBed": "301-A",
     "providerId": "PROV-001",
     "providerName": "DR. JANE SMITH",
     "ivSolution": "NORMAL SALINE 0.9%",
     "ivVolumeMl": 1000,
     "infusionRateStr": "125 mL/hr"
   }
   ```
2. Load orders in the UI. Click on the new KCL order.
3. The detail panel shows Type: IV (purple badge).
4. Click **Verify (RPh)** to verify.
5. After verification (Status: ACTIVE), click the **+ Additive** button.
6. In the additive form:
   - Drug name: `MULTIVITAMIN INJ`
   - Dose: `10`
   - Unit: `ML`
   - Primary additive: unchecked
7. Click **Add**.

### Expected Result

- Action message: "Action 'additive' completed."
- The IV Additives table now shows:
  | Drug | Dose | Unit | Type |
  |------|------|------|------|
  | MULTIVITAMIN INJ | 10 | ML | Additive |
- The detail panel shows the IV Solution: NORMAL SALINE 0.9% 1000mL and Rate: 125 mL/hr.

---

## Scenario 3: Create LVP (Large Volume Parenteral) Order

### Steps

1. Create an LVP order via the API:
   ```
   POST /api/inpatientpharmacy/22/orders
   {
     "drugName": "D5W 1000ML",
     "drugId": "50-D5W",
     "orderType": "LVP",
     "dosage": "1000ML",
     "doseUnit": "ML",
     "route": "IV",
     "schedule": "CONTINUOUS",
     "priority": "ROUTINE",
     "wardId": "WARD-MED-3A",
     "wardName": "MEDICAL 3A",
     "roomBed": "301-A",
     "providerId": "PROV-001",
     "providerName": "DR. JANE SMITH",
     "ivSolution": "DEXTROSE 5% IN WATER",
     "ivVolumeMl": 1000,
     "infusionRateStr": "83 mL/hr over 12 hours"
   }
   ```
2. Load orders. Click on the D5W order.
3. Confirm: Type badge: LVP (teal), Status: PENDING.
4. Click **Verify (RPh)**.

### Expected Result

- Status changes to ACTIVE after verification.
- Detail shows IV Solution: DEXTROSE 5% IN WATER, Volume: 1000mL, Rate: 83 mL/hr over 12 hours.
- LVP badge displays in teal color.

---

## Scenario 4: View Pending Orders Queue and Verify Multiple

### Steps

1. Create 2-3 new unverified orders via the API (different drugs, all PENDING).
2. Navigate to `/inpatientpharmacy`, enter Patient ID: `22`, click **Load Orders**.
3. Sort or scan the order table for rows with Verified column showing "Pending".
4. Click on the first pending order. Click **Verify (RPh)**.
5. After success, click on the next pending order. Click **Verify (RPh)**.
6. Repeat for all pending orders.

### Expected Result

- Each order transitions from PENDING to ACTIVE after verification.
- The Verified column updates to show the checkmark for each verified order.
- The order table refreshes after each verification.

---

## Scenario 5: Discontinue an Active Order

### Steps

1. Select an ACTIVE order (e.g., a previously verified VANCOMYCIN order).
2. Click the **Discontinue** button.
3. A text input appears: "Discontinue reason".
4. Enter: `Culture negative after 72 hours. D/C per provider.`
5. Click **Confirm D/C**.

### Expected Result

- Action message: "Action 'discontinue' completed."
- Status changes to **DISCONTINUED** (red badge).
- The D/C Reason field shows: "Culture negative after 72 hours. D/C per provider."
- The action bar shows no action buttons (discontinued orders cannot be modified).

---

## Scenario 6: Place Order on Hold, Then Resume

### Steps

1. Select an ACTIVE order (e.g., POTASSIUM CHLORIDE).
2. Click the **Hold** button.
3. Action message: "Action 'hold' completed."
4. Detail panel shows:
   - Status: **HOLD** (orange badge)
   - Hold Reason: "Held by pharmacist"
5. The action bar now shows only the **Resume** button.
6. Click **Resume**.

### Expected Result

- After Hold:
  - Status: HOLD, Hold Reason displayed.
  - Only Resume button visible (no Fill, Discontinue, etc.).
- After Resume:
  - Status: ACTIVE, Hold Reason cleared.
  - Action message: "Action 'resume' completed."
  - Full action bar restored.

---

## Scenario 7: Add Scheduled Administration Times

### Steps

1. Scheduled administration times are set when the order is created or via the API. To test visibility:
   ```
   POST /api/inpatientpharmacy/22/orders/{orderId}/admin-times
   {
     "times": ["2026-03-30T08:00:00Z", "2026-03-30T20:00:00Z", "2026-03-31T08:00:00Z"]
   }
   ```
2. Reload orders in the UI. Select the order.
3. Under "Scheduled Administration Times", time chips should appear.

### Expected Result

- The detail panel shows a "Scheduled Administration Times" section.
- Time chips display in blue pills: "03/30 08:00", "03/30 20:00", "03/31 08:00".
- Up to 10 times are shown (the UI truncates with `.Take(10)`).
- The BCMA Events count shows the number of BCMA administration records linked to this order.
