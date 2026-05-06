# Drug Accountability

**Route:** `/drugaccountability`

The Drug Accountability module manages drug inventory, accountability transactions, and physical inventory reconciliation across pharmacy locations (vaults, dispensing areas, ward stock locations). This module corresponds to the VistA Drug Accountability/Inventory package and provides comprehensive tracking of all medication movements.

---

## Location Selection

Upon entering the Drug Accountability module, you must first select a location to manage.

1. Enter the **Location ID** in the Location field (e.g., `VAULT-001`, `MAIN-PHARMACY`, `SATELLITE-2`, `NARCOTICS-SAFE`).
2. Click **Load** to load the inventory for that location.
3. The inventory table populates with all drugs assigned to that location.

> **Tip:** Your facility will have defined location IDs for each pharmacy vault, dispensing area, and satellite pharmacy. Contact your Pharmacy Supervisor if you are unsure of the correct location ID for your area.

---

## Inventory Table

The inventory table displays the current stock status for all medications at the selected location.

### Columns

| Column | Description |
|--------|-------------|
| Drug Name | Medication name and strength |
| Balance | Current on-hand quantity |
| Unit | Unit of measure: **TAB** (tablet), **CAP** (capsule), **mL** (milliliter), **EA** (each), **PATCH**, **SUPP** (suppository), **GM** (gram), **VIAL** |
| Type | Drug type badge: **DEA** (controlled substance with schedule number) or **RX** (non-controlled prescription medication) |
| Reorder Point | Quantity at which a reorder should be triggered |
| Status | Stock status indicator: **OK** (adequate stock), **LOW STOCK** (at or below reorder point) |
| Actions | Available transaction actions for the item |

### Filters

- **Low stock only** -- Show only items with LOW STOCK status (at or below reorder point).
- **Controlled only** -- Show only items with DEA type badge (controlled substances).
- **Drug name search** -- Filter by drug name (partial match supported).

![Inventory table with status indicators](screenshots/da-inventory-table.png)

---

## Transaction Functions

All medication movements in and out of a location must be documented as transactions. The following transaction types are available.

| Function | Description |
|----------|-------------|
| **Receive** | Record receipt of medications from suppliers, wholesalers, or inter-facility transfers. Increases the location balance. |
| **Dispense** | Document dispensing of medications to patients or wards. Decreases the location balance. |
| **Return** | Process medication returns from patients, nursing units, or other locations. Increases the location balance. |
| **Waste** | Document medication waste or destruction. Decreases the location balance. Controlled substance waste requires a witness. |
| **Transfer** | Record transfers of medications between pharmacy locations, wards, or facilities. Decreases the source balance and increases the destination balance. |
| **Physical Inventory** | Conduct and document a full physical count of all items at the location. Adjusts balances to match actual counts. |

### Transaction Fields

When recording any transaction, the following fields are available (required fields vary by transaction type):

| Field | Description |
|-------|-------------|
| Quantity | Number of units involved in the transaction (required for all types) |
| Reason/Notes | Free-text field for documenting the reason for the transaction or additional notes |
| Lot Number | Manufacturer lot number for the medication (for receipt and tracking) |
| Expiration Date | Medication expiration date (for receipt and tracking) |
| Witness | Name of the witness (required for controlled substance waste transactions) |

![Transaction form](screenshots/da-transaction-form.png)

---

### Receive

Record the receipt of medications from suppliers or other sources.

1. Select the drug from the inventory list, or search for a drug not yet in the location inventory.
2. Click **Receive**.
3. Enter the **Quantity** received.
4. Enter the **Lot Number** from the manufacturer label.
5. Enter the **Expiration Date** from the manufacturer label.
6. Enter any **Notes** (e.g., purchase order number, supplier name).
7. Click **Submit**.
8. The location balance is increased by the received quantity.

> **Note:** For controlled substances, receipt must also be documented on DEA Form 222 (for Schedule II) or the electronic equivalent (CSOS). Ensure the DEA receipt documentation matches the system receipt record.

### Dispense

Document the dispensing of medications to patients or nursing units.

1. Select the drug from the inventory list.
2. Click **Dispense**.
3. Enter the **Quantity** dispensed.
4. Enter the **Reason/Notes** (e.g., patient name and ID, prescription number, ward destination).
5. Click **Submit**.
6. The location balance is decreased by the dispensed quantity.

### Return

Process medication returns from patients, nursing units, or other pharmacy locations.

1. Select the drug from the inventory list.
2. Click **Return**.
3. Enter the **Quantity** returned.
4. Enter the **Reason/Notes** (e.g., patient discharged, order discontinued, temperature excursion).
5. Enter the **Lot Number** and **Expiration Date** if known.
6. Click **Submit**.
7. The location balance is increased by the returned quantity.

> **Note:** Returned medications must be inspected for integrity before being returned to stock. Medications that have been out of pharmacy control (e.g., patient returns from home) should generally not be restocked and should be documented as waste instead.

### Waste

Document the waste or destruction of medications.

1. Select the drug from the inventory list.
2. Click **Waste**.
3. Enter the **Quantity** wasted.
4. Enter the **Reason/Notes** (e.g., expired, contaminated, partial dose waste, patient refused).
5. For controlled substances: Enter the **Witness** name. The witness must be physically present to observe the waste.
6. Click **Submit**.
7. The location balance is decreased by the wasted quantity.

> **Warning:** Controlled substance waste requires a witness who physically observes the destruction of the medication. Both the waster and the witness must be identified in the record. Unwitnessed controlled substance waste is a compliance violation.

### Transfer

Record the transfer of medications between locations.

1. Select the drug from the inventory list.
2. Click **Transfer**.
3. Enter the **Quantity** transferred.
4. Enter the destination location in **Reason/Notes** (e.g., "Transfer to SATELLITE-2", "Transfer to Ward 3C stock").
5. Click **Submit**.
6. The source location balance is decreased by the transferred quantity.

> **Note:** Transfers between locations should be recorded at both the sending and receiving locations. Coordinate with the receiving location to ensure both records match.

---

## Physical Inventory Process

A physical inventory is a complete count of all medications at a location, used to reconcile the system balance with the actual on-hand quantity. Physical inventories should be conducted on a regular schedule per facility policy.

### Step 1: Initiate Inventory

1. Select the location for the physical inventory.
2. Click **Physical Inventory** from the transaction actions.
3. The system generates a count sheet listing all drugs at the location with their expected (system) balances.

### Step 2: Count Each Item

1. Physically count each medication item at the location.
2. For each item, enter the **Actual Quantity** -- the physical count determined by hand counting.
3. Work systematically through the location (alphabetically, by shelf, or by section) to ensure no items are missed.

### Step 3: Compare and Review

1. After all counts are entered, the system compares the actual quantity to the expected quantity for each item.
2. Discrepancies are highlighted:
   - **Match** (green) -- Actual count matches the expected balance.
   - **Overage** (yellow) -- Actual count is higher than expected. May indicate a missed receipt or documentation error.
   - **Shortage** (red) -- Actual count is lower than expected. May indicate a missed dispensing, waste, or potential diversion.
3. Review all discrepancies carefully.

![Physical inventory count](screenshots/da-physical-inventory.png)

### Step 4: Investigate, Document, and Finalize

1. For each discrepancy, investigate the cause:
   - Review recent transaction history for the item.
   - Check for pending transactions not yet recorded.
   - Look for documentation errors (wrong drug, wrong quantity, wrong location).
   - For controlled substance shortages, escalate to the Pharmacy Supervisor and facility security.
2. Document the investigation findings and resolution in the **Notes** field for each discrepancy.
3. Click **Finalize Inventory** to apply the adjustments.
4. The system updates all balances to match the actual physical counts and creates adjustment transactions for each discrepancy.

> **Warning:** Controlled substance discrepancies identified during physical inventory must be reported immediately to the Pharmacy Supervisor and investigated per facility policy. Do not simply adjust the balance without a thorough investigation and documentation.

---

## Inventory Management Best Practices

> **Tip:** Run the "Low stock only" filter at the start of each shift to identify items that need reordering. Proactive reordering prevents stockouts that can delay patient care.

> **Tip:** When receiving medications, always verify the lot number and expiration date against the packing slip and the physical product. Discrepancies should be reported to the supplier.

> **Note:** The Drug Accountability module tracks inventory at the location level. For facility-wide inventory reporting and analytics, consult the pharmacy management reports (if available) or export data from multiple locations.

> **Tip:** First-expiring, first-out (FEFO) stock rotation helps minimize medication waste due to expiration. When receiving new stock, place it behind existing stock with earlier expiration dates.
