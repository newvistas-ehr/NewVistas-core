# Pharmacy Benefits and Point-of-Sale

This document covers two related pharmacy modules: Pharmacy Benefits (patient benefit verification, prior authorization, and coverage determination) and Pharmacy POS (point-of-sale claims processing and payment transactions).

---

## Pharmacy Benefits

**Route:** `/pharmacybenefits`

The Pharmacy Benefits module manages patient pharmacy benefit information, including plan enrollment, formulary coverage, prior authorization, coverage determination, and copay information. This module supports third-party billing, VA pharmacy benefits, and coordination of benefits workflows.

### Tabs

#### Patient Benefits Tab

The Patient Benefits tab displays the pharmacy benefit plans associated with a selected patient.

| Column | Description |
|--------|-------------|
| Plan Name | Name of the pharmacy benefit plan |
| Status | Plan status (Active, Inactive, Pending) |
| Insurer | Insurance company or PBM (Pharmacy Benefit Manager) name |
| Member ID | Patient's member or subscriber ID for the plan |
| Group | Group number or identifier |
| Effective Date | Date the plan coverage became effective |
| Expires | Date the plan coverage expires |
| Copay Tiers | Summary of copay structure by tier (Tier 1/Generic, Tier 2/Preferred Brand, Tier 3/Non-Preferred, Tier 4/Specialty) |

Click any plan to view the full benefit detail, including:

- Plan type (commercial, Medicare Part D, Medicaid, VA, TRICARE, etc.)
- Coverage limitations (quantity limits, days supply limits, step therapy requirements)
- Prior authorization requirements by drug class
- Copay amounts by tier
- Deductible status and remaining amount
- Out-of-pocket maximum status
- Mail-order versus retail coverage differences

![Patient benefits tab](screenshots/benefits-patient-benefits.png)

#### Plan Formulary Tab

The Plan Formulary tab allows you to search for a drug within a specific benefit plan's formulary to determine coverage, tier placement, and any restrictions.

1. Select the patient's benefit plan.
2. Enter a drug name to search.
3. Review the results:
   - **Formulary status** -- Covered, Not Covered, or Covered with Restrictions
   - **Tier placement** -- Tier 1 (generic), Tier 2 (preferred brand), Tier 3 (non-preferred), Tier 4 (specialty)
   - **Prior authorization required** -- Yes or No
   - **Step therapy required** -- Yes or No (and which medications must be tried first)
   - **Quantity limits** -- Maximum quantity or days supply per fill or per time period
   - **Patient copay** -- Estimated copay amount for the medication

### Key Functions

#### Benefit Verification

Verify a patient's pharmacy benefit eligibility before processing a prescription.

1. Select the patient.
2. Navigate to the Patient Benefits tab.
3. Review the active benefit plan(s) and their effective dates.
4. Confirm the patient's member ID and group number match the information on file.
5. If no active plan is found, check with the patient for updated insurance information.

> **Tip:** Always verify benefits before dispensing, especially for new patients, at the start of a new calendar year (when plans frequently change), and when a patient reports a change in insurance. Dispensing without benefit verification can result in rejected claims and unpaid prescriptions.

#### Prior Authorization

Submit and track prior authorization (PA) requests for non-formulary or restricted medications.

1. Identify the need for prior authorization:
   - The clinical screening process flags medications requiring PA during prescription verification.
   - The Plan Formulary tab indicates PA requirements for specific drugs.
2. Initiate a PA request:
   - Enter the drug name, indication, and clinical justification.
   - Attach supporting clinical documentation (lab results, previous therapy failures, diagnosis).
   - Submit the PA request to the insurer or PBM.
3. Track the PA request status:
   - **Pending** -- Request submitted, awaiting review.
   - **Approved** -- PA approved (effective dates and duration noted).
   - **Denied** -- PA denied (denial reason provided).
   - **Appeal** -- Denial appealed with additional documentation.
4. Notify the prescriber and patient of the PA outcome.

> **Note:** PA processing times vary by insurer. Urgent PAs may be processed within 24 hours, while standard PAs may take 3-5 business days. Plan accordingly and provide interim therapy if clinically necessary.

#### Coverage Determination

Review coverage determination decisions and manage appeals.

1. When a claim is denied or a PA is denied, review the denial reason.
2. Determine whether an appeal is appropriate based on:
   - Clinical justification for the requested medication
   - Available alternatives on the plan formulary
   - Patient's clinical history and previous therapy attempts
3. If appealing, prepare the appeal documentation including:
   - Letter of medical necessity from the prescriber
   - Supporting clinical documentation
   - Applicable clinical guidelines or evidence
4. Submit the appeal and track the outcome.

#### Copay Information

View the patient's copay obligations for prescribed medications.

1. Select the patient's benefit plan.
2. Search for the specific medication in the Plan Formulary.
3. Review the estimated copay amount based on the drug's tier placement.
4. Communicate copay information to the patient before dispensing.

> **Tip:** When a patient's copay is unexpectedly high, check for generic or therapeutic alternatives that may be on a lower formulary tier. Discuss options with the prescriber if a switch could save the patient significant cost without compromising clinical effectiveness.

---

## Pharmacy POS

**Route:** `/pharmacy-pos`

The Pharmacy POS (Point-of-Sale) module manages electronic claims processing with insurers and PBMs, payment collection at the pharmacy window, and transaction management. This module handles the financial side of prescription dispensing.

### Tabs

#### POS Claims Tab

The POS Claims tab displays all electronic claims and their processing status.

**Status Filter:**

| Status | Description |
|--------|-------------|
| **Pending** | Claim created but not yet transmitted to the insurer |
| **Transmitted** | Claim sent to the insurer/PBM for adjudication |
| **Paid** | Claim adjudicated and approved for payment |
| **Rejected** | Claim rejected by the insurer/PBM (rejection reason provided) |
| **Reversed** | Previously paid claim that has been reversed (e.g., prescription returned, billing correction) |
| **Duplicate** | Claim identified as a duplicate of an existing claim |
| **Pending Review** | Claim flagged for manual review before processing |

#### Creating a Claim

| Field | Required | Description |
|-------|----------|-------------|
| Transaction Type | Yes | NCPDP transaction type (see below) |
| BIN | Yes | Bank Identification Number (IIN) for the PBM |
| PCN | No | Processor Control Number |
| Group ID | No | Group identifier |
| Cardholder ID | Yes | Patient's cardholder/member ID |
| Drug NDC | Yes | National Drug Code of the dispensed medication |
| Quantity | Yes | Quantity dispensed |
| Days Supply | Yes | Days supply dispensed |
| Ingredient Cost | Yes | Pharmacy's acquisition cost for the medication |
| Dispensing Fee | Yes | Pharmacy's dispensing fee |

**Transaction Types:**

| Code | Description |
|------|-------------|
| **B1** | Billing (new claim submission) |
| **B2** | Reversal (reverse a previously paid claim) |
| **B3** | Rebill (resubmit a corrected claim) |
| **E1** | Eligibility verification |
| **D1** | Information reporting (DUR/PPS) |
| **P1** | Prior authorization inquiry |

![POS claim form](screenshots/pos-claim-form.png)

#### Claim Detail Tab

The Claim Detail tab displays the full adjudication detail for a selected claim.

- **Claim Information** -- All submitted claim fields (BIN, PCN, drug NDC, quantity, etc.)
- **Adjudication Detail** -- Insurer/PBM response including:
  - Ingredient cost allowed
  - Dispensing fee allowed
  - Total amount payable
  - Patient copay amount
  - Plan pay amount
  - Tax amount (if applicable)
- **Response Codes** -- NCPDP response codes indicating the adjudication outcome
- **Rejection Reasons** -- For rejected claims, specific NCPDP rejection codes and descriptions (e.g., 75 = Prior Authorization Required, 70 = Product/Service Not Covered, 79 = Refill Too Soon)
- **DUR/PPS Responses** -- Drug Utilization Review and Professional Pharmacy Service responses from the PBM

![Claim adjudication detail](screenshots/pos-claim-detail.png)

> **Note:** Common rejection codes and their resolutions:
> - **75 (Prior Authorization Required)** -- Submit a prior authorization request through the Pharmacy Benefits module.
> - **70 (Product/Service Not Covered)** -- Check for covered alternatives or submit a coverage determination.
> - **79 (Refill Too Soon)** -- The patient is attempting to refill before the plan allows. Calculate the earliest fill date based on the previous fill date and days supply.
> - **22 (M/I Dispense as Written Code)** -- Check the DAW code on the prescription and ensure it matches facility and plan requirements.

#### Insurers Tab

The Insurers tab displays the PBM and insurer configurations used for claims processing.

- **Insurer Name** -- PBM or insurance company name
- **BIN** -- Bank Identification Number
- **PCN** -- Processor Control Number
- **Phone** -- PBM help desk phone number for claim inquiries
- **Status** -- Configuration status (Active, Inactive)

### Key Functions

#### Process Payments

Accept copay payments at the pharmacy window.

1. After a claim is adjudicated (Paid status), the patient copay amount is determined.
2. Present the copay amount to the patient.
3. Accept payment (cash, credit card, debit card, check, as supported by the facility).
4. Record the payment in the POS system.
5. Generate a receipt for the patient.

#### Receipt Generation

Generate payment receipts for patients.

1. Select the transaction or prescription.
2. Click **Print Receipt**.
3. The receipt includes:
   - Patient name
   - Prescription number
   - Drug name and quantity
   - Total cost, insurance payment, and patient copay
   - Payment method and amount
   - Date and time
   - Pharmacy contact information

#### Transaction History

View transaction history by patient or date range.

1. Filter by **Patient ID** to see all claims for a specific patient.
2. Filter by **Date Range** to see all claims within a time period.
3. Export transaction data if needed for reconciliation or reporting.

#### Refunds

Process copay refunds when a prescription is returned or a billing correction results in a lower copay.

1. Locate the original transaction in the Transaction History.
2. Click **Refund**.
3. Enter the refund amount and reason.
4. Process the refund to the original payment method.
5. A reversal claim (B2 transaction) is submitted to the insurer/PBM if applicable.

> **Tip:** When a claim is rejected, review the rejection reason carefully before resubmitting. Many rejections can be resolved by correcting a data entry error (wrong BIN, wrong member ID, wrong DAW code) without contacting the insurer.

---

## Common Billing Scenarios

### New Prescription with Insurance

1. Verify patient benefits (Patient Benefits tab).
2. Process the prescription through the standard dispensing workflow.
3. Submit a B1 (Billing) claim with the patient's insurance information.
4. Review the adjudication response.
5. Collect the patient copay.
6. Dispense the medication.

### Rejected Claim Resolution

1. Review the rejection code and reason.
2. Determine the appropriate resolution:
   - **Data entry error** -- Correct and resubmit (B3 Rebill).
   - **Prior authorization required** -- Submit PA through Pharmacy Benefits.
   - **Non-covered drug** -- Discuss alternatives with prescriber or submit coverage determination.
   - **Refill too soon** -- Inform the patient of the earliest eligible fill date.
3. Resubmit or take corrective action as appropriate.

### Insurance Change

1. When a patient reports a change in insurance, update their benefit information in the Patient Benefits tab.
2. Reverse any claims submitted under the old insurance (B2 Reversal).
3. Resubmit claims under the new insurance (B1 Billing).
4. Collect any copay difference.
