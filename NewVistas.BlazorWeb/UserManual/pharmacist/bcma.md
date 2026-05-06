# BCMA (Pharmacist Perspective)

**Route:** `/bcma`

The BCMA (Bar Code Medication Administration) module is the primary tool for safe medication administration in inpatient settings. While **nurses are the primary BCMA users** (see the Nurse Guide for complete BCMA administration workflows), pharmacists interact with BCMA in several important supporting roles:

- **Verify medication labels** generate correctly and scan properly.
- **Investigate administration discrepancies** reported by nursing staff.
- **Review administration history** for clinical consultations, drug utilization reviews, and therapeutic monitoring.
- **Support missing dose and late dose investigations** when nursing reports that a medication is not available or was not administered on time.

> **Note:** Pharmacists typically access BCMA in a read-only or support capacity. Medication administration recording is performed by nursing staff at the bedside using the barcode scanner. See the Nurse Guide for step-by-step medication administration procedures.

---

## MAR Tab

The Medication Administration Record (MAR) tab displays all active medications for a selected patient, along with their administration status and schedule.

### Table Columns

| Column | Description |
|--------|-------------|
| Drug | Medication name displayed in **bold**, with the patient's ward and bed shown below |
| Dose/Route | Prescribed dose, units, and route of administration (e.g., "10 mg PO", "1000 mL IV") |
| Schedule | Dosing schedule (BID, TID, QID, Q6H, Q8H, CONTINUOUS, PRN, ONE-TIME, etc.) |
| Priority | Order priority with color coding: **Red** = STAT, **Orange** = URGENT/ASAP, **Gray** = ROUTINE |
| Last Given | Date and time of the most recent administration |
| Status | Current administration status (DUE, GIVEN, HELD, REFUSED, NOT GIVEN, LATE) |
| Count | Number of doses administered in the current period |
| Action | Available actions (Record, View History) |

### Due Medication Highlighting

Medications that are currently due for administration are highlighted in **yellow**. The due window is typically 60 minutes before and after the scheduled administration time (configurable per facility policy).

- **Yellow highlight** -- Medication is within the due window and should be administered.
- **Red highlight** -- Medication is past the due window (late).
- **No highlight** -- Medication is not currently due.

![BCMA MAR view](screenshots/bcma-mar-view.png)

### Pharmacist Use of the MAR

As a pharmacist, use the MAR tab to:

1. **Verify medication availability** -- When nursing reports a missing dose, check the MAR to confirm the medication is ordered, verified, and active. If the medication shows as active but nursing cannot find it, investigate the dispensing status.

2. **Check administration timing** -- Review last administration times to assess adherence to the dosing schedule. Late or missed doses may indicate a dispensing delay, nursing workflow issue, or patient refusal.

3. **Review PRN usage patterns** -- For PRN (as needed) medications, review the frequency and timing of administration to assess appropriateness and effectiveness.

4. **Support clinical consultations** -- When consulted about a patient's medication therapy, review the MAR to see what has actually been administered (as opposed to what was ordered). There may be differences due to held doses, refusals, or NPO status.

---

## History Tab

The History tab provides a detailed record of all medication administration events for a patient. This is the primary view for investigating administration discrepancies and conducting clinical reviews.

### Table Columns

| Column | Description |
|--------|-------------|
| Drug | Medication name and strength |
| Dose/Route | Administered dose and route |
| Time | Date and time of the administration event |
| Administered By | Name of the person who administered the medication |
| Witness | Name of the witness (for controlled substances and high-alert medications) |
| Status | Administration status: **GIVEN**, **HELD**, **REFUSED**, **NOT_GIVEN** |
| PRN Reason | For PRN medications: the reason for administration (e.g., pain, nausea, anxiety) |
| PRN Effectiveness | For PRN medications: documented effectiveness assessment |

### Pharmacist Use of the History Tab

Use the History tab to:

1. **Investigate discrepancies** -- When nursing reports an administration discrepancy (e.g., dose given but not scanned, wrong medication scanned, timing error), review the history to reconstruct what occurred.

2. **Conduct drug utilization reviews** -- Review administration patterns for specific drug classes (e.g., opioid use, antimicrobial days of therapy) to support clinical pharmacy programs.

3. **Support adverse event investigations** -- When a patient experiences an adverse drug event, review the administration history to determine exactly what was given, when, and by whom.

4. **Verify controlled substance administration** -- For controlled substances, review that administration records include appropriate witness documentation and that the timing and quantity match the dispensing records.

![BCMA administration history](screenshots/bcma-administration-history.png)

> **Tip:** When investigating a discrepancy, cross-reference the BCMA administration history with the pharmacy dispensing records and the controlled substance dispense log (if applicable). Discrepancies between these records may indicate a documentation error, a workflow issue, or a potential diversion concern.

---

## Record Tab (Manual Entry)

The Record tab provides a form for manually recording medication administration events. This is used in situations where the standard barcode scanning workflow was not possible (e.g., barcode scanner malfunction, medication label damaged, emergency administration).

> **Warning:** Manual entries bypass the barcode verification safety check. Manual entries should only be used when barcode scanning is not possible. All manual entries require appropriate justification and may be subject to additional review.

### Fields

| Field | Required | Description |
|-------|----------|-------------|
| Drug Name | Yes | Medication name and strength |
| Dosage | Yes | Administered dose and units |
| Route | Yes | Route of administration: PO, IV, IM, SC, SL, TOP, PR, INH |
| Schedule | Yes | Dosing schedule for context |
| Administered By | Yes | Name of the person who administered the medication |
| Witness | Conditional | Name of the witness. **Required for controlled substances** and high-alert medications. |
| PRN Reason | Conditional | Reason for administration. **Required for PRN medications.** |
| PRN Effectiveness | No | Effectiveness assessment for PRN medications (documented after administration) |

### Recording a Manual Entry

1. Navigate to the Record tab.
2. Select the patient.
3. Enter the **Drug Name** and **Dosage**.
4. Select the **Route** from the dropdown.
5. Select or enter the **Schedule**.
6. Enter the **Administered By** name.
7. If the medication is a controlled substance: enter the **Witness** name.
8. If the medication is a PRN medication: enter the **PRN Reason**.
9. Click **Submit** to record the administration.

> **Note:** Manual entries are flagged in the administration history for audit purposes. Facility policy may require additional documentation or supervisor approval for manual BCMA entries.

---

## Common Pharmacist BCMA Scenarios

### Missing Dose Investigation

When nursing reports a missing dose:

1. Check the MAR tab to confirm the order is active and verified.
2. Check the pharmacy dispensing system to confirm the medication was dispensed and included in the most recent cart fill.
3. Check the BCMA history to see if the dose was already administered (possibly by a different nurse).
4. If the medication was dispensed but cannot be found, check for possible misplacement or mislabeling.
5. If the medication was not dispensed, expedite dispensing and delivery.

### Late Dose Investigation

When a medication is flagged as late:

1. Review the MAR tab to identify which medications are past due.
2. Check whether the delay is pharmacy-related (late verification, late cart fill, late delivery) or nursing-related (workflow, patient unavailable, clinical hold).
3. If pharmacy-related, take corrective action to prevent recurrence.
4. Document the investigation findings if required by facility policy.

### Administration Discrepancy

When nursing reports an administration discrepancy (e.g., barcode did not match, wrong medication scanned):

1. Review the BCMA history for the patient.
2. Compare the scanned medication information with the ordered medication.
3. Determine whether the discrepancy was a scanning error, a labeling error, or an actual wrong-medication event.
4. If a wrong medication was administered, escalate to the provider and follow the facility's adverse event reporting process.
5. If a labeling or scanning error, correct the issue and document.

> **Tip:** Collaborate closely with nursing staff when investigating BCMA discrepancies. Nursing can provide bedside context (patient condition, environmental factors) that may not be apparent from the system records alone.
