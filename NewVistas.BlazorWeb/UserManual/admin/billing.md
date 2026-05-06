# Billing and Finance

This module covers all revenue cycle and financial management functions within NewVistas, from copay assessment and insurance billing through procurement and accounts receivable. These modules implement the full VistA Integrated Billing, Accounts Receivable, and IFCAP financial workflows for healthcare facility operations.

**Intended Audience:** Revenue Cycle Staff, Billing Specialists, Fiscal Officers, Agent Cashiers, Fee Basis Coordinators, Procurement Officers, and C&P Exam Coordinators.

**VistA File References:** File #350 (Integrated Billing), File #430 (Accounts Receivable), File #161 (Fee Basis), File #442 (IFCAP/Procurement).

**Primary Routes:** `/integrated-billing`, `/accounts-receivable`, `/agent-cashier`, `/edi-billing`, `/fee-basis`, `/ifcap`, `/drg`, `/compensation-pension`.

---

## Integrated Billing (/integrated-billing)

The Integrated Billing (IB) module is the starting point for all patient billing activity. It manages copay accounts, billing actions, insurance verification, and inpatient billing clock tracking.

### Tab 1: Copay Account

The Copay Account tab displays the patient's copayment status and exemption information.

| Field | Description |
|-------|-------------|
| Patient ID | The patient whose copay account is displayed. |
| Copay Status | EXEMPT, REQUIRED_REDUCED, or REQUIRED_FULL. |
| Exemption Reason | If exempt, the specific reason code. |
| Current Balance | Total outstanding copay balance. |
| Last Payment Date | Date of the most recent copay payment received. |

#### Exemption Reason Codes

| Code | Description |
|------|-------------|
| SC50 | Service-connected disability rated 50% or higher. |
| POW | Former Prisoner of War. |
| MEDAL_OF_HONOR | Medal of Honor recipient. |
| CATASTROPHIC | Catastrophically disabled (Priority Group 4). |
| HARDSHIP | Approved financial hardship determination. |
| ANNUAL_CAP | Patient has reached the annual copay cap for the calendar year. |

> **Note:** Copay exemption status is determined by the patient's service-connected rating, means test result, and special eligibility categories. Changes to these underlying factors automatically update the copay status.

![Integrated Billing copay account showing status, exemption, and balance](screenshots/billing-ib-copay.png)

### Tab 2: Billing Actions

The Billing Actions tab displays all charges generated for the patient with their types and statuses.

| Column | Description |
|--------|-------------|
| Charge ID | System-assigned identifier for the billing action. |
| Date of Service | Date the service was provided. |
| Charge Type | INPATIENT, OUTPATIENT, PHARMACY, PROSTHETICS, or LONG_TERM_CARE. |
| Description | Description of the service or item billed. |
| Amount | Dollar amount of the charge. |
| Status | PENDING, BILLED, PAID, ADJUSTED, CANCELLED, or WRITE_OFF. |
| Insurance | Whether the charge was submitted to third-party insurance. |

Actions available:

- **View Detail** -- Opens the full charge detail including billing codes and insurance submission history.
- **Adjust** -- Apply an adjustment to the charge (requires authorization).
- **Cancel** -- Cancel the charge (requires authorization and documentation of reason).

### Tab 3: Insurance

The Insurance tab manages the patient's insurance policies and coverage verification.

| Field | Description |
|-------|-------------|
| Insurance Company | Name of the insurance carrier. |
| Policy Number | Patient's policy or member ID number. |
| Group Number | Group or plan number. |
| Subscriber | Name of the primary subscriber (patient or other). |
| Effective Date | Date coverage began. |
| Expiration Date | Date coverage ends (if known). |
| Coverage Type | Primary, Secondary, or Tertiary. |
| Verification Status | VERIFIED, UNVERIFIED, EXPIRED, or DENIED. |

To verify insurance coverage:

1. Select the insurance policy to verify.
2. Click **Verify Coverage**.
3. The system checks coverage status and updates the verification status and date.
4. If verification fails, investigate with the insurance carrier and update manually.

> **Tip:** Verify insurance coverage at every visit. Coverage can change without notice, and billing claims submitted to expired or incorrect insurance will be rejected.

### Tab 4: Billing Clock

The Billing Clock tab tracks the inpatient billing period for VERA (Veterans Equitable Resource Allocation) reporting. Each inpatient stay has a billing clock that tracks the admission date, current day count, and billing period boundaries.

| Field | Description |
|-------|-------------|
| Admission Date | Start of the current inpatient stay. |
| Current Day | Number of days since admission. |
| Billing Period | Current VERA billing period (based on length of stay thresholds). |
| Expected Discharge | Estimated discharge date based on DRG average length of stay. |

---

## Accounts Receivable (/accounts-receivable)

The Accounts Receivable (AR) module manages all debts owed to the facility, including patient copays, third-party insurance receivables, fee basis obligations, and vendor accounts.

### Tab 1: Debtors

The Debtors tab provides a summary of all entities with outstanding balances.

| Column | Description |
|--------|-------------|
| Debtor Name | Patient name, insurance company, or vendor. |
| Debtor Type | PATIENT, INSURANCE, VENDOR, or GOVERNMENT. |
| Total Charged | Total amount billed. |
| Total Paid | Total payments received. |
| Balance | Outstanding amount owed. |
| Oldest Charge | Age of the oldest unpaid charge (for delinquency tracking). |
| Delinquency Status | CURRENT, 30_DAY, 60_DAY, 90_DAY, 120_PLUS. |

![Accounts Receivable debtor summary showing balances and delinquency](screenshots/billing-ar-debtor.png)

### Tab 2: Accounts

The Accounts tab manages individual AR accounts by type.

| Account Type | Description |
|--------------|-------------|
| COPAY | Patient copayment obligations for healthcare services. |
| FEE_BASIS | Amounts owed by or to community care providers. |
| THIRD_PARTY | Insurance company receivables for billed claims. |
| VENDOR | Amounts owed to suppliers and contractors. |
| OTHER | Miscellaneous receivables not covered by the above categories. |

Each account shows the full transaction history, current balance, and available actions.

### Tab 3: Transactions

The Transactions tab logs all financial transactions against AR accounts.

| Transaction Type | Description |
|-----------------|-------------|
| Payment | Payment received (reduces balance). |
| Adjustment | Administrative adjustment to a charge amount. |
| Waiver | Partial or full waiver of an amount owed (requires approval authority). |
| Interest | Interest charge applied to delinquent accounts per federal guidelines. |
| Administrative Cost | Administrative cost added to delinquent accounts. |
| Penalty | Late payment penalty. |
| Refund | Overpayment refund issued to the debtor. |

### Tab 4: Batch Payments

The Batch Payments tab allows processing of multiple payments in a single operation.

1. Click **New Batch**.
2. Enter the batch description and total expected amount.
3. Add individual payment entries (debtor, account, amount, payment method, reference number).
4. Verify that the total of individual payments matches the batch total.
5. Click **Process Batch** to apply all payments atomically.
6. If any payment in the batch fails validation, the entire batch is rolled back and an error report is generated.

> **Note:** Batch payments are processed atomically -- either all payments in the batch succeed or none do. This prevents partial application that could create reconciliation issues.

### Treasury Offset Program (TOP)

For delinquent debts that meet federal thresholds, the AR module supports referral to the Treasury Offset Program (TOP). TOP allows the federal government to offset the debtor's federal payments (tax refunds, federal salary, etc.) to recover the debt.

> **Warning:** Treasury Offset Program referral has serious consequences for the debtor, including offset of federal tax refunds and salary. TOP referral requires that all due process steps have been completed, including written notification, opportunity to dispute, and establishment of a repayment plan opportunity. Ensure all procedural requirements are met before referring a debt to TOP.

---

## Agent Cashier (/agent-cashier)

The Agent Cashier module manages point-of-service payment collection and cashier session management.

### Tab 1: Receipts

The Receipts tab handles issuing and voiding receipts for payments collected at the cashier window.

![Agent Cashier receipt showing payment details and receipt number](screenshots/billing-agent-cashier.png)

#### Issuing a Receipt

1. Enter the patient ID or debtor information.
2. Select the account to which the payment applies.
3. Enter the payment amount.
4. Select the payment method:

| Payment Method | Description |
|----------------|-------------|
| CASH | Currency and coin. |
| CHECK | Personal or cashier's check (record check number). |
| CREDIT_CARD | Credit or debit card (record last 4 digits and authorization code). |
| MONEY_ORDER | Money order (record serial number). |

5. Click **Issue Receipt**.
6. Print two copies of the receipt -- one for the patient and one for the cashier's records.

#### Voiding a Receipt

1. Locate the receipt by receipt number or patient ID.
2. Click **Void**.
3. Enter the reason for the void.
4. Click **Confirm Void**.
5. The payment is reversed and the account balance is restored.

> **Warning:** Voided receipts must be documented with a reason and are subject to supervisory review. All voids are logged in the audit trail.

### Tab 2: Sessions

The Sessions tab manages cashier drawer sessions.

#### Opening a Session

1. Click **Open Session**.
2. Enter the opening balance (cash in drawer at start of shift).
3. Confirm the opening balance.
4. The session is now active and all payments collected will be associated with this session.

#### Closing a Session

1. Click **Close Session**.
2. Count the cash, checks, and credit card receipts in the drawer.
3. Enter the closing balance for each payment method.
4. The system calculates any over/short variance.
5. Submit the session close.

#### Turn-In

1. After the session is closed, click **Turn-In**.
2. Verify the turn-in amounts match the session close totals.
3. Print the turn-in report.
4. Deliver the funds and report to the fiscal department per local procedure.

---

## EDI Billing (/edi-billing)

The Electronic Data Interchange (EDI) Billing module manages electronic claims submission and remittance processing using ANSI X12 transaction sets.

### Tab 1: Claims

The Claims tab manages X12 837 Professional and Institutional claims.

| Column | Description |
|--------|-------------|
| Claim Number | System-assigned claim identifier. |
| Patient | Patient name and ID. |
| Insurance | Insurance carrier and policy number. |
| Date of Service | Service date range. |
| Amount | Total billed amount. |
| Status | Current claim status (see workflow below). |

#### Claim Status Workflow

```
Draft → Ready → Transmitted → Accepted → Paid
                            → Rejected (→ Corrected → Ready)
```

| Status | Description |
|--------|-------------|
| Draft | Claim created but not yet validated for transmission. |
| Ready | Claim validated and queued for the next transmission batch. |
| Transmitted | Claim sent to the clearinghouse or payer. |
| Accepted | Claim acknowledged as received by the payer (997 acknowledgment). |
| Rejected | Claim rejected by the payer. Reason code provided. Must be corrected and resubmitted. |
| Paid | Payment received and matched to the claim via ERA (835). |

![EDI Billing claims list showing status workflow](screenshots/billing-edi-claims.png)

### Tab 2: Transmissions

The Transmissions tab manages batch claim transmissions and X12 997 Functional Acknowledgment tracking.

| Column | Description |
|--------|-------------|
| Batch ID | Identifier for the transmission batch. |
| Transmission Date | Date and time the batch was transmitted. |
| Claim Count | Number of claims in the batch. |
| Total Amount | Sum of all claim amounts in the batch. |
| 997 Status | PENDING, ACCEPTED, or REJECTED (functional acknowledgment from the receiver). |

### Tab 3: ERA (Electronic Remittance Advice)

The ERA tab processes X12 835 remittance advices received from payers, containing payment and adjustment information.

| Column | Description |
|--------|-------------|
| ERA ID | Identifier for the remittance advice. |
| Payer | Insurance company that sent the remittance. |
| Check/EFT Number | Payment reference number. |
| Payment Amount | Total payment amount. |
| Claims Matched | Number of claims matched to payments in this ERA. |
| Unmatched | Number of payment lines not yet matched to claims. |
| Status | RECEIVED, RECONCILED, or PARTIALLY_RECONCILED. |

Reconciliation steps:

1. Import the ERA file.
2. The system automatically matches payments to open claims.
3. Review unmatched items and manually match or investigate discrepancies.
4. Post payments to the corresponding AR accounts.
5. Mark the ERA as RECONCILED.

### Payment Chain

The full electronic billing payment chain flows:

```
Integrated Billing (IB) → EDI Claims (837) → Payer → ERA (835) → Accounts Receivable (AR)
```

Each step is tracked and auditable within the respective module.

---

## Fee Basis (/fee-basis)

The Fee Basis module manages community care -- healthcare services provided to VA patients by non-VA providers under VA authorization.

### Tab 1: Patient Fee Records

Summary of all fee basis activity for a patient, including authorized services, claims processed, and total expenditures.

### Tab 2: Vendors

Community care provider directory.

| Field | Description |
|-------|-------------|
| Vendor Name | Name of the community provider or facility. |
| Vendor ID | System-assigned identifier. |
| Tax ID | Vendor's tax identification number. |
| Specialty | Medical specialty or service type. |
| Address | Physical address of the vendor. |
| Phone | Contact phone number. |
| Status | ACTIVE, INACTIVE, or SUSPENDED. |
| Contract | Associated contract or agreement (if applicable). |

### Tab 3: Authorizations

Authorizations approve specific community care services for a patient with a defined scope, provider, and time frame.

#### Authorization Status Workflow

```
DRAFT → SUBMITTED → APPROVED → COMPLETED
                  → DENIED
      → CANCELLED
```

| Status | Description |
|--------|-------------|
| DRAFT | Authorization created but not yet submitted for approval. |
| SUBMITTED | Authorization submitted for clinical and fiscal review. |
| APPROVED | Authorization approved. Services may proceed. |
| DENIED | Authorization denied. Reason documented. |
| COMPLETED | All authorized services have been provided and paid. |
| CANCELLED | Authorization cancelled before services were rendered. |

![Fee Basis authorization showing service details and approval status](screenshots/billing-fee-basis.png)

#### Creating an Authorization

1. Open the Authorizations tab and click **New Authorization**.
2. Enter the patient ID and select the community care vendor.
3. Specify the authorized services (procedure codes, number of visits, date range).
4. Enter the estimated cost.
5. Add clinical justification for the community care referral.
6. Submit the authorization for review.

> **Warning:** Insurance verification is required before processing fee basis authorizations. Verify that the patient's insurance has been checked and that VA is the appropriate payer before authorizing community care services.

### Tab 4: Invoices

Invoices received from community care vendors for authorized services.

| Field | Description |
|-------|-------------|
| Invoice Number | Vendor's invoice reference. |
| Authorization | Linked authorization number. |
| Service Date | Date the service was provided. |
| Amount | Invoiced amount. |
| Status | RECEIVED, VERIFIED, APPROVED, PAID, DISPUTED. |

### Tab 5: Batch Payments

Process multiple vendor invoices in a single payment batch. Similar to the AR batch payment process but specifically for fee basis vendor payments.

---

## IFCAP (/ifcap)

The Integrated Funds Distribution, Control Point Activity, Accounting and Procurement (IFCAP) module manages the facility's procurement and financial control operations.

### Tab 1: Control Points

Control points are budget allocation units that track obligated and expended funds for specific programs or services.

| Field | Description |
|-------|-------------|
| Control Point Number | Unique identifier for the control point. |
| Name | Descriptive name (e.g., "Radiology Supplies", "Pharmacy Operating"). |
| Fund | Appropriation fund code. |
| Budget Amount | Total allocated budget for the fiscal year. |
| Obligated | Funds committed to approved purchase orders. |
| Expended | Funds actually disbursed for received goods/services. |
| Available Balance | Budget Amount minus Obligated. |
| Status | ACTIVE, FROZEN, or CLOSED. |

| Status | Description |
|--------|-------------|
| ACTIVE | Control point is operational and can accept new obligations. |
| FROZEN | Control point is temporarily locked. No new obligations allowed until unfrozen. |
| CLOSED | Control point is closed for the fiscal year. No further activity allowed. |

> **Warning:** Never over-obligate a control point. Obligating funds beyond the available balance violates the Anti-Deficiency Act and can result in serious administrative and legal consequences. The system will prevent over-obligation, but always verify the available balance before submitting a purchase request.

![IFCAP control point showing budget, obligations, and available balance](screenshots/billing-ifcap-control-point.png)

### Tab 2: Purchase Requests

Purchase requests initiate the procurement process by identifying a need and requesting funds.

#### Purchase Request Status Workflow

```
DRAFT → SUBMITTED → APPROVED → OBLIGATED
                  → RETURNED (for corrections)
      → CANCELLED
```

| Status | Description |
|--------|-------------|
| DRAFT | Request created but not yet submitted. |
| SUBMITTED | Request submitted for approval. |
| APPROVED | Request approved by the control point official. |
| OBLIGATED | Funds committed against the control point. |
| RETURNED | Request returned for corrections or additional information. |
| CANCELLED | Request cancelled. |

### Tab 3: Purchase Orders

Purchase orders are created from approved and obligated purchase requests.

#### Purchase Order Status Workflow

```
CREATED → SENT → RECEIVED → CLOSED
                           → PARTIAL_RECEIPT (→ RECEIVED → CLOSED)
       → CANCELLED
```

| Status | Description |
|--------|-------------|
| CREATED | Purchase order generated from the approved purchase request. |
| SENT | Purchase order transmitted to the vendor. |
| RECEIVED | All items on the purchase order have been received and inspected. |
| PARTIAL_RECEIPT | Some but not all items received. Awaiting remaining delivery. |
| CLOSED | Purchase order completed. All items received and payment processed. |
| CANCELLED | Purchase order cancelled before completion. |

### Tab 4: Receiving Reports

Receiving reports document the receipt and inspection of goods ordered on a purchase order.

1. Locate the purchase order for the incoming delivery.
2. Click **Receive** to open the receiving report form.
3. Record the quantity received for each line item.
4. Note any discrepancies (damaged goods, short shipment, wrong items).
5. Submit the receiving report.
6. The purchase order status updates to RECEIVED or PARTIAL_RECEIPT as appropriate.

### Tab 5: Vendors

Procurement vendor directory with contract information, past performance, and contact details.

### Two-Stage Accounting

IFCAP uses a two-stage accounting model:

1. **Obligation** -- When a purchase request is approved, funds are obligated (committed) against the control point. This reduces the available balance but does not move money.
2. **Expenditure** -- When goods are received and the invoice is processed, the obligated funds become expended. Money is actually disbursed to the vendor.

This two-stage approach ensures that committed funds are tracked separately from actual spending, providing accurate budget visibility.

---

## DRG Grouper (/drg)

The DRG (Diagnosis Related Group) Grouper module calculates the DRG assignment for an inpatient encounter based on diagnoses, procedures, and patient demographics.

### Input Fields

| Field | Description |
|-------|-------------|
| Principal Diagnosis | Primary ICD-10-CM code for the admission. |
| Secondary Diagnoses | Additional ICD-10-CM codes (comorbidities and complications). |
| Procedures | ICD-10-PCS procedure codes performed during the stay. |
| Age | Patient's age at admission. |
| Sex | Patient's sex. |
| Discharge Status | Disposition at discharge (home, SNF, expired, etc.). |

### Calculating a DRG

1. Enter the principal diagnosis ICD-10-CM code.
2. Add secondary diagnoses (include all relevant comorbidities and complications).
3. Enter any procedure codes.
4. Enter or confirm patient demographics (age, sex).
5. Select the discharge status.
6. Click **Calculate DRG**.

### Output

| Field | Description |
|-------|-------------|
| DRG Code | The assigned DRG number. |
| DRG Description | Text description of the DRG. |
| MDC | Major Diagnostic Category that groups the DRG. |
| Relative Weight | The DRG relative weight, which determines reimbursement compared to the national average. |
| Mean LOS | Average length of stay for patients in this DRG nationally. |
| Geometric Mean LOS | Geometric mean length of stay (used for outlier calculations). |
| Expected Reimbursement | Estimated reimbursement based on relative weight and the facility's base rate. |
| With/Without CC/MCC | Whether the DRG is the complication/comorbidity (CC) or major CC (MCC) version. |

![DRG Grouper result showing DRG assignment, relative weight, and expected reimbursement](screenshots/billing-drg-result.png)

> **Tip:** Accurate coding of secondary diagnoses (especially CCs and MCCs) significantly impacts DRG assignment and reimbursement. Work closely with coding specialists to ensure completeness.

---

## Compensation and Pension (/compensation-pension)

The Compensation and Pension (C&P) module tracks disability examination requests from the Veterans Benefits Administration (VBA) and manages Disability Benefits Questionnaire (DBQ) completion.

### Tab 1: Exams

C&P examination requests received from VBA.

#### Exam Status Workflow

```
REQUESTED → SCHEDULED → COMPLETED → SUBMITTED_TO_VBA
                                   → ADDENDUM_REQUESTED (→ COMPLETED → SUBMITTED_TO_VBA)
         → CANCELLED
```

| Status | Description |
|--------|-------------|
| REQUESTED | Exam request received from VBA. Not yet scheduled. |
| SCHEDULED | Exam appointment booked with the examining provider. |
| COMPLETED | Exam performed and report or DBQ completed by the examiner. |
| SUBMITTED_TO_VBA | Completed exam results sent to VBA for rating decision. |
| ADDENDUM_REQUESTED | VBA requested additional information or clarification. |
| CANCELLED | Exam cancelled (patient no-show, request withdrawn, etc.). |

### Tab 2: DBQ (Disability Benefits Questionnaires)

DBQs are standardized forms used by examiners to document findings for specific disabilities claimed by the veteran.

#### DBQ Status Workflow

```
DRAFT → SIGNED → SUBMITTED
```

| Status | Description |
|--------|-------------|
| DRAFT | DBQ started by the examiner but not yet finalized. |
| SIGNED | DBQ reviewed and electronically signed by the examiner. |
| SUBMITTED | Signed DBQ transmitted to VBA. |

### Tab 3: Dashboard

The C&P Dashboard provides operational metrics for managing the exam workload.

| Metric | Description |
|--------|-------------|
| Pending Exams | Number of exam requests in REQUESTED status (not yet scheduled). |
| Scheduled This Week | Number of exams scheduled for the current week. |
| Completed This Month | Number of exams completed in the current month. |
| Average Turnaround | Mean days from REQUESTED to SUBMITTED_TO_VBA. |
| Overdue | Number of exams past the VBA-requested completion date. |

> **Note:** VBA imposes timeliness standards for C&P exam completion. Monitor the overdue count closely and prioritize scheduling for aging requests.

---

## Common Workflows

### End-to-End Billing Cycle

1. **Service Delivery** -- Patient receives care (outpatient visit, inpatient stay, pharmacy fill, etc.).
2. **Charge Generation** -- Integrated Billing creates a charge in the Billing Actions tab based on the encounter and coding.
3. **Insurance Check** -- If the patient has third-party insurance, an EDI claim (X12 837) is generated and transmitted.
4. **Copay Assessment** -- Based on the patient's copay status, a copay charge is posted to the patient's AR account.
5. **Payment Processing** -- Payments are received via ERA (insurance), agent cashier (patient), or batch processing. Payments are posted to the AR account.
6. **Account Reconciliation** -- Outstanding balances are reviewed, follow-up actions are taken (rebilling, collections, waivers), and accounts are closed when fully resolved.

### Copay Collection at Point of Service

1. When the patient checks in for their appointment, the scheduling clerk notifies the patient of any outstanding copay balance.
2. The patient proceeds to the agent cashier window to make payment.
3. The agent cashier issues a receipt, and the payment is posted to the patient's AR copay account.

---

## Tips and Best Practices

> **Tip:** Verify insurance coverage at every encounter. Stale insurance information is the leading cause of claim rejections.

> **Tip:** Process ERAs promptly when received. Delayed ERA processing creates reconciliation backlogs and obscures the true AR aging picture.

> **Tip:** When voiding an agent cashier receipt, always document the specific reason. Voids without documented justification will be flagged during fiscal audit.

> **Tip:** Review the IFCAP control point available balance before submitting purchase requests. Submitting requests against insufficient funds delays the procurement process.

> **Tip:** For fee basis authorizations, include detailed clinical justification. Authorizations with vague justification are more likely to be returned or denied during review.

> **Tip:** Use the DRG Grouper proactively during inpatient stays to estimate reimbursement and identify documentation opportunities (missing CCs/MCCs) before discharge coding is finalized.

> **Tip:** Schedule C&P exams as early as possible after receiving the VBA request. Delayed scheduling is the primary driver of overdue exams and VBA timeliness failures.

---

## Screenshots Reference

The following screenshots are referenced throughout this section:

- ![Integrated Billing copay account](screenshots/billing-ib-copay.png)
- ![Accounts Receivable debtor summary](screenshots/billing-ar-debtor.png)
- ![Agent Cashier receipt](screenshots/billing-agent-cashier.png)
- ![EDI Billing claims list](screenshots/billing-edi-claims.png)
- ![Fee Basis authorization](screenshots/billing-fee-basis.png)
- ![IFCAP control point with budget details](screenshots/billing-ifcap-control-point.png)
- ![DRG Grouper result](screenshots/billing-drg-result.png)
