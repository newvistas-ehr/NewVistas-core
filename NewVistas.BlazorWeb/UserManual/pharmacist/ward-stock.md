# Ward Stock

**Route:** `/ward-stock`

The Ward Stock module manages medication inventory for inpatient nursing units. Ward stock consists of medications kept on the nursing unit for routine use, reducing the need for individual patient-specific dispensing for commonly used items. Pharmacists use this module to monitor stock levels, process replenishment requests, adjust PAR levels, and conduct physical counts.

---

## Ward Selection

Upon entering the Ward Stock module, you must first select a ward (nursing unit) to manage.

1. Enter the **Ward ID** in the ward selection field (e.g., `3NORTH`, `ICU`, `4SOUTH`, `PACU`, `ED`, `OR`).
2. Click **Load** to load the ward stock inventory for that ward.
3. The inventory table populates with all medications assigned to that ward's stock list.

> **Note:** Ward IDs correspond to the nursing unit identifiers configured in the system. Contact your Pharmacy Supervisor or system administrator if you are unsure of the correct Ward ID for a specific unit.

---

## Inventory Table

The inventory table displays the current stock status for all medications assigned to the selected ward.

### Columns

| Column | Description |
|--------|-------------|
| Drug Name | Medication name and strength |
| On Hand | Current quantity on the ward |
| PAR Level | The target stock level (Periodic Automatic Replenishment). This is the quantity the ward should have on hand at all times. |
| Reorder Point | The quantity at which a replenishment request is triggered. When the on-hand quantity drops to or below this level, the item needs restocking. |
| Usage (7-day) | Total quantity consumed from this ward stock over the past 7 days. Used for trending and PAR level adjustments. |
| Status | Current stock status indicator (see below) |

### Stock Status Indicators

| Status | Color | Description |
|--------|-------|-------------|
| **STOCKED** | Green | On-hand quantity is above the reorder point. No action needed. |
| **LOW** | Yellow | On-hand quantity is at or below the reorder point but above zero. Replenishment should be initiated. |
| **CRITICAL** | Orange | On-hand quantity is critically low (near zero). Urgent replenishment needed. |
| **OUT_OF_STOCK** | Red | On-hand quantity is zero. The medication is not available on the ward. Immediate action required. |

![Ward stock inventory](screenshots/ward-stock-inventory.png)

> **Warning:** OUT_OF_STOCK status means the nursing unit does not have the medication available. This can delay medication administration. Investigate immediately and expedite restocking for any item showing OUT_OF_STOCK.

---

## Operations

The following operations are available for managing ward stock.

### Restock

Replenish ward stock from the pharmacy.

1. Identify items needing replenishment (LOW, CRITICAL, or OUT_OF_STOCK status).
2. Select the item and click **Restock**.
3. Enter the **Quantity** to send to the ward.
4. The system calculates the suggested restock quantity as: PAR Level minus On Hand.
5. Confirm the restock quantity and click **Submit**.
6. The on-hand quantity is increased by the restocked amount.
7. The transaction is recorded in the ward stock transaction history.

![Restock form](screenshots/ward-stock-restock-form.png)

> **Tip:** When restocking, bring the on-hand quantity up to the PAR level rather than just above the reorder point. This reduces the frequency of replenishment trips and ensures adequate supply between restocking cycles.

### Adjust PAR

Modify the PAR (Periodic Automatic Replenishment) level for a medication on the ward.

1. Select the item and click **Adjust PAR**.
2. Review the current PAR level and 7-day usage data.
3. Enter the new **PAR Level**.
4. Optionally adjust the **Reorder Point** (typically set at 50-75% of the PAR level).
5. Enter a **Reason** for the adjustment (e.g., "Increased usage due to seasonal respiratory illness", "Decreased usage, ward census down").
6. Click **Submit**.

![PAR level adjustment](screenshots/ward-stock-par-adjustment.png)

**When to adjust PAR levels:**

- **Increase** when the 7-day usage consistently exceeds the current PAR level, leading to frequent LOW or OUT_OF_STOCK events.
- **Decrease** when the 7-day usage is consistently well below the PAR level, indicating excess stock that may expire before use.
- **Seasonal adjustments** for medications with predictable seasonal demand patterns (e.g., bronchodilators in winter, antihistamines in spring).

> **Note:** PAR level changes affect the restocking workflow for the ward. Significant changes should be communicated to the nursing unit staff so they understand the new expected stock levels.

### Record Usage

Document medication usage from ward stock. Usage recording is essential for maintaining accurate on-hand quantities and for tracking consumption patterns.

1. Select the item and click **Record Usage**.
2. Enter the **Quantity** used.
3. Enter **Notes** if applicable (e.g., patient identifier, clinical reason, shift).
4. Click **Submit**.
5. The on-hand quantity is decreased by the used amount.

> **Note:** In facilities with automated dispensing cabinets (ADCs), usage may be recorded automatically when medications are removed from the cabinet. Manual usage recording is needed for medications not stocked in ADCs.

### Physical Count

Conduct a physical count to reconcile the system's on-hand quantity with the actual quantity on the ward.

1. Select the item (or all items for a full ward count) and click **Physical Count**.
2. Physically count the medication on the ward.
3. Enter the **Actual Count** -- the quantity determined by physical counting.
4. The system compares the actual count to the expected on-hand quantity:
   - **Match** (green) -- Counts agree.
   - **Overage** (yellow) -- Actual count is higher than expected.
   - **Shortage** (red) -- Actual count is lower than expected.
5. For discrepancies, enter a **Reason** or investigation notes.
6. Click **Submit** to update the on-hand quantity to match the actual count.

> **Tip:** Conduct physical counts on a regular schedule (weekly or bi-weekly) to maintain inventory accuracy. More frequent counts may be needed for high-use items or items with recurring discrepancies.

### Add Drug

Add a new medication to the ward stock list.

1. Click **Add Drug**.
2. Search for the medication by name.
3. Select the medication from the drug file results.
4. Enter the initial **PAR Level** for the ward.
5. Enter the **Reorder Point** (suggested: 50-75% of PAR level).
6. Enter the initial **On Hand** quantity (0 if not yet stocked).
7. Click **Submit**.
8. The medication appears in the ward stock inventory table.

> **Note:** Adding medications to the ward stock list should be coordinated with nursing leadership for the unit. Ward stock medications should be limited to frequently used items to minimize waste from expiration and to maintain inventory manageability.

### Remove Drug

Remove a medication from the ward stock list.

1. Select the item and click **Remove Drug**.
2. Confirm the removal. The system may prompt you to document the reason.
3. The item is removed from the ward stock list.
4. Any remaining on-hand quantity should be physically retrieved from the ward and returned to the pharmacy.

> **Note:** Removing a drug from the ward stock list does not affect individual patient orders for that medication. Patients who need the medication will have it dispensed from the pharmacy as a patient-specific dose.

---

## Key Functions

### View Ward Stock Levels by Unit/Location

1. Select a ward from the Ward Selection field.
2. The inventory table shows all medications and their current status.
3. Use the status indicators (STOCKED, LOW, CRITICAL, OUT_OF_STOCK) to quickly identify items needing attention.
4. To compare stock levels across wards, load each ward separately and review.

### Replenishment Requests from Nursing Units

Nursing staff may submit electronic replenishment requests when they identify low stock on their unit.

1. Replenishment requests appear as notifications in the Pharmacy Hub and/or the Ward Stock module.
2. Review the request, confirming the item, quantity needed, and priority.
3. Process the request using the **Restock** operation.
4. Deliver the restocked medications to the nursing unit.

### Stock Adjustments for Corrections and Write-offs

When on-hand quantities need correction outside of normal usage or replenishment:

1. Use the **Physical Count** operation to adjust the quantity to the actual on-hand amount.
2. Document the reason for the adjustment:
   - **Correction** -- Fixing a documentation error (e.g., usage not recorded, receipt not recorded).
   - **Write-off** -- Removing expired, damaged, or unusable medications from the count.
   - **Found stock** -- Adding medications found that were not previously recorded.
3. All adjustments are recorded in the transaction history for audit purposes.

### Formulary Compliance Monitoring

Ensure that ward stock medications are consistent with the facility formulary.

1. Review the ward stock list periodically (monthly or quarterly).
2. Cross-reference ward stock items against the current facility formulary status.
3. Identify and remove non-formulary items unless a specific exception has been approved.
4. Add newly approved formulary items that are appropriate for ward stock based on unit needs.

### Expiration Tracking

Monitor medication expiration dates to prevent the use of expired medications on the ward.

1. During physical counts, check expiration dates on all ward stock items.
2. Identify items approaching expiration (within 30-90 days per facility policy).
3. For items approaching expiration:
   - Rotate stock (first-expiring, first-out).
   - If the item will likely expire before use, return it to the pharmacy for potential redistribution to a higher-volume location.
4. Remove and document any expired medications immediately.

> **Warning:** Expired medications must never be administered to patients. Remove expired medications from the ward immediately upon discovery and process through the pharmacy return/waste workflow.

---

## Ward Stock Best Practices

> **Tip:** Focus restocking efforts at consistent times (e.g., early morning shift) to ensure wards are fully stocked before peak medication administration times.

> **Tip:** Review 7-day usage trends when adjusting PAR levels. A single high-usage day may not justify a PAR increase -- look for sustained patterns over multiple weeks.

> **Tip:** Coordinate ward stock changes with nursing leadership. Nurses need to know what is available on their unit, and their input on utilization patterns is valuable for PAR optimization.

> **Note:** For controlled substances on ward stock (if permitted by facility policy), additional accountability requirements apply. See [Controlled Substances](controlled-substances.md) for controlled substance handling procedures.
