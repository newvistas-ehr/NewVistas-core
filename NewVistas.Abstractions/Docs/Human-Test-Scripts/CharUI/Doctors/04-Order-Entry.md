# Order Entry -- Physician CharUI Human Test Script

## Prerequisites

- **Login:** DOCTOR1 / Password: `smythVista1`
- **Security Keys Required:** ORES (Order Entry/Signing), TIU SIGN
- **Patient:** Select a patient with demo data loaded.
- **Pre-conditions:** SiloHost and WebServer running. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: List Active Orders (Happy Path)

### Steps

1. At the Main Menu, type: `OR` and press Enter.
2. At the Orders menu, type: `1` (List Active Orders).

### Expected Result

- A table displays with columns: #, Order, Type, Status, Date, Provider.
- Only orders with active statuses (PENDING, ACTIVE) appear.
- If demo data loaded, at least 1 order should be listed.

---

## Scenario 2: List All Orders

### Steps

1. At the Orders menu, type: `2` (List All Orders).

### Expected Result

- A table displays ALL orders regardless of status (PENDING, ACTIVE, COMPLETED, DISCONTINUED, HELD).
- More rows than the active-only view.

---

## Scenario 3: Place a New Order -- Routine Lab (Happy Path)

### Steps

1. At the Orders menu, type: `3` (Place New Order).
2. Enter the following field-by-field:

| Prompt | Value to Enter |
|--------|----------------|
| Order Type (Lab, Rad, Med, Diet, Consult, Other) | `Lab` |
| Order Text | `CBC with Differential` |
| Urgency (Routine, STAT, ASAP) | `Routine` |
| Instructions (optional) | `Fasting specimen preferred` |
| Indication (optional) | `Annual screening` |

3. At the confirmation prompt `Place this order?`, type: `Y`.

### Expected Result

- The terminal displays: `Order placed: [order-ID]`
- Return to the Orders menu.
- Verify by listing active orders (option 1) -- "CBC with Differential" appears with Status: PENDING.

---

## Scenario 4: Place a STAT Order

### Steps

1. At the Orders menu, type: `3` (Place New Order).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Order Type | `Lab` |
| Order Text | `Troponin I` |
| Urgency | `STAT` |
| Instructions (optional) | `Chest pain evaluation` |
| Indication (optional) | `R/O acute MI` |

3. Confirm: `Y`

### Expected Result

- Order placed with Urgency = STAT.

---

## Scenario 5: Place a Medication Order

### Steps

1. At the Orders menu, type: `3` (Place New Order).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Order Type | `Med` |
| Order Text | `Lisinopril 10mg PO Daily` |
| Urgency | `Routine` |
| Instructions (optional) | `For hypertension management` |
| Indication (optional) | `Essential Hypertension I10` |

3. Confirm: `Y`

### Expected Result

- Order placed with Type = Med.

---

## Scenario 6: Place a Radiology Order

### Steps

1. At the Orders menu, type: `3` (Place New Order).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Order Type | `Rad` |
| Order Text | `CXR PA and Lateral` |
| Urgency | `ASAP` |
| Instructions (optional) | `Evaluate for pneumonia` |
| Indication (optional) | `Productive cough, fever` |

3. Confirm: `Y`

### Expected Result

- Order placed with Type = Rad and Urgency = ASAP.

---

## Scenario 7: Place a Consult Order

### Steps

1. At the Orders menu, type: `3` (Place New Order).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Order Type | `Consult` |
| Order Text | `Cardiology Consult` |
| Urgency | `Routine` |
| Instructions (optional) | `Evaluate new murmur` |
| Indication (optional) | `Heart murmur on physical exam` |

3. Confirm: `Y`

### Expected Result

- Order placed with Type = Consult.

---

## Scenario 8: Place a Diet Order

### Steps

1. At the Orders menu, type: `3` (Place New Order).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Order Type | `Diet` |
| Order Text | `Low Sodium Diet 2g` |
| Urgency | `Routine` |
| Instructions (optional) | `Heart failure dietary management` |
| Indication (optional) | `CHF` |

3. Confirm: `Y`

### Expected Result

- Order placed with Type = Diet.

---

## Scenario 9: Cancel Placing an Order

### Steps

1. At the Orders menu, type: `3` (Place New Order).
2. Fill in Order Type: `Lab`, Order Text: `Test Order`.
3. Continue through remaining fields.
4. At the confirmation prompt `Place this order?`, type: `N`.

### Expected Result

- The order is NOT placed.
- Returns to the Orders menu.

---

## Scenario 10: Sign an Order (Happy Path)

### Steps

1. Pre-condition: An unsigned/pending order must exist (place one if needed).
2. Pre-condition: Electronic signature must be set for DOCTOR1 via `POST /api/auth/signature/set`.
3. At the Orders menu, type: `4` (Sign Order).
4. A numbered list of unsigned orders appears:
   ```
   Sign Order
   1  CBC with Differential   PENDING
   2  Troponin I              PENDING
   ```
5. At the prompt `Select order (1-N)`, type: `1`.
6. At the prompt `SIGNATURE CODE:`, type the electronic signature code (masked input).

### Expected Result

- If signature is valid: `Order signed.`
- The order status changes from PENDING to ACTIVE (or signed status).
- Verify by listing active orders -- the signed order now shows updated status.

---

## Scenario 11: Sign an Order -- Invalid Signature

### Steps

1. At the Orders menu, type: `4` (Sign Order).
2. Select an unsigned order.
3. At the `SIGNATURE CODE:` prompt, type: `WRONGCODE`

### Expected Result

- The terminal displays: `*** INVALID SIGNATURE CODE ***`
- The order remains unsigned.
- Returns to the Orders menu.

---

## Scenario 12: Discontinue an Order (Happy Path)

### Steps

1. At the Orders menu, type: `5` (Discontinue Order).
2. A numbered list of active orders appears.
3. Select an order by number.
4. At the prompt `Reason for discontinuation`, type: `Patient no longer requires this medication`
5. At the confirmation prompt `Discontinue '[OrderText]'?`, type: `Y`.

### Expected Result

- The terminal displays: `Order discontinued.`
- Verify by listing active orders -- the discontinued order no longer appears.
- Verify by listing all orders -- the order shows DISCONTINUED status.

---

## Scenario 13: Cancel Discontinuing an Order

### Steps

1. At the Orders menu, type: `5` (Discontinue Order).
2. Select an order.
3. Enter a reason.
4. At the confirmation prompt, type: `N`.

### Expected Result

- The order remains active.
- Returns to the Orders menu.

---

## Scenario 14: Place an Order on Hold

### Steps

1. At the Orders menu, type: `6` (Hold Order).
2. A numbered list of active orders appears.
3. Select an order by number.
4. Confirm when prompted.

### Expected Result

- The terminal displays: `Order placed on hold.`
- The order status changes to HELD.
- The order no longer appears in active orders (option 1) but appears in all orders (option 2) with HELD status.

---

## Scenario 15: Release a Held Order

### Steps

1. At the Orders menu, type: `7` (Release Held Order).
2. A numbered list of held orders appears.
3. Select the held order by number.
4. Confirm when prompted.

### Expected Result

- The terminal displays: `Hold released.`
- The order status returns to ACTIVE.
- Verify by listing active orders -- the order reappears.

---

## Scenario 16: Release Held Order -- No Held Orders

### Steps

1. Ensure no orders are currently on hold.
2. At the Orders menu, type: `7` (Release Held Order).

### Expected Result

- An empty list is displayed or a message indicating no held orders exist.
- Returns to the Orders menu.

---

## Scenario 17: Return to Main Menu

### Steps

1. At the Orders menu, type `Q` or `^` to return.

### Expected Result

- Returns to the Main Menu with patient context preserved.
