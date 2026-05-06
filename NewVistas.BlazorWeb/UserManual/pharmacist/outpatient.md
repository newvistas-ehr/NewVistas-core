# Outpatient Pharmacy

**Route:** `/outpatientpharmacy`

The Outpatient Pharmacy module handles the complete outpatient prescription dispensing workflow, from receipt of new orders through clinical screening, pharmacist verification, dispensing, labeling, and patient counseling. This module corresponds to the VistA Outpatient Pharmacy V7 package.

---

## Prescription List

The main view displays all outpatient prescriptions for the selected patient or the facility-wide pending queue, depending on context.

### Table Columns

| Column | Description |
|--------|-------------|
| Drug | Medication name (generic or brand, per facility preference) |
| Dosage | Prescribed dose and units (e.g., 10 mg, 500 mg) |
| Status | Current prescription status (ACTIVE, PENDING, HOLD, DISCONTINUED, EXPIRED, SUSPENDED) |
| Priority | Order priority (ROUTINE, URGENT, STAT) |
| Refills Left | Number of authorized refills remaining |
| Expires | Prescription expiration date |
| Verified | Verification status (checkmark if verified, pending icon if not) |
| Counsel | Whether patient counseling is required (Yes/No) |
| Provider | Ordering provider name and credential |

Click any row to expand the detail panel for that prescription.

![Outpatient prescription list](screenshots/outpatient-prescription-list.png)

### Detail Panel

When a prescription is selected, the detail panel displays comprehensive information:

| Field | Description |
|-------|-------------|
| Rx ID | Unique prescription identifier |
| SIG | Complete prescriber directions (e.g., "Take 1 tablet by mouth twice daily with food") |
| Route | Route of administration (PO, TOP, INH, etc.) |
| Schedule | Dosing schedule (BID, TID, QID, QHS, PRN, etc.) |
| Days Supply | Number of days the dispensed quantity should last |
| Quantity | Total quantity to dispense |
| Verified By | Name and credential of verifying pharmacist (blank if pending) |
| Counseling Required | Whether new prescription counseling or change counseling is indicated |
| D/C Reason | Reason for discontinuation, if applicable |
| Hold Reason | Reason the prescription is on hold, if applicable |

![Prescription detail panel](screenshots/outpatient-prescription-detail.png)

### Prescription Actions

The following actions are available from the prescription list or detail panel:

- **Renew** -- Renew an active or recently expired prescription. Generates a new prescription with the same parameters and resets the refill count.
- **Edit** -- Modify prescription parameters (dose, quantity, days supply, refills, SIG). Changes require re-verification.
- **Discontinue** -- Discontinue the prescription. Requires a reason for discontinuation.
- **Hold** -- Place the prescription on hold. Requires a hold reason. The prescription cannot be dispensed while on hold.
- **Release** -- Release a prescription from hold status, returning it to active or pending verification.
- **Verify** -- Pharmacist verification action. Opens the clinical screening and verification workflow.
- **Print Label** -- Generate and print the prescription label and patient information leaflet.

---

## Outpatient Dispensing Workflow

The standard outpatient dispensing workflow follows five steps. Each step must be completed before proceeding to the next.

### Step 1: Review Pending Prescriptions

Review each pending prescription for completeness and appropriateness.

1. Navigate to the Outpatient Pharmacy module (`/outpatientpharmacy`).
2. Select a patient or use the facility-wide pending queue.
3. Review each pending prescription, checking the following:
   - **Patient name** -- Confirm correct patient identification.
   - **Drug** -- Verify the medication name, strength, and dosage form.
   - **Dose** -- Confirm the prescribed dose is appropriate for the indication.
   - **Route** -- Verify the route of administration matches the dosage form.
   - **Frequency** -- Confirm the dosing schedule is appropriate.
   - **Quantity and days supply** -- Verify the quantity matches the days supply and dosing frequency.
   - **Refills** -- Confirm the number of refills is appropriate for the medication class and duration of therapy.
   - **Provider** -- Verify the prescriber has authority to prescribe the medication.

> **Note:** For controlled substances (DEA Schedule II-V), additional review requirements apply. Schedule II prescriptions cannot have refills. See [Controlled Substances](controlled-substances.md) and [EPCS](epcs.md) for additional details.

### Step 2: Clinical Screening

The system performs automated clinical screening checks. Review all alerts and resolve or override as appropriate.

1. The system automatically runs the following checks when you open a prescription for verification:
   - **Drug-allergy interaction** -- Cross-references the prescribed medication against the patient's documented allergies.
   - **Drug-drug interaction** -- Checks for interactions with all active medications on the patient's profile.
   - **Duplicate therapy** -- Identifies prescriptions for the same therapeutic class already active on the profile.
   - **Dose range check** -- Verifies the prescribed dose falls within established minimum and maximum ranges.
   - **Formulary status** -- Indicates whether the medication is formulary, non-formulary, restricted, or has criteria for use.
2. Review each alert. Alerts are classified by severity:
   - **Critical** (red) -- Potentially life-threatening interactions or contraindications. Must be resolved or overridden with documented clinical justification.
   - **Significant** (orange) -- Clinically significant interactions requiring review. May be overridden with clinical judgment.
   - **Minor** (yellow) -- Informational alerts. Review and acknowledge.
3. For critical or significant alerts, either:
   - Contact the prescriber to discuss the concern and obtain a modified order, or
   - Override the alert with a documented clinical rationale.

![Clinical screening alerts](screenshots/outpatient-clinical-screening.png)

> **Warning:** Critical drug-allergy alerts should never be overridden without direct communication with the prescriber and documented clinical justification. Patient safety depends on thorough screening.

### Step 3: Verify Prescription

After completing clinical screening, verify the prescription to approve it for dispensing.

1. Confirm all clinical screening alerts have been reviewed, resolved, or appropriately overridden.
2. Click the **Verify** button to approve the prescription for dispensing.
3. The system records the verifying pharmacist's name, credential, date, and time.
4. The prescription status changes from PENDING to ACTIVE and moves to the Dispensing Queue.

> **Note:** If issues are identified during review that cannot be resolved immediately, place the prescription on **Hold** with a documented reason rather than leaving it in the pending queue indefinitely.

![Verify button on prescription](screenshots/outpatient-verify-button.png)

### Step 4: Dispense and Label

Once verified, the prescription is ready for physical dispensing and label generation.

1. Select the verified prescription from the Dispensing Queue.
2. Click **Print Label** to generate the prescription label and patient information leaflet.
3. The label includes:
   - Patient name and identifier
   - Drug name, strength, and dosage form
   - Quantity dispensed
   - SIG directions
   - Prescriber name
   - Dispensing pharmacist name
   - Rx number and refill number
   - Expiration date
   - Auxiliary labels (as applicable)
4. The patient information leaflet includes:
   - Drug name (generic and brand)
   - Purpose and indication
   - Dosing instructions
   - Common side effects
   - Storage requirements
   - Warnings and precautions
5. Affix the label to the dispensed medication container and include the patient information leaflet.

### Step 5: Patient Counseling

Provide counseling to the patient or caregiver, particularly for new prescriptions or changes to existing therapy.

1. Confirm the patient's identity.
2. Counsel on the following points:
   - **Drug name** -- Generic and brand name of the medication.
   - **Purpose** -- The reason the medication was prescribed.
   - **Dose and administration** -- How much to take, how often, and how to take it (with food, on an empty stomach, etc.).
   - **Side effects** -- Common and serious side effects to watch for, and what to do if they occur.
   - **Storage** -- How to store the medication (room temperature, refrigeration, protect from light, etc.).
   - **Interactions** -- Foods, beverages, or other medications to avoid.
   - **Refills** -- Number of refills remaining and how to request refills.
3. Document that counseling was provided in the system.
4. Mark the prescription as dispensed.

> **Tip:** Counseling is especially important for new prescriptions, dose changes, medications with narrow therapeutic indices, high-alert medications, and medications with complex administration requirements (e.g., inhalers, injectable pens, patches).

---

## Prescription Status Reference

| Status | Description |
|--------|-------------|
| PENDING | New order received, awaiting pharmacist verification |
| ACTIVE | Verified and available for dispensing or refill |
| HOLD | Temporarily suspended (requires hold reason) |
| DISCONTINUED | Permanently stopped (requires D/C reason) |
| EXPIRED | Past the prescription expiration date |
| SUSPENDED | Suspended for auto-refill or CMOP processing |

---

## Common Tasks

### Renewing a Prescription

1. Select the prescription to renew from the prescription list.
2. Click **Renew**.
3. Review and confirm the prescription parameters (drug, dose, quantity, days supply, refills).
4. The system generates a new prescription linked to the original. The new prescription enters the pending verification queue.

### Placing a Prescription on Hold

1. Select the prescription.
2. Click **Hold**.
3. Enter the hold reason (e.g., "Awaiting prescriber callback re: dose clarification").
4. The prescription status changes to HOLD and is removed from the dispensing queue.

### Releasing a Held Prescription

1. Select the held prescription.
2. Click **Release**.
3. The prescription returns to its previous status (PENDING or ACTIVE).

![Outpatient pharmacy workflow](screenshots/outpatient-workflow-overview.png)
