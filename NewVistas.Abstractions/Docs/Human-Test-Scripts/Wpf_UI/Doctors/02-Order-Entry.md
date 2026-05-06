# Order Entry -- Physician Human Test Script -- WPF UI

## Prerequisites
- Login: DOCTOR1 / Password: smythVista1
- Patient: 9
- Pre-conditions: Demo data loaded. Ensure SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) are running.

---

## Scenario 1: Place a Lab Order (Happy Path)

### Steps
1. Log in as **DOCTOR1** (SMITH,JOHN A / Internal Medicine)
2. In the Navigation Panel, select **Orders**
3. Enter Patient ID in the toolbar: `9`
4. Click **Load** (or press Enter)
5. The Active Orders TabItem (tab 0) loads. Note any existing orders.
6. Click the **New Order** TabItem (tab 1)
7. Fill in the New Order form:
   - Order Type: **Lab** (ComboBox)
   - Order Text: `CBC WITH DIFFERENTIAL`
   - Urgency: **Routine** (ComboBox; options: Routine, Urgent, STAT)
   - Instructions: `Fasting not required`
   - Clinic / Location: Click **Load Clinics**, then select the first active clinic from the ComboBox
   - Provider: `SMITH,JOHN A`
8. Click **Place Order**

### Expected Result
- A green notification appears in the status bar: "Order placed: CBC WITH DIFFERENTIAL"
- The Order Text field clears
- View switches to Active Orders TabItem automatically
- The new order appears in the DataGrid with:
  - Order: "CBC WITH DIFFERENTIAL"
  - Type: "Lab"
  - Status: status indicator showing "Pending" (yellow)
  - Provider: "SMITH,JOHN A"
- Action buttons visible: **Sign**, (no Hold/DC since it is Pending)

---

## Scenario 2: Sign an Order

### Steps
1. Continuing from Scenario 1, locate the "CBC WITH DIFFERENTIAL" order in the Active Orders DataGrid
2. Click the **Sign** button on that row (or right-click and select **Sign**)

### Expected Result
- A green notification appears in the status bar: "Order sign successful."
- The order status changes from "Pending" to "Active" (green status indicator)
- New action buttons appear for the order: **Hold**, **DC**

---

## Scenario 3: Place a STAT Order

### Steps
1. Click the **New Order** TabItem
2. Fill in:
   - Order Type: **Lab**
   - Order Text: `TROPONIN I`
   - Urgency: **STAT**
   - Instructions: `Stat draw for chest pain evaluation`
   - Provider: `SMITH,JOHN A`
3. Click **Place Order**

### Expected Result
- A success notification appears: "Order placed: TROPONIN I"
- Order appears in Active Orders with urgency data visible
- Status: "Pending"

---

## Scenario 4: Place a Medication Order

### Steps
1. Click the **New Order** TabItem
2. Fill in:
   - Order Type: **Pharmacy**
   - Order Text: `LISINOPRIL 10MG TAB DAILY`
   - Urgency: **Routine**
   - Instructions: `For hypertension. Take in the morning.`
   - Provider: `SMITH,JOHN A`
3. Click **Check Order** first

### Expected Result
- If order checks find issues (e.g., duplicate drug, drug-allergy), a yellow **Order Check Warnings** box appears below the form with:
  - Check type (e.g., "DUPLICATE_ORDER", "DRUG_ALLERGY")
  - Severity (High/Moderate/Low with color coding: red/orange/gray)
  - Message describing the conflict
- If no issues found, the warnings section does not appear

### Steps (continued)
4. Click **Place Order** (proceed even if warnings exist -- this acts as an override)

### Expected Result
- A success notification appears: "Order placed: LISINOPRIL 10MG TAB DAILY"
- Order appears in Active Orders DataGrid with Type: "Pharmacy"

---

## Scenario 5: Discontinue an Active Order

### Steps
1. In the Active Orders TabItem, locate an order with status "Active"
2. Click the **DC** button (red text) on that order row (or right-click and select **Discontinue**)

### Expected Result
- A green notification appears in the status bar: "Order discontinue successful."
- The order status changes to "Discontinued" (red status indicator)
- The Hold and DC buttons disappear for that order
- The order remains visible in the DataGrid

---

## Scenario 6: Hold and Release an Order

### Steps
1. In the Active Orders TabItem, locate an Active order (sign one if needed)
2. Click the **Hold** button (orange text) (or right-click and select **Hold**)

### Expected Result
- A success notification appears: "Order hold successful."
- The order status changes to "Hold" (orange status indicator)
- The row background turns light orange
- A **Release** button appears (green text); Sign/Hold/DC buttons disappear

### Steps (continued)
3. Click the **Release** button on the held order (or right-click and select **Release**)

### Expected Result
- A success notification appears: "Order release successful."
- The order status returns to "Active" (green status indicator)
- Hold/DC buttons reappear; Release button disappears

---

## Scenario 7: Execute an Order Set

### Steps
1. Click the **Order Sets** TabItem (tab 2)
2. Click the **Load Order Sets** button
3. A busy indicator appears while order sets load
4. Each card shows:
   - Name (bold, navy)
   - Category, Service Section, and item count
5. Click on one of the order set cards (e.g., an admission order set if available)

### Expected Result
- The card highlights with a blue border and light blue background
- Below the grid, a **detail section** appears showing:
  - Order Set Name (heading)
  - Description
  - DataGrid with columns: Seq, Order, Type, Urgency
  - Items listed in sequence order

### Steps (continued)
6. Fill in the **Provider** field: `SMITH,JOHN A`
7. Click **Execute Order Set**

### Expected Result
- A success notification appears: "Order set executed: [N] orders created."
- View switches to Active Orders TabItem
- Multiple new orders appear in the DataGrid, one per item in the order set
- All new orders have status "Pending" and provider "SMITH,JOHN A"

---

## Scenario 8: Order Check -- Duplicate Order Warning

### Steps
1. Click the **New Order** TabItem
2. Enter Order Type: **Lab**, Order Text: `CBC WITH DIFFERENTIAL` (same as Scenario 1)
3. Click **Check Order**

### Expected Result
- The **Order Check Warnings** section appears
- A warning shows:
  - Check Type: "DUPLICATE_ORDER" (or similar)
  - Severity: "Moderate" or "High" (orange or red text)
  - Message describing the duplicate

### Steps (continued)
4. Click **Place Order** anyway to override

### Expected Result
- The order is placed despite the warning (override behavior)
- A success notification appears

---

## Scenario 9: Filter Active Orders

### Steps
1. On the Active Orders TabItem, locate the **Filter** ComboBox
2. Default selection is: "Current (Active/Pending/Hold)" (value 2)
3. Change the filter to **Discontinued** (value 3)

### Expected Result
- The orders DataGrid reloads showing only discontinued orders
- Each order has status indicator "Discontinued" (red)

### Steps (continued)
4. Change filter to **All Orders** (value 1)

### Expected Result
- All orders appear regardless of status
- Mixed status indicators visible (Active, Pending, Hold, Discontinued, Completed)

### Steps (continued)
5. Change filter to **Unsigned** (value 11)

### Expected Result
- Only orders that have not been signed appear

---

## Scenario 10: View Order History

### Steps
1. Click the **Order History** TabItem (tab 3)
2. Set the date range:
   - From: 90 days ago (pre-filled default) using the DatePicker
   - To: Today (pre-filled default) using the DatePicker
   - Max Results: `100`
3. Click **Search**

### Expected Result
- A results count appears: "[N] result(s)"
- A DataGrid shows historical orders with columns: Order, Type, Status, Start, Provider
- Orders are listed within the date range
- If no orders exist, message: "No orders found for the selected date range."

---

## Scenario 11: Place Order with Empty Required Fields

### Steps
1. Click the **New Order** TabItem
2. Leave Order Text empty
3. Leave Provider empty
4. Observe button states

### Expected Result
- The **Check Order** button is disabled (grayed out) when Order Text is empty
- The **Place Order** button is disabled when either Order Text or Provider is empty
- No error messages; buttons simply cannot be clicked
