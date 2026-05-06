# Order Entry -- Physician Human Test Script

## Prerequisites
- Login: DOCTOR1 / Password: smythVista1
- Patient: 9
- Pre-conditions: Demo data loaded. Ensure SiloHost, WebServer, and BlazorWeb are running.

---

## Scenario 1: Place a Lab Order (Happy Path)

### Steps
1. Log in as **DOCTOR1** (SMITH,JOHN A / Internal Medicine)
2. Navigate to `/orders`
3. Enter Patient ID: `9`
4. Click **Load** (or press Enter)
5. The Active Orders tab (tab 0) loads. Note any existing orders.
6. Click the **New Order** tab (tab 1)
7. Fill in the New Order form:
   - Order Type: **Lab** (dropdown)
   - Order Text: `CBC WITH DIFFERENTIAL`
   - Urgency: **Routine** (dropdown; options: Routine, Urgent, STAT)
   - Instructions: `Fasting not required`
   - Clinic / Location: Click **Load Clinics**, then select the first active clinic
   - Provider: `SMITH,JOHN A`
8. Click **Place Order**

### Expected Result
- Green success banner: "Order placed: CBC WITH DIFFERENTIAL"
- The Order Text field clears
- View switches to Active Orders tab automatically
- The new order appears in the table with:
  - Order: "CBC WITH DIFFERENTIAL"
  - Type: "Lab"
  - Status: badge showing "Pending" (yellow)
  - Provider: "SMITH,JOHN A"
- Action buttons visible: **Sign**, (no Hold/DC since it is Pending)

---

## Scenario 2: Sign an Order

### Steps
1. Continuing from Scenario 1, locate the "CBC WITH DIFFERENTIAL" order in the Active Orders table
2. Click the **Sign** button on that row

### Expected Result
- Green success banner: "Order sign successful."
- The order status changes from "Pending" to "Active" (green badge)
- New action buttons appear for the order: **Hold**, **DC**

---

## Scenario 3: Place a STAT Order

### Steps
1. Click the **New Order** tab
2. Fill in:
   - Order Type: **Lab**
   - Order Text: `TROPONIN I`
   - Urgency: **STAT**
   - Instructions: `Stat draw for chest pain evaluation`
   - Provider: `SMITH,JOHN A`
3. Click **Place Order**

### Expected Result
- Green success: "Order placed: TROPONIN I"
- Order appears in Active Orders with urgency data visible
- Status: "Pending"

---

## Scenario 4: Place a Medication Order

### Steps
1. Click the **New Order** tab
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
- Green success: "Order placed: LISINOPRIL 10MG TAB DAILY"
- Order appears in Active Orders list with Type: "Pharmacy"

---

## Scenario 5: Discontinue an Active Order

### Steps
1. In the Active Orders tab, locate an order with status "Active"
2. Click the **DC** button (red text) on that order row

### Expected Result
- Green success banner: "Order discontinue successful."
- The order status changes to "Discontinued" (red badge)
- The Hold and DC buttons disappear for that order
- The order remains visible in the list

---

## Scenario 6: Hold and Release an Order

### Steps
1. In the Active Orders tab, locate an Active order (sign one if needed)
2. Click the **Hold** button (orange text)

### Expected Result
- Green success: "Order hold successful."
- The order status changes to "Hold" (orange badge)
- The row background turns light orange
- A **Release** button appears (green text); Sign/Hold/DC buttons disappear

### Steps (continued)
3. Click the **Release** button on the held order

### Expected Result
- Green success: "Order release successful."
- The order status returns to "Active" (green badge)
- Hold/DC buttons reappear; Release button disappears

---

## Scenario 7: Execute an Order Set

### Steps
1. Click the **Order Sets** tab (tab 2)
2. Click the **Load Order Sets** button
3. Wait for the order set cards to load in a grid
4. Each card shows:
   - Name (bold, navy)
   - Category, Service Section, and item count
5. Click on one of the order set cards (e.g., an admission order set if available)

### Expected Result
- The card highlights with a blue border and light blue background
- Below the grid, a **detail section** appears showing:
  - Order Set Name (h3)
  - Description
  - Table with columns: Seq, Order, Type, Urgency
  - Items listed in sequence order

### Steps (continued)
6. Fill in the **Provider** field: `SMITH,JOHN A`
7. Click **Execute Order Set**

### Expected Result
- Green success: "Order set executed: [N] orders created."
- View switches to Active Orders tab
- Multiple new orders appear in the list, one per item in the order set
- All new orders have status "Pending" and provider "SMITH,JOHN A"

---

## Scenario 8: Order Check -- Duplicate Order Warning

### Steps
1. Click the **New Order** tab
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
- Green success message appears

---

## Scenario 9: Filter Active Orders

### Steps
1. On the Active Orders tab, locate the **Filter** dropdown
2. Default selection is: "Current (Active/Pending/Hold)" (value 2)
3. Change the filter to **Discontinued** (value 3)

### Expected Result
- The orders list reloads showing only discontinued orders
- Each order has status badge "Discontinued" (red)

### Steps (continued)
4. Change filter to **All Orders** (value 1)

### Expected Result
- All orders appear regardless of status
- Mixed status badges visible (Active, Pending, Hold, Discontinued, Completed)

### Steps (continued)
5. Change filter to **Unsigned** (value 11)

### Expected Result
- Only orders that have not been signed appear

---

## Scenario 10: View Order History

### Steps
1. Click the **Order History** tab (tab 3)
2. Set the date range:
   - From: 90 days ago (pre-filled default)
   - To: Today (pre-filled default)
   - Max Results: `100`
3. Click **Search**

### Expected Result
- A results count appears: "[N] result(s)"
- A table shows historical orders with columns: Order, Type, Status, Start, Provider
- Orders are listed within the date range
- If no orders exist, message: "No orders found for the selected date range."

---

## Scenario 11: Place Order with Empty Required Fields

### Steps
1. Click the **New Order** tab
2. Leave Order Text empty
3. Leave Provider empty
4. Observe button states

### Expected Result
- The **Check Order** button is disabled (grayed out) when Order Text is empty
- The **Place Order** button is disabled when either Order Text or Provider is empty
- No error messages; buttons simply cannot be clicked

---

## Appendix: Clinical Event Sourcing Verification

**Added 2026-04-27** -- Orders now emit clinical events to the per-patient event
stream and (when configured) flow to the federation outbox. Use this appendix
to verify the event-sourcing side effects.

### Steps

1. Before placing an order, capture the patient's current event-stream version:
   ```powershell
   $login = Invoke-RestMethod -Method Post -Uri https://localhost:7127/api/auth/login `
     -Body (@{ username = "DOCTOR1"; password = "smythVista1" } | ConvertTo-Json) `
     -ContentType "application/json"
   $before = Invoke-RestMethod -Method Get `
     -Uri "https://localhost:7127/api/patient/{patientId}/clinical-events?max=1" `
     -Headers @{ Authorization = "Bearer $($login.token)" }
   $beforeVersion = if ($before) { $before[0].version } else { 0 }
   ```
2. Place the order via the UI (Scenario 5 above).
3. Re-query the event stream and verify a new event appended:
   ```powershell
   $after = Invoke-RestMethod -Method Get `
     -Uri "https://localhost:7127/api/patient/{patientId}/clinical-events?max=5" `
     -Headers @{ Authorization = "Bearer $($login.token)" }
   $after | Where-Object { $_.version -gt $beforeVersion } | Format-Table version, domain, eventId
   ```

### Expected Result

- One new event with `domain = Order` (or `Lab`/`Medication` depending on order subtype) and `version = beforeVersion + 1`.
- Event `sourceClusterId` matches the local silo's cluster identity (e.g., `HUB-PRIMARY` or `SPOKE-TEST-1`).
- If federation outbox is enabled, the Federation Dashboard ([Admin/01](../Admin/01-Federation-Dashboard-Smoke.md)) shows Pending count incrementing.

### Verification Checklist (Event Sourcing)

- [ ] New event appears in `/clinical-events` after order placement
- [ ] Event `version` is exactly `previousVersion + 1`
- [ ] Event `sourceClusterId` matches local cluster
- [ ] Federation outbox row inserted (if outbox enabled)

See [Blazor/Admin/08-Clinical-Event-Sourcing.md](../Admin/08-Clinical-Event-Sourcing.md) for the full event-sourcing test script.
