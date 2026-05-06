# Auto-Refill and CMOP

This document covers two related pharmacy modules: the Auto-Refill system for managing automatic prescription refill programs, and the CMOP (Centralized Mail-Out Pharmacy) system for processing prescriptions for mail delivery.

---

## Auto-Refill

**Route:** `/auto-refill`

The Auto-Refill module manages the automatic prescription refill program, which generates refill orders for eligible maintenance medications without requiring the patient to request each refill individually. This module corresponds to the VistA Auto-Refill functionality within the Outpatient Pharmacy package.

### Tabs

#### Enrollment Tab

The Enrollment tab manages patient enrollment and disenrollment in the auto-refill program.

**Available Actions:**

| Action | Description |
|--------|-------------|
| **Enroll** | Enroll an eligible prescription in the auto-refill program. The prescription must be active with refills remaining. |
| **Suspend** | Temporarily suspend auto-refill processing for a prescription. The enrollment is preserved but refills are not automatically generated. |
| **Resume** | Resume auto-refill processing for a previously suspended prescription. |
| **Disenroll** | Permanently remove a prescription from the auto-refill program. The patient must re-enroll to resume auto-refills. |

**Enrollment Criteria:**

To be eligible for auto-refill enrollment, a prescription must meet the following criteria:

- The prescription must be in ACTIVE status.
- The prescription must have refills remaining.
- The medication should be a maintenance medication (intended for long-term, chronic use).
- The patient must consent to auto-refill enrollment.
- Controlled substances (DEA Schedule II-V) are generally not eligible for auto-refill per facility policy.

> **Note:** Auto-refill enrollment is per-prescription, not per-patient. A patient may have some prescriptions enrolled and others not enrolled. Review each prescription individually for eligibility and appropriateness.

#### Suspension Tab

The Suspension tab provides a focused view of all prescriptions currently in suspended auto-refill status. Use this tab to review and manage suspensions.

- **Columns:** Patient, Rx Number, Drug, Suspension Date, Suspension Reason, Refills Remaining
- **Actions:** Resume (return to active auto-refill) or Disenroll (permanently remove from auto-refill)

Common suspension reasons include:

- Patient request (e.g., traveling, temporary change in medication)
- Clinical hold (e.g., awaiting lab results, provider review)
- Insurance change (e.g., new plan, coverage gap)
- Adverse effect under investigation

#### Dashboard Tab

The Dashboard tab provides an operational overview of all auto-refill prescriptions and their current status.

| Column | Description |
|--------|-------------|
| Patient | Patient name and identifier |
| Rx Number | Prescription number |
| Drug | Medication name and strength |
| Days Supply | Days supply per fill |
| Last Fill Date | Date of the most recent fill |
| Refill Due | Calculated date when the next refill is due (based on last fill date and days supply) |
| Refills Remaining | Number of refills still authorized |
| Status | Auto-refill status: **ACTIVE** (enrolled and processing), **SUSPENDED** (temporarily paused), **DISENROLLED** (permanently removed) |

**Filters:**

- **Status** -- Filter by All, Active, Suspended, or Due Now.
- **Due Now** -- Shows only prescriptions with a refill due date on or before today. These are the prescriptions that need immediate processing.

![Auto-refill dashboard with due indicators](screenshots/auto-refill-dashboard.png)

> **Tip:** Review the "Due Now" filter at the start of each shift. Processing due refills promptly ensures patients receive their medications without gaps in therapy.

### Auto-Refill Workflow

Follow these four steps to manage the auto-refill process.

#### Step 1: Enroll Eligible Prescriptions

1. Navigate to the Enrollment tab.
2. Search for the patient and prescription to enroll.
3. Verify the prescription meets enrollment criteria (active status, refills remaining, maintenance medication, patient consent).
4. Click **Enroll**.
5. The prescription appears on the Dashboard with ACTIVE status.

#### Step 2: Review Due Refills

1. Navigate to the Dashboard tab.
2. Apply the **Due Now** filter to see prescriptions due for refill processing.
3. Review each due prescription:
   - Confirm the prescription is still clinically appropriate.
   - Verify the patient's address and contact information are current (especially for CMOP mail-out).
   - Check for any clinical alerts (drug interactions, allergy changes, lab monitoring due).

#### Step 3: Process Due Refills

1. Select one or more due prescriptions for processing.
2. Click **Process Refills**.
3. The system generates refill orders that enter the standard outpatient dispensing workflow:
   - Orders appear in the Outpatient Pharmacy pending verification queue.
   - Standard clinical screening is performed.
   - Pharmacist verification is required.
   - Prescriptions are dispensed via the normal process (window pickup, delivery, or CMOP mail-out).

#### Step 4: Manage Suspensions

1. Review the Suspension tab periodically for prescriptions that may need to be reactivated or permanently disenrolled.
2. For prescriptions ready to resume: click **Resume**.
3. For prescriptions that should be permanently removed: click **Disenroll**.
4. Follow up with patients on extended suspension to determine appropriate action.

> **Note:** Auto-refill prescriptions that expire (no refills remaining or past expiration date) are automatically disenrolled by the system. The prescriber must issue a new prescription for the patient to be re-enrolled.

---

## CMOP

**Route:** `/cmop`

The CMOP (Centralized Mail-Out Pharmacy) module manages the electronic transmission of prescriptions to a centralized mail-out pharmacy for fulfillment and shipment directly to the patient. This module corresponds to the VistA CMOP package.

### Site Selection

Upon entering the CMOP module, select the pharmacy site.

1. Enter the **Site ID** in the site selection field.
2. Click **Load** to load the CMOP data for that site.

### Tabs

#### Suspense Queue Tab

The Suspense Queue displays all prescriptions that have been queued for CMOP transmission but have not yet been sent.

| Column | Description |
|--------|-------------|
| Rx# | Prescription number |
| Patient | Patient name and identifier |
| Drug | Medication name and strength |
| Qty | Quantity to dispense |
| Days | Days supply |
| Fill Type | Type of fill: **NEW** (first fill), **REFILL** (subsequent fill), **PARTIAL** (partial fill) |
| Priority | Priority: **ROUTINE** or **URGENT** |
| Queued Date | Date the prescription was added to the CMOP queue |

**Operations:**

| Operation | Description |
|-----------|-------------|
| **Transmit to CMOP** | Create a transmission batch from selected queue items and submit electronically to the CMOP facility |
| **Remove** | Remove a prescription from the CMOP queue (returns to local dispensing) |
| **Refresh** | Refresh the queue to show the latest data |

![CMOP suspense queue](screenshots/cmop-suspense-queue.png)

#### Transmissions Tab

The Transmissions tab tracks all CMOP transmission batches and their current status.

| Column | Description |
|--------|-------------|
| Transmission ID | Unique transmission batch identifier |
| Items | Number of prescriptions in the transmission |
| Status | Current transmission status (see workflow below) |
| Transmitted Date | Date/time the transmission was sent |
| Acknowledged Date | Date/time the CMOP facility acknowledged receipt |
| Dispensed Date | Date/time the CMOP facility completed dispensing |
| Shipped Date | Date/time the shipment was sent |

**Transmission Statuses:**

```
TRANSMITTED → ACKNOWLEDGED → DISPENSED → SHIPPED → COMPLETE
                                                  ↘ CANCELLED
```

| Status | Description |
|--------|-------------|
| TRANSMITTED | Batch has been electronically submitted to the CMOP facility |
| ACKNOWLEDGED | CMOP facility has received and acknowledged the transmission |
| DISPENSED | CMOP facility has dispensed the medications |
| SHIPPED | Shipment has been sent to the patient |
| COMPLETE | Patient has received the shipment (or delivery confirmed) |
| CANCELLED | Transmission was cancelled (prescriptions return to local queue or local dispensing) |

![CMOP transmission status](screenshots/cmop-transmission-status.png)

#### Add to Queue Tab

The Add to Queue tab provides a form for manually adding prescriptions to the CMOP suspense queue.

| Field | Required | Description |
|-------|----------|-------------|
| Patient ID | Yes | Patient identifier |
| Prescription ID | Yes | Prescription number |
| Drug Name | Yes | Medication name and strength |
| Quantity | Yes | Quantity to dispense |
| Days Supply | Yes | Number of days the quantity should last |
| Priority | Yes | ROUTINE or URGENT |

1. Enter the required fields.
2. Click **Add to Queue**.
3. The prescription appears in the Suspense Queue tab.

### CMOP Transmission Workflow

Follow these four steps to process CMOP transmissions.

#### Step 1: Review Suspense Queue

1. Navigate to the Suspense Queue tab.
2. Review all queued prescriptions:
   - Verify the patient's mailing address is current and valid. Incorrect addresses result in returned shipments and delayed care.
   - Confirm the medication is available at the CMOP facility.
   - Check for any clinical holds or issues that should prevent transmission.
3. Remove any prescriptions that should not be transmitted (e.g., address issues, clinical holds, patient requests for local pickup).

#### Step 2: Create Transmission Batch

1. Select the prescriptions to include in the transmission batch.
   - You may select all eligible items or create targeted batches (e.g., by priority or patient group).
2. Click **Transmit to CMOP**.
3. The system creates a transmission batch with a unique Transmission ID.

#### Step 3: Submit Electronically

1. The system electronically submits the transmission batch to the CMOP facility.
2. The transmission status changes to TRANSMITTED.
3. Monitor the Transmissions tab for status updates:
   - ACKNOWLEDGED -- The CMOP facility has received the batch.
   - DISPENSED -- The CMOP facility has filled the prescriptions.
   - SHIPPED -- The shipment has been mailed to the patient.

#### Step 4: Track Shipping

1. Monitor the Transmissions tab for shipment status.
2. For prescriptions in SHIPPED status, tracking information may be available.
3. Handle exceptions:
   - **Returned shipments** -- Investigate the reason (wrong address, patient moved, refused delivery). Update patient address and retransmit or convert to local dispensing.
   - **Cancelled transmissions** -- Prescriptions from cancelled transmissions return to the local queue or may need to be dispensed locally.
   - **Delayed shipments** -- Follow up with the CMOP facility for transmissions that remain in DISPENSED status beyond expected timeframes.

> **Tip:** Process CMOP transmissions early in the day to maximize the chance of same-day or next-day shipping from the CMOP facility. Urgent prescriptions should be transmitted as soon as they are queued.

> **Warning:** Always verify the patient's mailing address before CMOP transmission. Medications shipped to an incorrect address may be lost, and controlled substances shipped to the wrong address create additional regulatory concerns.
