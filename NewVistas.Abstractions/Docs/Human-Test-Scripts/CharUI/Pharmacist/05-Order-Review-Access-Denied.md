# Order Review (Access Denied Scenarios) -- Pharmacist CharUI Human Test Script

## Prerequisites

- **Login:** PHARM1 / Password: `smythVista1`
- **Security Keys:** PSO PHARMACY, PSJ RPHARM, PSA ORDERS, PSB MANAGER
- **Keys NOT held:** ORES (cannot place/sign/discontinue orders), ORELSE (cannot place orders)
- **Patient:** Select a patient with demo data loaded (orders should exist).
- **Pre-conditions:** SiloHost and WebServer running. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: List Active Orders (Permitted -- Read-Only)

### Steps

1. At the Main Menu, type: `OR` and press Enter.
2. At the Orders menu, type: `1` (List Active Orders).

### Expected Result

- Table displays: #, Order, Type, Status, Date, Provider.
- Pharmacist CAN view all orders.
- **Pharmacist review focus:** Check for:
  - Medication orders pending verification
  - Duplicate therapy
  - Dose appropriateness
  - Drug interactions with current medication profile

---

## Scenario 2: List All Orders (Permitted)

### Steps

1. At the Orders menu, type: `2` (List All Orders).

### Expected Result

- All orders displayed regardless of status.
- Useful for reviewing complete order history including discontinued medications.

---

## Scenario 3: Attempt to Place a New Order -- ACCESS DENIED

### Steps

1. At the Orders menu, type: `3` (Place New Order).

### Expected Result

- The terminal displays: `You do not hold the ORES or ORELSE key. Order entry is not permitted.`
- Returns to the Orders menu.
- **Note:** Pharmacists use the full pharmacy system (Blazor UI) for order entry and verification, not the CharUI Orders module.

---

## Scenario 4: Attempt to Sign an Order -- ACCESS DENIED

### Steps

1. At the Orders menu, type: `4` (Sign Order).

### Expected Result

- The terminal displays: `You do not hold the ORES key. Signing is not permitted.`
- Returns to the Orders menu.

---

## Scenario 5: Attempt to Discontinue an Order -- ACCESS DENIED

### Steps

1. At the Orders menu, type: `5` (Discontinue Order).

### Expected Result

- The terminal displays: `You do not hold the ORES key. DC is not permitted.`
- Returns to the Orders menu.

---

## Scenario 6: Place an Order on Hold (Permitted -- No Key Required)

### Steps

1. At the Orders menu, type: `6` (Hold Order).
2. A list of active orders appears.
3. Select an order.
4. Confirm when prompted.

### Expected Result

- `Order placed on hold.`
- Pharmacists CAN hold orders (e.g., pending clarification, DUR issue).

---

## Scenario 7: Release a Held Order (Permitted)

### Steps

1. At the Orders menu, type: `7` (Release Held Order).
2. Select a held order.
3. Confirm.

### Expected Result

- `Hold released.`
- Order returns to active status.

---

## Scenario 8: Hold -- No Active Orders

### Steps

1. Ensure no active orders exist.
2. Type: `6` (Hold Order).

### Expected Result

- Empty list.

---

## Scenario 9: Release -- No Held Orders

### Steps

1. Ensure no held orders exist.
2. Type: `7` (Release Held Order).

### Expected Result

- Empty list.

---

## Scenario 10: Verify All Access Denied Operations (Summary)

### Steps

1. Attempt options 3, 4, and 5 in sequence.

### Expected Result

| Option | Operation | Key Required | Pharmacist Has? | Result |
|--------|-----------|--------------|-----------------|--------|
| 3 | Place Order | ORES or ORELSE | No | ACCESS DENIED |
| 4 | Sign Order | ORES | No | ACCESS DENIED |
| 5 | Discontinue Order | ORES or OREMAS | No | ACCESS DENIED |
| 6 | Hold Order | (none) | N/A | PERMITTED |
| 7 | Release Held | (none) | N/A | PERMITTED |

---

## Scenario 11: Return to Main Menu

### Steps

1. At the Orders menu, type `Q` or `^`.

### Expected Result

- Returns to the Main Menu.
