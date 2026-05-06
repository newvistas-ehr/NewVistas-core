# Inpatient Pharmacy

**Route:** `/inpatientpharmacy`

The Inpatient Pharmacy module manages inpatient medication orders, including unit dose dispensing, IV admixture coordination, stat/urgent order processing, and ward stock management. This module corresponds to the VistA Inpatient Medications package.

---

## Order Table

The main view displays all inpatient medication orders for a selected patient or the facility-wide order queue.

### Columns

| Column | Description |
|--------|-------------|
| Drug | Medication name |
| Type | Order type badge: **UD** (Unit Dose), **IV** (Intravenous), **LVP** (Large Volume Parenteral) |
| Status | Current order status (ACTIVE, PENDING, HOLD, DISCONTINUED, EXPIRED, COMPLETED) |
| Priority | Order priority (ROUTINE, URGENT, STAT) with color-coded badges |
| Schedule | Dosing schedule (BID, TID, QID, Q6H, Q8H, CONTINUOUS, PRN, ONE-TIME, etc.) |
| Dosage | Prescribed dose and units |
| Ward/Bed | Patient's current ward and bed assignment |
| Verified | Pharmacist verification status (checkmark or pending icon) |
| Last Admin | Date and time of most recent administration (from BCMA) |
| Provider | Ordering provider name and credential |

Click any order row to view the full order detail and available actions.

![Inpatient order list with type badges](screenshots/inpatient-order-list.png)

### Order Actions

The following actions are available depending on the order status:

- **Verify** -- Pharmacist verification of the order. Required before the first dose can be administered.
- **Hold** -- Temporarily hold the order. Requires a hold reason. Nursing is notified that the medication is on hold.
- **Resume** -- Resume a held order, returning it to active status.
- **Discontinue** -- Permanently discontinue the order. Requires a reason. Nursing is notified of the discontinuation.
- **Renew** -- Renew an order that is expiring or has expired, generating a new order with the same parameters.

> **Warning:** Stat and urgent orders are displayed with red and orange priority badges respectively, and appear at the top of the order queue. These orders should be processed immediately. Delays in verifying stat orders directly affect patient safety, as nursing cannot administer an unverified medication.

---

## Key Functions

### Order Review

Review incoming inpatient medication orders for clinical appropriateness before verification.

1. Select a pending order from the queue.
2. Review the medication, dose, route, frequency, and duration against the patient's clinical context.
3. Check for drug-allergy interactions, drug-drug interactions, duplicate therapy, and appropriate dose ranges.
4. Review the patient's active medication profile to ensure the new order fits within the overall treatment plan.
5. If concerns are identified, contact the ordering provider before proceeding.

> **Note:** Inpatient orders often have clinical context that differs from outpatient prescriptions. Consider the patient's current condition, recent lab values, NPO status, renal/hepatic function, and other active orders when reviewing.

### Verification

Pharmacist verification is the gatekeeper for patient safety in the inpatient medication process. No medication may be administered until it has been verified by a pharmacist, except in emergent situations per facility policy.

1. Open the pending order.
2. Complete clinical screening (automated alerts are displayed).
3. Review and resolve or override any alerts with documented clinical justification.
4. Click **Verify** to approve the order.
5. The system records the verifying pharmacist, date, and time.
6. The order status changes to ACTIVE and becomes available for administration in BCMA.

![Inpatient verification workflow](screenshots/inpatient-verification-workflow.png)

### Unit Dose Cart Fill

Generate cart fill lists for nursing unit medication carts. Cart fill lists are used by pharmacy technicians to prepare medication carts for delivery to nursing units.

1. Select the nursing unit (ward) for cart fill generation.
2. Specify the cart fill period (e.g., 24-hour fill, 48-hour fill).
3. The system generates a list of all active verified orders for patients on that unit, including:
   - Patient name and bed
   - Drug name, dose, and dosage form
   - Schedule and administration times
   - Quantity needed for the fill period
4. Print the cart fill list for use in filling the cart.
5. Pharmacy technicians fill the cart from the list.
6. The pharmacist performs a final check of the filled cart before delivery to the nursing unit.

![Cart fill list](screenshots/inpatient-cart-fill-list.png)

> **Tip:** Generate cart fill lists at the same time each day to establish a consistent workflow. Most facilities generate 24-hour cart fills during the day shift.

### IV Admixture Queue

IV orders requiring compounding appear in the IV Admixture Queue. This queue links directly to the IV Pharmacy module for detailed compounding workflows.

1. Review pending IV orders in the queue.
2. Orders are categorized by type: IV (continuous infusion), IVPB (piggyback), and LVP (large volume parenteral).
3. Click any IV order to navigate to the IV Pharmacy module (`/iv-pharmacy`) for compatibility verification and compounding.

See [IV Pharmacy](iv-pharmacy.md) for the complete IV admixture workflow.

### Stat Orders

Stat orders require immediate processing and are highlighted prominently in the inpatient order queue.

1. Stat orders appear at the top of the pending verification queue with a red priority badge.
2. The system may also generate an audible or visual alert (per facility configuration) when a new stat order arrives.
3. Process stat orders immediately:
   - Perform rapid clinical screening.
   - Verify the order.
   - Dispense the first dose for immediate delivery to the nursing unit.
4. Document the stat dispensing, including the time the first dose was delivered.

> **Warning:** Stat order turnaround time is a patient safety metric. Facilities typically require stat medications to be at the bedside within 15-30 minutes of order entry. Monitor and prioritize accordingly.

### Ward Stock Management

Monitor and replenish ward stock medications by nursing unit. Ward stock items are medications stocked on the nursing unit for routine use without individual patient orders (e.g., acetaminophen, ibuprofen, normal saline flushes).

1. Select the nursing unit to review.
2. View current ward stock levels, PAR levels, reorder points, and usage data.
3. Process replenishment requests from nursing.
4. Adjust PAR levels based on usage patterns.

See [Ward Stock](ward-stock.md) for the complete ward stock management workflow.

---

## Order Status Reference

| Status | Description |
|--------|-------------|
| PENDING | New order awaiting pharmacist verification |
| ACTIVE | Verified and available for administration |
| HOLD | Temporarily suspended (hold reason documented) |
| DISCONTINUED | Permanently stopped (D/C reason documented) |
| EXPIRED | Past the order expiration date/time |
| COMPLETED | Order fulfilled (e.g., one-time orders after administration) |

---

## Order Type Reference

| Type Badge | Full Name | Description |
|------------|-----------|-------------|
| **UD** | Unit Dose | Standard oral, topical, or other non-IV medications dispensed in unit dose packaging |
| **IV** | Intravenous | Continuous intravenous infusions requiring compounding |
| **LVP** | Large Volume Parenteral | Large volume IV solutions (typically 250 mL or greater) |

---

## Tips for Efficient Inpatient Pharmacy Operations

> **Tip:** Use the ward/bed column to group orders by nursing unit when processing verifications. This helps identify multiple orders for the same patient and allows for a more comprehensive medication profile review.

> **Tip:** When verifying orders for patients with complex medication regimens, review the full active medication list to check for interactions and duplications across all orders, not just the order being verified.

> **Note:** If a provider enters an order that duplicates an existing active order, the system generates a duplicate therapy alert during verification. Contact the provider to clarify intent before verifying both orders.

![Inpatient pharmacy overview](screenshots/inpatient-pharmacy-overview.png)
