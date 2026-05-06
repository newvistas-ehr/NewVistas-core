# Ward Stock Management -- Pharmacist Human Test Script

## Prerequisites

- **Login:** PHARM4 (KIM,JENNY H -- Inpatient Pharmacy) / Password: `smythVista1`
- **Pre-conditions:**
  1. The SiloHost, WebServer, and BlazorWeb projects must all be running.
  2. Navigate to `/ward-stock` in the browser.
  3. The default Ward ID is `WARD-MED-3A`. This matches one of the self-seeded demo wards.

---

## Scenario 1: View Ward Stock Items for a Ward

### Steps

1. Navigate to `/ward-stock`.
2. The Ward field shows `WARD-MED-3A` by default.
3. Click **Load** (or the page may auto-load on initialization).
4. If no items appear, click **Load Demo** to seed demo ward stock data.
5. The **Inventory** tab (tab 0) should be active.

### Expected Result

- The inventory table shows columns: Drug, On Hand, Par, Reorder Pt, UOM, Status, CS.
- Example entries:
  | Drug | On Hand | Par | Reorder Pt | UOM | Status | CS |
  |------|---------|-----|------------|-----|--------|----|
  | ACETAMINOPHEN 325MG TAB | 500 | 1000 | 200 | tablets | OK | |
  | MORPHINE SULFATE 2MG INJ | 25 | 50 | 10 | vials | OK | CS |
  | HEPARIN 5000U/ML INJ | 40 | 100 | 20 | vials | OK | |
- Drugs at or below reorder point show:
  - Status: **LOW** badge (red) -- if not yet reordered
  - Status: **REORDER PENDING** badge (yellow) -- if reorder has been triggered
  - The row has a yellow/orange background (row-low class).
- Drugs above reorder point show: **OK** badge (green).
- Controlled substances show "CS" in the CS column.

---

## Scenario 2: Adjust Stock Quantity

### Steps

1. Adjust ward stock via the API (the Blazor UI shows read-only inventory; adjustments go through the Drug Accountability or Ward Stock API):
   ```
   POST /api/wardstock/wards/WARD-MED-3A/items/{drugId}/adjust
   {
     "adjustmentQuantity": -10,
     "reason": "Dispensed to patients during shift",
     "adjustedBy": "PHARM4"
   }
   ```
2. Return to `/ward-stock` and click **Load** to refresh.

### Expected Result

- The On Hand quantity for the adjusted drug decreases by 10.
- If the new quantity falls at or below the Reorder Point, the Status changes to **LOW**.
- The replenishment log (tab 2) may show an auto-generated reorder request if the system detected low stock.

---

## Scenario 3: Auto-Replenishment Trigger (Stock Below Reorder Point)

### Steps

1. Adjust a drug's stock to below its reorder point:
   ```
   POST /api/wardstock/wards/WARD-MED-3A/items/{drugId}/adjust
   {
     "adjustmentQuantity": -450,
     "reason": "Testing auto-replenishment trigger",
     "adjustedBy": "PHARM4"
   }
   ```
   (This should bring the quantity below the reorder point.)
2. Refresh the ward stock page.

### Expected Result

- The adjusted drug's row shows:
  - On Hand: reduced quantity (below Reorder Pt)
  - Status: **LOW** or **REORDER PENDING**
  - Row highlighted in yellow/orange (row-low class)
  - The quantity cell shows red text (qty-low class)
- Switch to the **Low Stock Alerts** tab (tab 1). Click **Load** if needed.
  - The drug appears in the low stock list with columns: Drug, On Hand, Reorder Pt, Par, Status.
  - Status shows "NEEDS REORDER" or "REORDER PENDING".
- The system may have automatically generated a replenishment request.

---

## Scenario 4: View Replenishment Log

### Steps

1. Click the **Replenishment Log** tab (tab 2).
2. The page loads replenishment request history.

### Expected Result

- The replenishment log table shows columns: Drug, Qty Requested, Status, Requested, Filled.
- Entries include:
  - Drug name
  - Qty Requested: the amount needed to bring stock back to par level
  - Status badge:
    - **PENDING** (yellow) -- request created, not yet filled
    - **FILLED** (green) -- pharmacy has filled the request
    - **CANCELLED** (red) -- request was cancelled
  - Requested: date/time the request was generated
  - Filled: date/time it was filled (or dash if pending)
- If no replenishment requests exist, the message "No replenishment requests." appears.

---

## Scenario 5: Add New Item to Ward Stock

### Steps

1. Add a new ward stock item via the API:
   ```
   POST /api/wardstock/wards/WARD-MED-3A/items
   {
     "drugId": "50-ONDANSETRON",
     "drugName": "ONDANSETRON 4MG TAB ODT",
     "quantityOnHand": 100,
     "parLevel": 200,
     "reorderPoint": 50,
     "unitOfMeasure": "tablets",
     "isControlledSubstance": false
   }
   ```
2. Return to `/ward-stock` and click **Load**.

### Expected Result

- The inventory table now includes the new entry:
  - Drug: ONDANSETRON 4MG TAB ODT
  - On Hand: 100
  - Par: 200
  - Reorder Pt: 50
  - UOM: tablets
  - Status: OK (100 > 50)
  - CS: (blank -- not controlled)
- The new item is available for all ward stock operations.

### Testing Different Wards

3. Change the Ward field to `WARD-ICU-1` (one of the self-seeded wards).
4. Click **Load**.

### Expected Result

- The inventory loads for the ICU ward, showing different stock levels appropriate for an ICU setting.
- Available demo wards: WARD-MED-3A, WARD-MED-4B, WARD-SURG-2C, WARD-ICU-1, WARD-PSYCH-5A, WARD-OBS-1.
