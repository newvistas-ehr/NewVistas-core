# Drug Accountability -- Inventory Management -- Pharmacist Human Test Script -- WPF UI

## Prerequisites

- **Login:** PHARM3 (MARTINEZ,CARLOS R -- Ambulatory Pharmacy) / Password: `smythVista1`
- **Pre-conditions:**
  1. The SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) must all be running.
  2. In the Navigation Panel, select **Drug Accountability**.
  3. Load demo data by using the API: `POST /api/drugaccountability/demo/load`
  4. Enter Location ID: `VAULT-001` and click **Load Location**.
  5. The drug inventory DataGrid should display multiple drugs with balances, reorder points, and stock status.

---

## Scenario 1: Receive Stock (Incoming Shipment)

### Steps

1. In the Navigation Panel, select **Drug Accountability**.
2. Enter Location ID: `VAULT-001` and click **Load Location**.
3. Locate a drug in the inventory DataGrid (e.g., the first row).
4. Click the **Receive** button on that drug's row (or right-click and select **Receive**).
5. The Receive Stock panel opens with fields:
   - Quantity: enter `500`
   - Lot#: enter `LOT-2026-0329A`
   - User: enter `PHARM3`
   - Notes: enter `Shipment from McKesson PO#12345`
6. Click **Submit**.

### Expected Result

- The receive panel closes.
- The drug's Balance column increases by 500.
- Click on the drug row to view Transaction History.
- The newest transaction shows:
  - Type: **RECEIPT** (green status indicator)
  - Qty: +500
  - Before: (previous balance)
  - After: (previous balance + 500)
  - User: PHARM3
  - Notes: Shipment from McKesson PO#12345

---

## Scenario 2: Dispense to Patient

### Steps

1. In the inventory DataGrid, locate a drug with sufficient balance.
2. Click the **Dispense** button on that drug's row (or right-click and select **Dispense**).
3. The Dispense to Patient panel opens:
   - Quantity: enter `30`
   - Patient ID: enter `4`
   - Prescription ID: enter `RX-DEMO-001`
   - User: enter `PHARM3`
4. Click **Submit**.

### Expected Result

- The drug's Balance column decreases by 30.
- Transaction History shows:
  - Type: **DISPENSE** (red status indicator)
  - Qty: -30
  - Patient: 4
  - User: PHARM3

---

## Scenario 3: Record Waste with Witness

### Steps

1. Locate a drug in the inventory DataGrid.
2. Click the **Waste** button on that drug's row (or right-click and select **Waste**).
3. The Record Waste panel opens:
   - Quantity: enter `5`
   - Reason: enter `Expired partial vial - beyond use dating`
   - Witness ID: enter `PHARM4`
   - Witness Name: enter `KIM,JENNY H`
   - User: enter `PHARM3`
4. Click **Submit**.

### Expected Result

- Balance decreases by 5.
- Transaction History shows:
  - Type: **WASTE** (orange status indicator)
  - Qty: -5
  - User: PHARM3
  - Notes column: "Witness: KIM,JENNY H"

---

## Scenario 4: Transfer Between Locations

### Steps

1. Record a transfer via the API (the view does not have a direct transfer panel):
   ```
   POST /api/drugaccountability/locations/VAULT-001/drugs/{drugId}/transfer
   {
     "quantity": 100,
     "destinationLocationId": "VAULT-002",
     "userId": "PHARM3",
     "userName": "MARTINEZ,CARLOS R",
     "notes": "Transfer to satellite pharmacy for weekend coverage"
   }
   ```
2. Return to the view and click **Load Location** to refresh.

### Expected Result

- At VAULT-001: Balance decreases by 100.
- Transaction History shows:
  - Type: **TRANSFER**
  - Qty: -100
  - Notes: references destination VAULT-002
- At VAULT-002 (load that location): Balance increases by 100 with a matching TRANSFER transaction.

---

## Scenario 5: Perform Inventory Count (Creates Adjustment if Discrepancy)

### Steps

1. Locate a drug in the inventory DataGrid. Note its current balance (e.g., 470).
2. Click the **Count** button on that drug's row (or right-click and select **Count**).
3. The Physical Inventory Count panel opens:
   - System balance displayed: 470
   - Counted Qty: enter `465` (5 fewer than system)
   - User: enter `PHARM3`
   - Notes: enter `Monthly physical inventory count`
4. Click **Submit**.

### Expected Result

- The drug's Balance column updates to 465.
- Transaction History shows:
  - Type: **INVENTORY_COUNT**
  - Qty: -5 (delta adjustment)
  - Before: 470
  - After: 465
  - User: PHARM3
  - Notes: Monthly physical inventory count
- If the counted quantity matched the system (e.g., 470 = 470), the delta would be 0 and no adjustment transaction would be created.

---

## Scenario 6: Record Return from Patient

### Steps

1. Record a return via the API:
   ```
   POST /api/drugaccountability/locations/VAULT-001/drugs/{drugId}/return
   {
     "quantity": 15,
     "patientId": "4",
     "userId": "PHARM3",
     "userName": "MARTINEZ,CARLOS R",
     "notes": "Patient returned unused medication after regimen change"
   }
   ```
2. Refresh the location in the view.

### Expected Result

- Balance increases by 15.
- Transaction History shows:
  - Type: **RETURN**
  - Qty: +15
  - Patient: 4
  - User: PHARM3

---

## Scenario 7: View Low-Stock Drugs

### Steps

1. On the inventory DataGrid, check the **Low stock only** CheckBox.
2. The DataGrid filters to show only drugs where balance is at or below the Reorder Point.

### Expected Result

- Only drugs with a **LOW STOCK** status indicator (red) in the Status column appear.
- These drugs have CurrentBalance <= ReorderPoint.
- The balance column may show a warning color.
- Unchecking the filter restores the full list.

---

## Scenario 8: View Controlled Substances at Location

### Steps

1. On the inventory DataGrid, check the **Controlled only** CheckBox.
2. The DataGrid filters to show only drugs marked as controlled substances.

### Expected Result

- Only drugs with a **DEA** status indicator in the Type column appear.
- These drugs have IsControlled = true.
- Both filters (Low stock + Controlled) can be combined to find controlled substances that are running low.
- Unchecking restores the full list.
