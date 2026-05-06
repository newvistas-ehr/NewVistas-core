# IV Pharmacy

**Route:** `/iv-pharmacy`

The IV Pharmacy module manages intravenous medication orders from initial review through compounding, labeling, and delivery. This module corresponds to the VistA IV Pharmacy (Intravenous Medications) package and supports continuous infusions, intermittent piggyback (IVPB) preparations, and large volume parenterals (LVP).

---

## Tabs

The IV Pharmacy module is organized into four tabs, each representing a stage of the IV workflow or an action.

### All Orders

The All Orders tab displays every IV order in the system, regardless of status. This provides a comprehensive view of IV activity.

| Column | Description |
|--------|-------------|
| Base Solution | Primary IV solution (e.g., D5W, NS, LR, D5 1/2NS) |
| Vol (mL) | Total volume in milliliters |
| Route | Administration route: **IV** (continuous), **IVPB** (piggyback), **Central** (central line) |
| Frequency | Dosing frequency (CONTINUOUS, Q6H, Q8H, Q12H, Q24H, ONCE, PRN) |
| Priority | Order priority: **ROUTINE**, **STAT**, **ASAP** |
| Status | Current order status (see Order Status Workflow below) |
| Provider | Ordering provider name |
| Lot # | Compounding lot number (assigned during compounding) |
| Expires | Beyond-use date/time of the compounded preparation |
| Created | Order creation date/time |
| Additives | Count of additives in the order (click to expand additive details) |

Use the status filter to narrow the view to specific workflow stages.

![IV orders list](screenshots/iv-orders-list.png)

### Pending/Verified

The Pending/Verified tab shows a filtered view of orders in either PENDING or VERIFIED status. These are orders awaiting pharmacist review and verification, or orders that have been verified and are awaiting compounding.

- **Pending orders** require pharmacist verification before they can proceed to compounding.
- **Verified orders** have been approved by a pharmacist and are ready for the compounding queue.

This tab is the primary working view for pharmacists reviewing new IV orders.

### Compounding Queue

The Compounding Queue tab displays orders that are currently in the IV room for compounding. Orders move to this tab after a pharmacist verifies them and a technician or pharmacist moves them into the compounding workflow.

- **Columns:** Patient, Base Solution, Additives, Volume, Priority, Assigned To, Started, Estimated Completion
- **Actions:** Mark as compounding in progress, complete compounding, generate label

### New Order

The New Order tab provides a form for entering new IV orders directly from the IV Pharmacy module.

#### Required Fields

| Field | Description |
|-------|-------------|
| Base Solution | The primary IV solution (required). Select from the formulary list or type to search. |
| Total Volume (mL) | Total volume of the final preparation in milliliters (required). |
| Route | Route of administration (required): IV, IVPB, or Central. |
| Frequency | Dosing frequency (required): CONTINUOUS, Q6H, Q8H, Q12H, Q24H, ONCE, PRN. |
| Priority | Order priority (required): ROUTINE, STAT, or ASAP. |
| Provider | Ordering provider (required). Must be a credentialed provider with IV ordering privileges. |

#### Additives

Each IV order may include one or more additives. For each additive, provide:

| Field | Description |
|-------|-------------|
| Drug Name | Name of the additive medication (e.g., Potassium Chloride, Heparin, Insulin Regular) |
| Dose | Amount of the additive |
| Dose Unit | Unit of measurement: **mg**, **mEq**, **units**, **mL** |

Click **Add Additive** to add additional additives to the order. Click the remove icon next to an additive to remove it.

![New IV order form with additives](screenshots/iv-new-order-form.png)

> **Note:** IV orders entered through this module still require pharmacist verification before compounding, even if entered by a pharmacist. This ensures the verification step is never bypassed.

---

## Order Status Workflow

IV orders progress through a defined series of statuses:

```
PENDING → VERIFIED → COMPOUNDING → READY → ACTIVE → COMPLETED
                                                   ↘ DISCONTINUED
```

| Status | Description |
|--------|-------------|
| PENDING | Order received, awaiting pharmacist verification |
| VERIFIED | Pharmacist has verified the order; ready for compounding |
| COMPOUNDING | Order is being prepared in the IV room |
| READY | Compounding is complete; preparation is ready for delivery to the nursing unit |
| ACTIVE | Preparation has been delivered and is being administered to the patient |
| COMPLETED | Infusion has been completed |
| DISCONTINUED | Order was discontinued before completion |

> **Tip:** Use the status workflow to track the location and progress of every IV order. The Compounding Queue tab provides a focused view of orders currently in the IV room.

---

## Processing an IV Admixture Order

Follow these four steps to process an IV admixture order from receipt through delivery.

### Step 1: Review Order

Review the IV order for completeness, appropriateness, and safety.

1. Open the pending IV order from the Pending/Verified tab.
2. Review the following:
   - **Base solution** -- Verify the solution type and volume are appropriate for the patient and indication.
   - **Additives** -- Review each additive for appropriate dose and concentration.
   - **Rate** -- If specified, verify the infusion rate is within safe limits.
   - **Compatibility** -- Assess whether all additives are compatible with the base solution and with each other.
   - **Patient factors** -- Consider the patient's fluid status, renal function, electrolyte levels, and other clinical parameters.
3. Review the patient's active IV orders to identify potential conflicts or redundancies.

### Step 2: Verify Compatibility

Confirm that all components of the IV preparation are physically and chemically compatible.

1. Check each additive for compatibility with the base solution:
   - Consult IV compatibility references (e.g., Trissel's Handbook on Injectable Drugs).
   - Verify concentration limits for each additive.
   - Confirm stability data for the combination and assigned beyond-use dating.
2. Check additives for compatibility with each other:
   - Some additives may be incompatible when combined in the same solution (e.g., certain electrolytes, calcium with phosphate, bicarbonate with calcium).
3. Verify concentration safety:
   - Confirm that the final concentration of each additive falls within established safe ranges.
   - Check for maximum concentration limits for the route of administration (peripheral IV vs. central line).
4. If any compatibility issues are identified:
   - Contact the ordering provider to discuss alternatives.
   - Suggest separating incompatible additives into different solutions.
   - Document the clinical rationale for any changes.

> **Warning:** Never compound an IV preparation with known incompatibilities. Incompatible admixtures can cause precipitation, loss of drug activity, or generation of toxic byproducts. Always verify compatibility before proceeding.

### Step 3: Compound

Prepare the IV admixture using aseptic technique in the appropriate environment.

1. After verification, move the order to the Compounding Queue by clicking **Begin Compounding**.
2. The order status changes to COMPOUNDING.
3. Prepare the admixture in the appropriate environment:
   - Laminar airflow workbench (LAFW) for standard preparations.
   - Biological safety cabinet (BSC) for hazardous drug preparations.
   - Compounding aseptic isolator (CAI) or compounding aseptic containment isolator (CACI) as applicable.
4. Follow USP 797/800 standards for compounding.
5. Document the compounding details:
   - Lot number assigned to the preparation
   - Preparer identification
   - Date and time of compounding
   - Beyond-use date based on stability data and storage conditions

![Compounding queue](screenshots/iv-compounding-queue.png)

### Step 4: Label and Complete

Generate the IV label and finalize the preparation for delivery.

1. Click **Generate Label** to produce the IV preparation label.
2. The label includes:
   - Patient name and identifier
   - Ward and bed
   - Base solution and volume
   - Each additive with dose and concentration
   - Infusion rate (if specified)
   - Beyond-use date and time
   - Storage requirements
   - Preparer identification
   - Lot number
   - Barcode for BCMA scanning
3. Affix the label to the IV container.
4. Perform a final visual inspection of the preparation:
   - Check for particulate matter.
   - Check for discoloration.
   - Verify the label matches the order.
5. Mark the order as READY in the system.
6. Deliver the preparation to the nursing unit or place in the appropriate storage location.

![IV label preview](screenshots/iv-label-preview.png)

> **Tip:** For continuous infusions, prepare the next bag before the current bag is due to expire. Monitor the "Expires" column in the All Orders tab to plan ahead.

---

## IV Solution and Additive Reference

### Common Base Solutions

| Abbreviation | Full Name |
|-------------|-----------|
| NS | 0.9% Sodium Chloride (Normal Saline) |
| D5W | 5% Dextrose in Water |
| D5NS | 5% Dextrose in 0.9% Sodium Chloride |
| D5 1/2NS | 5% Dextrose in 0.45% Sodium Chloride |
| LR | Lactated Ringer's Solution |
| 1/2NS | 0.45% Sodium Chloride (Half Normal Saline) |
| D10W | 10% Dextrose in Water |
| SWFI | Sterile Water for Injection |

### Common Additives

| Additive | Typical Units |
|----------|--------------|
| Potassium Chloride (KCl) | mEq |
| Magnesium Sulfate | g or mg |
| Heparin | units |
| Insulin Regular | units |
| Calcium Gluconate | g or mg |
| Sodium Bicarbonate | mEq |
| Multivitamins (MVI) | mL |
| Famotidine | mg |
| Methylprednisolone | mg |

---

## Troubleshooting

> **Note:** If an IV order appears in the Pending tab but you cannot verify it, confirm that the ordering provider is credentialed and that all required fields are complete. Incomplete orders cannot be verified.

> **Note:** If the compounding queue shows orders that are no longer needed, check with the nursing unit before discontinuing. The order may still be active but the patient's status may have changed.
