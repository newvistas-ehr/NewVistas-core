# Controlled Substances

**Route:** `/controlled-substances`

The Controlled Substances module manages all aspects of DEA Schedule II-V medication handling, including dispensing documentation, physical counts, inspections, and compliance reporting. This module corresponds to the VistA Controlled Substances package and enforces strict accountability requirements mandated by the DEA and facility policy.

> **Warning:** All controlled substance transactions require meticulous documentation. Federal and state regulations impose severe penalties for discrepancies, diversion, and inadequate recordkeeping. Follow your facility's controlled substance policies at all times.

---

## Tabs

The Controlled Substances module is organized into three tabs: Dispense Log, Inspections, and Record.

### Tab 1: Dispense Log

The Dispense Log provides a chronological record of all controlled substance dispensing transactions. This log serves as the primary accountability record and is subject to DEA inspection.

#### Table Columns

| Column | Description |
|--------|-------------|
| Date/Time | Date and time of the dispensing transaction |
| Patient | Patient name and identifier |
| Drug | Medication name and strength |
| Schedule | DEA schedule badge: **II** (red), **III** (orange), **IV** (yellow), **V** (gray) |
| Qty | Quantity dispensed |
| Unit | Unit of measure (TAB, CAP, mL, PATCH, SUPP, etc.) |
| Running Balance | Current balance of the drug after this transaction |
| Dispensed By | Name of the person who performed the dispensing |

#### Filters

- **DEA Schedule** -- Filter by schedule (II, III, IV, V, or All).
- **Drug Name** -- Search by drug name.
- **Date Range** -- Filter by date range.
- **Location** -- Filter by vault or dispensing location.

#### Running Balance Alerts

The running balance column tracks the expected on-hand quantity of each controlled substance. The system monitors this balance for anomalies:

- **Negative running balances** are highlighted in **red**. A negative balance indicates that more medication has been documented as dispensed than was received, which is a compliance issue requiring immediate investigation.
- **Unexpected changes** in balance (large decreases without corresponding patient transactions) are flagged for review.

> **Warning:** A negative running balance is a serious compliance issue. It may indicate a documentation error, a missed receipt, or potential diversion. Investigate immediately and report to the Pharmacy Supervisor and facility security.

![Dispense log with schedule badges](screenshots/cs-dispense-log.png)

---

### Tab 2: Inspections

The Inspections tab manages controlled substance physical count inspections. Inspections are the primary mechanism for verifying that physical inventory matches documented records.

#### Inspection List

| Column | Description |
|--------|-------------|
| Inspection ID | Unique inspection identifier |
| Date | Date the inspection was conducted |
| Inspector | Name of the inspector |
| Type | Inspection type badge (see types below) |
| Status | Inspection status: **OPEN** (in progress), **FINALIZED** (complete, no discrepancies), **FAILED** (complete, discrepancies found) |
| Discrepancies | Count of discrepancies found during the inspection |

#### Inspection Types

| Type | Description |
|------|-------------|
| ROUTINE | Regularly scheduled inspections (e.g., monthly, quarterly) per facility policy |
| RANDOM | Unannounced inspections conducted at random intervals |
| INCIDENT | Inspections triggered by a specific incident (e.g., suspected diversion, patient complaint) |
| CHANGE_OF_SHIFT | Inspections conducted during pharmacy shift changes to verify accountability transfer |

#### Creating an Inspection

1. Click **New Inspection** on the Inspections tab.
2. Select the **Inspection Type** from the dropdown (ROUTINE, RANDOM, INCIDENT, or CHANGE_OF_SHIFT).
3. Enter the **Inspector Name** (the person conducting the inspection).
4. Click **Create** to generate the inspection record.
5. The inspection is created with OPEN status.

#### Recording Counts

Once an inspection is created, you must record the physical count for each controlled substance at the inspection location.

1. Open the OPEN inspection.
2. For each controlled substance item:
   - The system displays the **Drug Name**, **DEA Schedule**, **Unit**, and **Expected Count** (based on the running balance from the dispense log).
   - Enter the **Physical Count** -- the actual quantity determined by physical counting.
   - The system automatically calculates any **Discrepancy** (Physical Count minus Expected Count).
   - Discrepancies are highlighted:
     - **Green** -- Counts match (discrepancy = 0).
     - **Red** -- Counts do not match (discrepancy != 0).
3. Repeat for all controlled substances at the location.

![Inspection form with count entry](screenshots/cs-inspection-count.png)

#### Finalizing an Inspection

After all counts are recorded, finalize the inspection.

1. Review all count entries for accuracy.
2. Click **Finalize Inspection**.
3. The system evaluates all counts:
   - **All counts match (no discrepancies):** The inspection status is set to **FINALIZED**. The inspection is complete.
   - **One or more discrepancies found:** The inspection status is set to **FAILED**. The system generates a discrepancy report.
4. For FAILED inspections:
   - Document the investigation findings for each discrepancy.
   - Report to the Pharmacy Supervisor immediately.
   - Report to facility security as required by policy.
   - Maintain all documentation for DEA review.

> **Warning:** All count discrepancies must be investigated immediately and reported to the Pharmacy Supervisor and facility security. Unresolved discrepancies may trigger a DEA investigation and jeopardize the facility's controlled substance registration.

![Discrepancy alert](screenshots/cs-discrepancy-alert.png)

---

### Tab 3: Record (Dispense Entry)

The Record tab provides a form for documenting controlled substance dispensing transactions. Every dispensing event must be recorded here.

#### Fields

| Field | Required | Description |
|-------|----------|-------------|
| Patient ID | Yes | Patient identifier |
| Patient Name | Yes | Patient full name |
| Drug Name | Yes | Controlled substance name and strength |
| DEA Schedule | Yes | DEA schedule (II, III, IV, or V) |
| Quantity | Yes | Quantity dispensed |
| Unit | Yes | Unit of measure (TAB, CAP, mL, PATCH, etc.) |
| Dispensed By | Yes | Name of the person performing the dispensing |
| Witness | Yes | Name of the witness to the dispensing transaction |

#### Recording a Dispensing Transaction

1. Navigate to the Record tab.
2. Enter the **Patient ID** and **Patient Name**.
3. Enter the **Drug Name** and **DEA Schedule**.
4. Enter the **Quantity** and **Unit**.
5. Enter the **Dispensed By** name (the pharmacist or technician performing the dispensing).
6. Enter the **Witness** name. A witness is required for all controlled substance dispensing transactions.
7. Click **Submit** to record the transaction.
8. The transaction is added to the Dispense Log with an updated running balance.

> **Note:** The witness must be physically present during the dispensing and must visually verify the quantity dispensed. Witness documentation without actual observation is a compliance violation.

---

## Controlled Substance Handling Best Practices

> **Tip:** Perform change-of-shift counts at every shift change, even if not strictly required by facility policy. This practice limits the window of accountability and makes discrepancy investigation easier.

> **Tip:** Keep the vault organized with drugs in alphabetical order and clearly labeled. This reduces counting errors and speeds up inspections.

> **Note:** Schedule II substances (e.g., oxycodone, morphine, fentanyl, hydromorphone, methylphenidate, amphetamine salts) require the highest level of accountability. Many facilities require perpetual inventory (every transaction counted) for Schedule II items.

---

## DEA Schedule Reference

| Schedule | Risk Level | Examples |
|----------|-----------|----------|
| **II** | High potential for abuse; severe dependence | Oxycodone, Morphine, Fentanyl, Hydromorphone, Methylphenidate, Amphetamine |
| **III** | Moderate potential for abuse; moderate dependence | Buprenorphine, Testosterone, Ketamine, Codeine combinations |
| **IV** | Lower potential for abuse; limited dependence | Benzodiazepines (Diazepam, Lorazepam, Alprazolam), Zolpidem, Tramadol |
| **V** | Lowest potential for abuse; limited dependence | Pregabalin, Cough preparations with codeine (some states), Lacosamide |

> **Note:** State scheduling may differ from federal DEA scheduling. Always follow the more restrictive schedule when state and federal classifications differ.
