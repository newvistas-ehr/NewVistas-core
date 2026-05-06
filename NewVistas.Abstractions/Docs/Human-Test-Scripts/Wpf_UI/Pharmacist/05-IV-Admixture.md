# IV Admixture Compounding Workflow -- Pharmacist Human Test Script -- WPF UI

## Prerequisites

- **Login:** PHARM4 (KIM,JENNY H -- Inpatient Pharmacy) / Password: `smythVista1`
- **Patient:** 22
- **Pre-conditions:**
  1. The SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) must all be running.
  2. In the Navigation Panel, select **IV Pharmacy**.
  3. Enter Patient ID `22` in the Patient ID field in the toolbar.

---

## Scenario 1: Full Lifecycle -- Create IV Order, Add Additives, Verify, Compound, Label, Dispense

### Steps

1. In the Navigation Panel, select **IV Pharmacy**.
2. Enter Patient ID: `22` in the Patient ID field in the toolbar and click **Load Orders**.
3. Click the **New Order** TabItem (tab 3).
4. Fill in the new order form:
   - Base Solution: `Normal Saline 0.9%`
   - Base Volume (mL): `250`
   - Route: `Peripheral`
   - Frequency: `Q8H`
   - Container: `Bag`
   - Container Count: `1`
   - Priority: `Routine`
   - Infusion Rate String: `125 mL/hr`
   - Infusion Rate (mL/hr): `125`
   - Infusion Duration (hrs): `2`
   - Start Date/Time: (select current date/time using the DatePicker)
   - Stop Date/Time: (select 3 days from now using the DatePicker)
   - Provider ID: `PROV-001`
   - Provider Name: `DR. JANE SMITH`
   - Notes: `For hydration and electrolyte replacement`
5. Click **Create Order**.
6. A success toast notification appears: "IV admixture order created successfully."
7. Switch to the **Pending / Verified** TabItem (tab 1). The new order should appear with Status: Pending.
8. Click the **Verify** button on the new order row (or right-click and select **Verify**).
9. The order status changes to Verified.
10. Click the **Start Compound** button (now visible for Verified orders).
11. The order status changes to Compounding.
12. Switch to the **Compounding Queue** TabItem (tab 2). The order appears here.
13. Click **Complete** on the order in the compounding queue.
14. The order status changes to Ready.
15. Click **Dispense** on the Ready order.

### Expected Result

- Order progresses through the full lifecycle:
  - Pending -> Verified -> Compounding -> Ready -> Dispensed
- At each step, the appropriate TabItem shows the order in the correct state.
- The All Orders TabItem (tab 0) reflects the current status at all times.

---

## Scenario 2: Cancel a Pending IV Order

### Steps

1. Create a new IV order using the New Order TabItem (same as Scenario 1 steps 3-5, but with different notes).
   - Base Solution: `Lactated Ringer's`
   - Base Volume (mL): `1000`
   - Route: `Peripheral`
   - Frequency: `Once`
   - Container: `Bag`
   - Priority: `Routine`
   - Notes: `Order to be cancelled for testing`
2. Click **Create Order**.
3. The order appears on the Pending / Verified TabItem with Status: Pending.
4. Cancel the order via the API (the view does not have a direct Cancel button on pending orders):
   ```
   POST /api/IVPharmacy/22/orders/{orderId}/cancel
   ```
5. Click **Load Orders** to refresh.

### Expected Result

- The order status changes to **Cancelled**.
- The order row appears dimmed on the All Orders TabItem.
- The order no longer appears on the Pending / Verified TabItem.

---

## Scenario 3: Multiple Additives on One Order

### Steps

1. Create an IV order via the New Order TabItem:
   - Base Solution: `D5W (Dextrose 5% in Water)`
   - Base Volume (mL): `500`
   - Route: `Central`
   - Frequency: `Q12H`
   - Container: `Bag`
   - Priority: `Routine`
   - Notes: `Multiple additive test`
2. Click **Create Order**.
3. Add additives via the API:
   ```
   POST /api/IVPharmacy/22/orders/{orderId}/additives
   { "drugName": "POTASSIUM CHLORIDE", "drugId": "50-KCL", "dose": "20", "doseUnit": "MEQ", "isBase": false }

   POST /api/IVPharmacy/22/orders/{orderId}/additives
   { "drugName": "MAGNESIUM SULFATE", "drugId": "50-MGSO4", "dose": "2", "doseUnit": "GM", "isBase": false }

   POST /api/IVPharmacy/22/orders/{orderId}/additives
   { "drugName": "MULTIVITAMIN INJECTION", "drugId": "50-MVI", "dose": "10", "doseUnit": "ML", "isBase": false }
   ```
4. View the order on the All Orders TabItem. The Additives column should show 3.

### Expected Result

- The order details show 3 additives when viewed.
- The All Orders DataGrid shows Additives column = 3.
- Each additive has the correct drug name, dose, dose unit, and type (Additive vs. Primary).

---

## Scenario 4: TPN (Total Parenteral Nutrition) Complex Admixture

### Steps

1. Create a TPN order via the New Order TabItem:
   - Base Solution: `TPN Base Solution`
   - Base Volume (mL): `2000`
   - Route: `Central`
   - Frequency: `Continuous`
   - Container: `Bag`
   - Container Count: `1`
   - Priority: `Routine`
   - Infusion Rate String: `83 mL/hr over 24 hours`
   - Infusion Rate (mL/hr): `83`
   - Infusion Duration (hrs): `24`
   - Provider ID: `PROV-002`
   - Provider Name: `DR. MARK JONES`
   - Notes: `TPN: Dextrose 250g, Amino Acids 85g, Lipids 50g. Cycle over 24 hours.`
2. Click **Create Order**.
3. Add multiple additives via the API:
   ```
   POST /api/IVPharmacy/22/orders/{orderId}/additives
   { "drugName": "DEXTROSE 70%", "dose": "357", "doseUnit": "ML", "isBase": true }

   POST /api/IVPharmacy/22/orders/{orderId}/additives
   { "drugName": "AMINO ACIDS 10%", "dose": "850", "doseUnit": "ML", "isBase": true }

   POST /api/IVPharmacy/22/orders/{orderId}/additives
   { "drugName": "LIPID EMULSION 20%", "dose": "250", "doseUnit": "ML", "isBase": false }

   POST /api/IVPharmacy/22/orders/{orderId}/additives
   { "drugName": "SODIUM CHLORIDE", "dose": "40", "doseUnit": "MEQ", "isBase": false }

   POST /api/IVPharmacy/22/orders/{orderId}/additives
   { "drugName": "POTASSIUM PHOSPHATE", "dose": "15", "doseUnit": "MMOL", "isBase": false }

   POST /api/IVPharmacy/22/orders/{orderId}/additives
   { "drugName": "TRACE ELEMENTS", "dose": "1", "doseUnit": "ML", "isBase": false }
   ```
4. Verify the order, start compounding, complete compounding, and dispense.

### Expected Result

- The order displays as a complex TPN with 6+ additives.
- Additives column shows 6 on the All Orders DataGrid.
- The order progresses through the full lifecycle: Pending -> Verified -> Compounding -> Ready -> Dispensed.
- TPN-specific fields (large volume, continuous rate, 24-hour duration) display correctly.

---

## Scenario 5: STAT Priority IV with Shortened Workflow

### Steps

1. Create a STAT IV order via the New Order TabItem:
   - Base Solution: `Normal Saline 0.9%`
   - Base Volume (mL): `100`
   - Route: `Peripheral`
   - Frequency: `Once`
   - Container: `Syringe`
   - Container Count: `1`
   - Priority: **STAT**
   - Infusion Rate String: `Over 30 min`
   - Infusion Rate (mL/hr): `200`
   - Infusion Duration (hrs): `0.5`
   - Start Date/Time: (current date/time)
   - Provider ID: `PROV-001`
   - Provider Name: `DR. JANE SMITH`
   - Notes: `STAT antibiotic push - sepsis protocol`
2. Click **Create Order**.
3. The order appears on the Pending / Verified TabItem highlighted in red (STAT priority).
4. Immediately verify the order by clicking **Verify**.
5. Start compounding by clicking **Start Compound**.
6. Complete compounding by clicking **Complete**.
7. Dispense by clicking **Dispense**.

### Expected Result

- The order priority column shows **STAT** highlighted in bold red throughout the workflow.
- The STAT order appears at the top of the Pending / Verified list (priority sort).
- Container type: Syringe (for IV push).
- The order completes the full lifecycle.
- Route enum values confirmed: Peripheral, Central, PICC, Midline, Epidural, Subcutaneous, Other.
- Frequency enum values confirmed: Once, Continuous, Q1H, Q2H, Q4H, Q6H, Q8H, Q12H, Q24H, PRN, Other.
- Container enum values confirmed: Bag, Syringe, Bottle, Cassette, Other.
- Priority enum values confirmed: Routine, ASAP, STAT, OnCall.
