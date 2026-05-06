# Consolidated Mail Outpatient Pharmacy (CMOP) -- Pharmacist Human Test Script

## Prerequisites

- **Login:** PHARM3 (MARTINEZ,CARLOS R -- Ambulatory Pharmacy) / Password: `smythVista1`
- **Pre-conditions:**
  1. The SiloHost, WebServer, and BlazorWeb projects must all be running.
  2. Navigate to `/cmop` in the browser.
  3. The default Site ID is `SITE-500`. Keep this or change as needed.
  4. The Suspense Queue tab loads automatically on page load.

---

## Scenario 1: Add Prescription to CMOP Suspense Queue

### Steps

1. Navigate to `/cmop`.
2. Confirm Site ID is `SITE-500`.
3. Click the **Add to Queue** tab (tab 2).
4. Fill in the form:
   - Prescription ID: `RX-CMOP-001`
   - Patient ID: `4`
   - Patient Name: `DEMO,PATIENT FOUR`
   - Drug Name: `LISINOPRIL 10MG TAB`
   - Rx Number: `RX20260329001`
   - Quantity: `90`
   - Days Supply: `90`
   - Fill Type: **Original**
   - Priority: **Routine**
5. Click **Add to Queue**.

### Expected Result

- Success banner: "Added to suspense queue."
- The view automatically switches to the Suspense Queue tab.
- The queue table shows the new entry:
  - Rx#: RX20260329001
  - Patient: DEMO,PATIENT FOUR
  - Drug: LISINOPRIL 10MG TAB
  - Qty: 90
  - Days: 90
  - Fill: **ORIGINAL** badge (blue)
  - Priority: **ROUTINE** badge (gray)
  - Queued: current date/time

---

## Scenario 2: View Suspense Queue and Count

### Steps

1. Click the **Suspense Queue** tab (tab 0) if not already active.
2. Click **Refresh** to reload the queue.
3. Add a second prescription to the queue:
   - Go to Add to Queue tab.
   - Prescription ID: `RX-CMOP-002`
   - Patient ID: `4`
   - Patient Name: `DEMO,PATIENT FOUR`
   - Drug Name: `METFORMIN 500MG TAB`
   - Rx Number: `RX20260329002`
   - Quantity: `60`
   - Days Supply: `30`
   - Fill Type: **Refill**
   - Priority: **Urgent**
   - Click **Add to Queue**.
4. Return to Suspense Queue tab.

### Expected Result

- The queue now shows 2 entries.
- The URGENT prescription row is highlighted (row-urgent class with light orange background).
- The METFORMIN entry shows:
  - Fill: **REFILL** badge (green)
  - Priority: **URGENT** badge (yellow)
- The **Transmit to CMOP** button is visible (queue is not empty).

---

## Scenario 3: Transmit Queue to CMOP Facility

### Steps

1. On the Suspense Queue tab, confirm at least 1 prescription is in the queue.
2. Click the **Transmit to CMOP** button.

### Expected Result

- Success banner: "Queue transmitted to CMOP."
- The suspense queue is now empty (all items moved to a transmission).
- Switch to the **Transmissions** tab (tab 1). Click **Refresh**.
- A new transmission entry appears:
  - Transmission ID: (truncated GUID)
  - CMOP Facility: CMOP LEAVENWORTH
  - Status: **TRANSMITTED** badge
  - Rx Count: 2 (or however many were in the queue)
  - Dispensed: 0
  - Rejected: 0
  - Tracking: (dash -- not yet shipped)
  - Transmitted: current date/time
  - Action button: **Ack** (Acknowledge)

---

## Scenario 4: Acknowledge Transmission Receipt

### Steps

1. On the **Transmissions** tab, find the TRANSMITTED transmission.
2. Click the **Ack** button.

### Expected Result

- The transmission status changes to **RECEIVED** (blue badge).
- The Ack button disappears.
- A new action button appears: **Dispensed**.

---

## Scenario 5: Record Dispensed/Shipped/Complete

### Steps

1. On the RECEIVED transmission, click the **Dispensed** button.
2. The status changes to **DISPENSED** (green badge). Dispensed count updates to match Rx Count.
3. Click the **Ship** button (now visible).
4. The status changes to **SHIPPED** (purple badge). A tracking number appears (e.g., USPS followed by a random 9-digit number).
5. Click the **Complete** button (now visible).

### Expected Result

- Status progression: TRANSMITTED -> RECEIVED -> DISPENSED -> SHIPPED -> COMPLETED.
- After Ship: Tracking column shows a USPS tracking number (auto-generated).
- After Complete: Status shows **COMPLETED** (green badge). No more action buttons.

---

## Scenario 6: Cancel a Transmission

### Steps

1. Create a new transmission by adding items to the queue and transmitting.
2. Cancel the transmission via the API (the UI does not have a Cancel button):
   ```
   POST /api/cmop/sites/SITE-500/transmissions/{transmissionId}/cancel
   ```
3. Refresh the Transmissions tab.

### Expected Result

- The transmission status changes to **CANCELLED** (red badge).
- No action buttons appear for cancelled transmissions.
- The prescriptions that were part of the cancelled transmission may need to be re-queued.

---

## Scenario 7: Remove Item from Suspense Before Transmission

### Steps

1. Add a prescription to the suspense queue (using the Add to Queue tab):
   - Prescription ID: `RX-CMOP-REMOVE`
   - Patient ID: `4`
   - Patient Name: `DEMO,PATIENT FOUR`
   - Drug Name: `ATORVASTATIN 40MG TAB`
   - Rx Number: `RX20260329003`
   - Quantity: `90`
   - Days Supply: `90`
   - Fill Type: **Original**
   - Priority: **Routine**
2. Return to the Suspense Queue tab. The item appears.
3. Click the **Remove** button (red text) on the ATORVASTATIN row.

### Expected Result

- The ATORVASTATIN entry is removed from the suspense queue.
- The queue table refreshes and no longer shows this item.
- If this was the only item, the queue shows "No prescriptions in suspense queue."
- The Transmit to CMOP button is hidden when the queue is empty.
