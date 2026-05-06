# Order Entry (Limited Access) -- Nurse CharUI Human Test Script

## Prerequisites

- **Login:** NURSE1 / Password: `smythVista1`
- **Security Keys:** ORELSE (allows placing orders), GMRV VITALS, GMRA ALLERGY, GMPL PROBLEM, SD SCHEDULING
- **Keys NOT held:** ORES (cannot sign orders), OREMAS (cannot discontinue orders)
- **Patient:** Select a patient with demo data loaded.
- **Pre-conditions:** SiloHost and WebServer running. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: List Active Orders (Happy Path)

### Steps

1. At the Main Menu, type: `OR` and press Enter.
2. At the Orders menu, type: `1` (List Active Orders).

### Expected Result

- A table displays: #, Order, Type, Status, Date, Provider.
- Read-only view -- no restrictions on viewing orders.

---

## Scenario 2: List All Orders

### Steps

1. At the Orders menu, type: `2` (List All Orders).

### Expected Result

- All orders displayed regardless of status.

---

## Scenario 3: Place a New Order Using ORELSE Key (Happy Path)

### Steps

1. At the Orders menu, type: `3` (Place New Order).
2. **Note:** Nurses hold ORELSE (not ORES), which permits order entry.
3. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Order Type (Lab, Rad, Med, Diet, Consult, Other) | `Lab` |
| Order Text | `Fingerstick Blood Glucose` |
| Urgency (Routine, STAT, ASAP) | `Routine` |
| Instructions (optional) | `AC (before meals) and HS (bedtime)` |
| Indication (optional) | `Diabetes monitoring per nursing protocol` |

4. Confirm: `Y`

### Expected Result

- The terminal displays: `Order placed: [order-ID]`
- The order is created with Status: PENDING.
- **Note:** The order remains PENDING because a nurse cannot sign it (no ORES key). A physician must sign it.

---

## Scenario 4: Place a STAT Lab Order

### Steps

1. At the Orders menu, type: `3`.
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Order Type | `Lab` |
| Order Text | `Point-of-Care Troponin` |
| Urgency | `STAT` |
| Instructions | `Patient complaining of chest pain, onset 20 min ago` |
| Indication | `Chest pain, R/O MI` |

3. Confirm: `Y`

### Expected Result

- Order placed with STAT urgency.

---

## Scenario 5: Place a Diet Order

### Steps

1. At the Orders menu, type: `3`.
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Order Type | `Diet` |
| Order Text | `NPO after midnight` |
| Urgency | `Routine` |
| Instructions | `Pre-surgical NPO for scheduled procedure tomorrow` |
| Indication | `Pre-operative preparation` |

3. Confirm: `Y`

### Expected Result

- Order placed successfully.

---

## Scenario 6: Cancel Placing an Order

### Steps

1. At the Orders menu, type: `3`.
2. Fill in fields.
3. At confirmation, type: `N`.

### Expected Result

- Order NOT placed.

---

## Scenario 7: Attempt to Sign an Order -- ACCESS DENIED

### Steps

1. At the Orders menu, type: `4` (Sign Order).

### Expected Result

- The terminal displays: `You do not hold the ORES key. Signing is not permitted.`
- Returns to the Orders menu.
- **This is the expected behavior for nurses** -- orders placed by nurses require physician co-signature.

---

## Scenario 8: Attempt to Discontinue an Order -- ACCESS DENIED

### Steps

1. At the Orders menu, type: `5` (Discontinue Order).

### Expected Result

- The terminal displays: `You do not hold the ORES key. DC is not permitted.`
- Returns to the Orders menu.
- **Expected behavior** -- nurses cannot discontinue orders without ORES or OREMAS key.

---

## Scenario 9: Place an Order on Hold (Permitted)

### Steps

1. At the Orders menu, type: `6` (Hold Order).
2. A list of active orders appears.
3. Select an order.
4. Confirm when prompted.

### Expected Result

- `Order placed on hold.`
- **Hold/Release do not require security keys** -- nurses can hold and release orders.

---

## Scenario 10: Release a Held Order (Permitted)

### Steps

1. At the Orders menu, type: `7` (Release Held Order).
2. Select a held order.
3. Confirm.

### Expected Result

- `Hold released.`
- Order returns to active status.

---

## Scenario 11: Return to Main Menu

### Steps

1. At the Orders menu, type `Q` or `^`.

### Expected Result

- Returns to the Main Menu.
