# Health Information Management

This module covers the full scope of Health Information Management (HIM) functions within NewVistas, including release of information, record tracking, incomplete records management, audit trail review, ICD-10 coding support, DRG grouping, health summaries, patient record merging, and patient record security. These functions map to the VistA HIM package and associated regulatory requirements for healthcare information governance.

**Intended Audience:** HIM Professionals, Medical Records Technicians, Coding Specialists, Release of Information (ROI) Staff, Privacy Officers, and Information Security Officers.

**VistA File References:** File #195 (Release of Information), File #190 (Record Tracking), File #80 (ICD-10 Diagnosis), File #8925 (TIU Document).

**Primary Routes:** `/release-of-information`, `/record-tracking`, `/incomplete-records`, `/audit-trail`, `/icd10`, `/drg`, `/health-summary`, `/patient-merge`, `/security`.

---

## Release of Information (/release-of-information)

The Release of Information (ROI) module manages requests for patient health information from patients, authorized representatives, attorneys, other facilities, and government agencies. All ROI activity must comply with HIPAA, the Privacy Act, and 38 U.S.C. 7332 (special categories of VA health information).

### Tab 1: Record Requests

The Record Requests tab tracks all incoming requests for patient health information through their lifecycle.

| Column | Description |
|--------|-------------|
| Request ID | System-assigned identifier. |
| Patient | Patient whose records are requested. |
| Requestor | Name and organization of the person or entity requesting records. |
| Request Date | Date the request was received. |
| Authorization Type | Patient authorization, subpoena, court order, or statutory exception. |
| Records Requested | Types of records requested (progress notes, lab results, imaging, entire record, etc.). |
| Status | Current processing status (see workflow below). |
| Due Date | HIPAA-mandated response deadline. |
| Assigned To | Staff member processing the request. |

#### Request Status Workflow

```
Received → Acknowledged → InProcess → PendingAuthorization → Fulfilled
                                                            → Denied
```

| Status | Description |
|--------|-------------|
| Received | Request received and logged in the system. |
| Acknowledged | Receipt acknowledged to the requestor. |
| InProcess | Records are being gathered and reviewed for release. |
| PendingAuthorization | Additional authorization required (e.g., 38 U.S.C. 7332 protected information needs specific consent). |
| Fulfilled | Records have been released to the requestor. |
| Denied | Request denied with documented reason (invalid authorization, records not found, etc.). |

![Release of Information request list showing status and due dates](screenshots/him-roi-requests.png)

### Processing a Record Request

1. When a new request arrives, create the request entry in the system with all required fields (requestor, authorization, records requested).
2. Review the authorization for validity -- confirm the patient signature, date, scope of records authorized, and expiration date.
3. Identify and gather the requested records from the appropriate sources (TIU documents, lab results, imaging reports, etc.).
4. Review the gathered records for information that requires additional authorization under 38 U.S.C. 7332 (HIV, substance abuse, sickle cell anemia, mental health in some contexts).
5. If 7332-protected information is present and not specifically authorized, redact it or set the request to PendingAuthorization and contact the patient for specific consent.
6. Release the records to the requestor via the approved delivery method (mail, secure electronic transfer, in-person pickup).

> **Warning:** HIPAA requires that ROI requests be responded to within 30 calendar days of receipt, with one 30-day extension permitted if documented. Monitor the Due Date column closely and escalate requests approaching the deadline.

### Tab 2: HIPAA Disclosures

The HIPAA Disclosures tab maintains a log of all disclosures of protected health information (PHI) made by the facility, including the purpose, recipient, and scope.

| Column | Description |
|--------|-------------|
| Disclosure Date | Date the disclosure was made. |
| Patient | Patient whose information was disclosed. |
| Recipient | Person or entity that received the information. |
| Purpose | Reason for disclosure (Treatment, Payment, Operations, Required by Law, Research, etc.). |
| Records Disclosed | Description of the information disclosed. |
| Method | How the information was transmitted (mail, fax, electronic, verbal). |
| Staff | Staff member who performed the disclosure. |

### Tab 3: Accounting of Disclosures

The Accounting of Disclosures tab generates the report required by HIPAA that patients may request, showing all disclosures of their PHI during a specified period.

Key rules:

- The accounting covers a 6-year lookback period.
- Disclosures for Treatment, Payment, and Operations (TPO) are **excluded** from the accounting.
- Disclosures required by law, for public health, and other non-TPO purposes are **included**.

To generate an accounting:

1. Enter the patient ID and the date range (up to 6 years back).
2. Click **Generate Report**.
3. Review the report for completeness.
4. Provide the report to the patient or their authorized representative.

> **Note:** Patients have the right to an accounting of disclosures under HIPAA. The first request in any 12-month period must be provided at no charge.

### Tab 4: Dashboard

The ROI Dashboard provides operational metrics for managing the ROI workload.

| Metric | Description |
|--------|-------------|
| Open Requests | Number of requests not yet fulfilled or denied. |
| Overdue | Number of requests past the 30-day HIPAA deadline. |
| Average Turnaround | Mean days from Received to Fulfilled. |
| Requests This Month | Total requests received in the current month. |
| Fulfillment Rate | Percentage of requests fulfilled versus denied. |

---

## Record Tracking (/record-tracking)

The Record Tracking module manages the physical location and movement of paper medical records (charts) within the facility.

### Tab 1: Charts

The Charts tab displays the current location and status of all tracked charts.

| Column | Description |
|--------|-------------|
| Chart ID | Identifier for the chart (usually the patient ID). |
| Patient Name | Patient whose chart this is. |
| Current Location | Where the chart is currently located (clinic name, file room, provider office, etc.). |
| Status | Current chart status. |
| Checked Out By | Staff member who currently has the chart (if checked out). |
| Checkout Date | Date the chart was checked out. |
| Days Out | Number of days since checkout (for overdue tracking). |

#### Chart Status Values

| Status | Description |
|--------|-------------|
| IN_FILE | Chart is in the medical records file room at its designated location. |
| CHECKED_OUT | Chart has been checked out to a clinic, provider, or other location. |
| OVERDUE | Chart was checked out and has exceeded the maximum checkout period without return. |
| RETIRED | Chart has been retired to offsite storage or transferred to the National Archives. |
| LOST | Chart cannot be located after search efforts. |

> **Warning:** Charts in LOST status represent a potential HIPAA breach and must be escalated to the Privacy Officer immediately. Missing charts require a documented search effort and may trigger a breach notification assessment.

### Tab 2: Requests

The Requests tab manages chart pull requests -- requests from clinics, providers, or other departments to have a chart pulled from the file room and delivered.

| Column | Description |
|--------|-------------|
| Request ID | System-assigned identifier. |
| Patient/Chart | The chart being requested. |
| Requestor | Person or clinic requesting the chart. |
| Request Date | Date the request was submitted. |
| Priority | ROUTINE, URGENT, or STAT. |
| Status | PENDING, PULLED, DELIVERED, RETURNED. |
| Needed By | Date the chart is needed. |

Processing chart pull requests:

1. Review incoming requests, sorting by priority (STAT first).
2. Locate the chart in the file room.
3. Mark the request as PULLED and record the checkout.
4. Deliver the chart to the requesting location.
5. Mark the request as DELIVERED.
6. When the chart is returned, mark as RETURNED and check the chart back in.

### Tab 3: Status Dashboard

The Status Dashboard provides aggregate metrics for chart management.

| Metric | Description |
|--------|-------------|
| Total Charts | Number of charts tracked in the system. |
| In File | Charts currently in the file room. |
| Checked Out | Charts currently checked out. |
| Overdue | Charts past the maximum checkout period. |
| Missing/Lost | Charts in LOST status. |
| Most Active Locations | Locations that most frequently request charts. |

---

## Incomplete Records (/incomplete-records)

The Incomplete Records module tracks medical record deficiencies -- documentation that is required but has not yet been completed by the responsible provider.

### Deficiency Types

| Deficiency Type | Description |
|-----------------|-------------|
| Unsigned Note | Progress note or consult result that requires the provider's electronic signature. |
| Unsigned Order | Order that requires cosignature or attending signature. |
| Missing H&P | History and Physical examination not documented within the required timeframe. |
| Missing Operative Report | Operative report not dictated within the required timeframe after surgery. |
| Missing Discharge Summary | Discharge summary not completed within the required timeframe after patient discharge. |
| Missing Consult Result | Consult response not documented. |
| Addendum Required | Additional documentation requested by a supervisor or reviewer. |

### Filtering and Management

The incomplete records list can be filtered by:

- **Deficiency Type** -- Show only specific types of deficiencies.
- **Provider** -- Show deficiencies for a specific provider.
- **Date Range** -- Filter by the date the deficiency was identified.
- **Ward/Clinic** -- Filter by the location where the encounter occurred.

Actions available:

- **Notify Provider** -- Send an automated notification to the provider about their outstanding deficiencies.
- **Suspend Privileges** -- For chronic non-compliance, initiate a privilege suspension action (requires Medical Staff Office authorization).

### Age Tracking

Deficiency age is tracked from the date the deficiency was identified and is color-coded for urgency:

| Age | Color | Significance |
|-----|-------|-------------|
| Less than 7 days | Normal (no highlight) | Within acceptable timeframe. Provider should complete soon. |
| 7 to 30 days | Yellow | Approaching compliance threshold. Provider notification recommended. |
| Greater than 30 days | Red | Compliance risk. The Joint Commission and VHA require timely completion. Escalation and potential privilege suspension may be warranted. |

### Aging Report

The aging report provides a summary of all open deficiencies grouped by provider and age bracket. This report is used by the Medical Staff Office and HIM leadership for compliance monitoring and credentialing reviews.

To generate the aging report:

1. Navigate to the Incomplete Records page.
2. Click **Aging Report**.
3. Select the date range and optional filters (provider, deficiency type).
4. Click **Generate**.
5. Review and export the report for distribution.

> **Tip:** Run the aging report weekly and distribute it to department chiefs. Provider awareness of their deficiency counts is the most effective tool for timely completion.

---

## Audit Trail (/audit-trail)

The Audit Trail module provides a comprehensive log of all system access and data modification events. It is an essential tool for privacy investigations, compliance reviews, and security incident response.

### Filters

The audit trail supports the following search filters:

| Filter | Description |
|--------|-------------|
| Domain | The system domain or module (Patient, Order, Lab, Pharmacy, TIU Document, etc.). |
| Date Range | Start and end dates for the search. |
| Entity ID | The specific record identifier (Patient ID, Order ID, Document ID, etc.). |
| User | The user who performed the action. |
| Action Type | Read, Create, Update, or Delete. |
| IP Address | The IP address from which the action was performed. |

### Event Log

The event log displays matching audit entries.

| Column | Description |
|--------|-------------|
| Timestamp | Exact date and time of the event. |
| User | The user who performed the action (username and display name). |
| Domain | The system module or data type accessed. |
| Entity ID | The specific record accessed or modified. |
| Action | Read, Create, Update, or Delete. |
| IP Address | Source IP address of the session. |
| Details | Additional context about the action (fields changed, values before and after, etc.). |

![Audit trail showing filtered results with timestamps, users, and actions](screenshots/him-audit-trail.png)

### Export

Audit trail results can be exported for offline analysis or for submission to oversight bodies.

1. Apply the desired filters.
2. Click **Export**.
3. Select the export format (CSV or PDF).
4. The export file is generated and downloaded.

> **Note:** Regular audit trail review is required by VHA Directive 6500 and The Joint Commission standards. Privacy Officers should conduct proactive audits on sensitive patient records, VIP patients, employee patients, and records involved in incident reports.

---

## ICD-10 Browser (/icd10)

The ICD-10 Browser provides a searchable interface for the International Classification of Diseases, 10th Revision (ICD-10-CM for diagnoses, ICD-10-PCS for procedures).

### Search Methods

| Method | Description |
|--------|-------------|
| Keyword Search | Search by disease name, body system, or clinical term (e.g., "diabetes", "fracture femur"). |
| Code Search | Search by ICD-10 code or partial code (e.g., "E11", "S72.001"). |
| Chapter Browse | Navigate the hierarchical chapter structure. |
| Category Browse | Browse within a specific category for subcategories and billable codes. |

### Code Detail View

When a code is selected, the detail view displays:

| Field | Description |
|-------|-------------|
| Code | The ICD-10-CM or ICD-10-PCS code. |
| Description | Full text description of the code. |
| Billable | Whether the code is at the billable specificity level (leaf node). Only billable codes can be used on claims. |
| Chapter | The ICD-10 chapter containing this code. |
| Block | The block within the chapter. |
| Category | The 3-character category. |
| Includes | Conditions included in this code's scope. |
| Excludes1 | Codes that cannot be used together with this code (mutually exclusive). |
| Excludes2 | Codes that are not included here but may be used together if appropriate. |
| Code First / Use Additional Code | Sequencing instructions for etiology/manifestation coding. |

### Hierarchical Navigation

ICD-10 codes are organized in a hierarchy:

```
Chapter → Block → Category → Subcategory → Billable Code
```

Example:
```
Chapter 4: Endocrine, nutritional and metabolic diseases (E00-E89)
  → Block: Diabetes mellitus (E08-E13)
    → Category: E11 - Type 2 diabetes mellitus
      → Subcategory: E11.3 - Type 2 diabetes mellitus with ophthalmic complications
        → Billable Code: E11.311 - Type 2 diabetes mellitus with unspecified diabetic retinopathy with macular edema
```

![ICD-10 Browser showing search results and code hierarchy](screenshots/him-icd10-browser.png)

> **Tip:** When coding, always code to the highest level of specificity available. Non-billable (non-specific) codes should only be used when the medical record does not contain enough information to assign a more specific code.

---

## DRG Grouper (/drg)

The DRG Grouper calculates the Diagnosis Related Group assignment for inpatient encounters. This is a shared module also documented in [billing.md](billing.md).

### Input Fields

| Field | Description |
|-------|-------------|
| Principal Diagnosis | Primary ICD-10-CM code (the condition chiefly responsible for the admission). |
| Secondary Diagnoses | All additional ICD-10-CM codes (comorbidities, complications, and other relevant conditions). |
| Principal Procedure | Primary ICD-10-PCS procedure code. |
| Secondary Procedures | All additional procedures performed during the stay. |
| Age | Patient's age at admission. |
| Sex | Patient's administrative sex. |
| Discharge Status | Disposition at discharge. |

### Calculating the DRG

1. Enter the principal diagnosis code (required).
2. Add all secondary diagnoses documented in the medical record.
3. Enter all procedure codes.
4. Confirm patient demographics.
5. Click **Calculate**.

### Results

The output includes the DRG code, description, Major Diagnostic Category (MDC), relative weight, mean length of stay, geometric mean length of stay, and the estimated reimbursement based on the facility's base rate.

The result also indicates whether the assignment includes complication/comorbidity (CC) or major complication/comorbidity (MCC) severity levels, which significantly affect the relative weight and reimbursement.

> **Tip:** HIM coders should use the DRG Grouper to verify that documentation supports the assigned DRG. If the medical record supports additional CC/MCC diagnoses that are not coded, work with the clinical team through compliant query processes to capture the documentation.

---

## Health Summary (/health-summary)

The Health Summary module generates consolidated clinical summary reports for a patient, combining data from multiple clinical domains into a single printable or viewable document.

### Tab 1: Summary Types

Predefined summary types that determine which clinical data elements are included in the report.

| Summary Type | Included Data |
|--------------|---------------|
| Brief Summary | Demographics, active problems, current medications, allergies, recent vitals. |
| Comprehensive Summary | All brief summary data plus lab results, radiology reports, consult results, immunizations, health factors. |
| Discharge Summary | Admission/discharge dates, diagnoses, procedures, discharge medications, follow-up instructions. |
| Transfer Summary | Current clinical status, active orders, medications, pending results, care plan. |
| Continuity of Care | Problems, medications, allergies, immunizations, procedures -- formatted for external sharing. |

### Tab 2: Generate Report

1. Enter the patient ID.
2. Select the summary type.
3. Optionally specify a date range to limit the data included.
4. Click **Generate**.
5. Review the generated summary on screen.
6. Print or export as needed.

---

## Patient Merge (/patient-merge)

HIM performs the final verification and execution of patient record merges when duplicate records are identified. The Patient Merge module is shared with Registration and is documented in detail in [registration.md](registration.md).

HIM's role in the merge process:

1. Receive the merge request from Registration or MPI operations.
2. Independently verify that the two records represent the same patient by reviewing demographics, clinical history, and any available identification documents.
3. Confirm that the target (surviving) record has the correct and most complete demographic information.
4. Execute the merge in the Patient Merge module.
5. Document the merge in the HIM merge log for audit purposes.

> **Warning:** Patient merge is an irreversible operation. Once executed, the source record is permanently retired and all data is transferred to the target record. HIM must independently verify the merge before execution -- do not rely solely on the MPI confidence score.

---

## Security (/security)

The Security module manages patient record sensitivity levels and access controls for protected health information.

### Tab 1: Patient Sensitivity

Patient sensitivity levels control who can access a patient's record and what additional logging and notification occurs when the record is accessed.

| Sensitivity Level | Description |
|-------------------|-------------|
| STANDARD | Default level. All authorized users can access the record. Normal audit logging. |
| ELEVATED | Additional access logging. Users receive a sensitivity warning before accessing the record. |
| HIGH | Restricted access. Only providers on the authorized whitelist can access the record. All access attempts are logged and reviewed. |

#### Sensitivity Categories

| Category | Description | Regulatory Basis |
|----------|-------------|-----------------|
| HIV | Patient has HIV-related information in their record. | 38 CFR Part 1 |
| SUBSTANCE_ABUSE | Patient has substance abuse treatment information. | 42 CFR Part 2 |
| BEHAVIORAL | Patient has behavioral health information requiring additional protection. | Facility policy |
| EMPLOYEE | Patient is a facility employee, requiring protection from coworker access. | VHA Directive |

> **Warning:** Federal regulations impose stricter privacy protections for HIV-related records (38 CFR Part 1) and substance abuse treatment records (42 CFR Part 2). These records cannot be disclosed without specific written consent, even for treatment purposes in many cases. Violations carry significant civil and criminal penalties.

![Security sensitivity settings showing levels and categories](screenshots/him-security-sensitivity.png)

### Tab 2: Access Log

The Access Log shows all access events for records with ELEVATED or HIGH sensitivity.

| Column | Description |
|--------|-------------|
| Timestamp | Date and time of the access event. |
| User | Staff member who accessed the record. |
| Patient | Patient whose record was accessed. |
| Sensitivity Level | The sensitivity level at the time of access. |
| Action | Type of access (View, Edit, Print, Export). |
| Justification | Reason provided by the user for accessing the record (required for HIGH sensitivity). |
| Authorized | Whether the user was on the authorized provider list (for HIGH sensitivity). |

### Tab 3: Authorized Providers

The Authorized Providers tab manages the whitelist of providers who are permitted to access HIGH sensitivity records.

| Column | Description |
|--------|-------------|
| Provider Name | Name of the authorized provider. |
| Provider ID | System identifier. |
| Role | Clinical role (Physician, Nurse, Social Worker, etc.). |
| Authorized By | The person who added this provider to the whitelist. |
| Authorization Date | Date the authorization was granted. |
| Expiration Date | Date the authorization expires (if time-limited). |
| Reason | Clinical justification for the access authorization. |

### Setting Up Sensitive Record Protection

1. Navigate to the Security module and select the patient whose record needs protection.
2. On the Patient Sensitivity tab, select the appropriate sensitivity level (ELEVATED or HIGH).
3. Select one or more sensitivity categories (HIV, SUBSTANCE_ABUSE, BEHAVIORAL, EMPLOYEE).
4. If setting HIGH sensitivity, navigate to the Authorized Providers tab and add all providers who need access, including a clinical justification for each.

> **Note:** When a record is set to HIGH sensitivity, all previously authorized users lose access until they are explicitly added to the authorized provider whitelist. Plan the transition carefully to avoid disrupting ongoing patient care.

---

## Common Workflows

### Processing an ROI Request End-to-End

1. Log the incoming request with all required fields (requestor, patient, authorization, records requested).
2. Acknowledge receipt to the requestor within 5 business days.
3. Validate the authorization for completeness and specificity.
4. Gather the requested records from the clinical systems.
5. Review for 38 U.S.C. 7332 protected content and redact or obtain specific consent as needed.
6. Release the records and mark the request as Fulfilled.

### Investigating a Potential Privacy Breach

1. Receive the report of potential unauthorized access.
2. Open the Audit Trail and search by patient ID, date range, and suspected user.
3. Review all access events for the patient's record during the relevant period.
4. For sensitive records, also check the Security module's Access Log for detailed access justifications.
5. Document findings and report to the Privacy Officer for breach assessment.

### Chart Location Audit

1. Generate the Record Tracking Status Dashboard report.
2. Identify all charts in CHECKED_OUT status for more than the maximum checkout period.
3. Contact the location/person shown as the checkout holder for each overdue chart.
4. Update chart status based on results (returned, still in use with extension, or escalate to LOST).

---

## Tips and Best Practices

> **Tip:** Process ROI requests in order of due date, not receipt date. This ensures HIPAA deadlines are met even when request volume fluctuates.

> **Tip:** Run the incomplete records aging report weekly and share it with department chiefs. Timely completion of medical records is a Joint Commission requirement and affects facility accreditation.

> **Tip:** When coding, always reference the ICD-10 Browser's Includes, Excludes1, and Excludes2 notes. These notes prevent common coding errors and improve claim acceptance rates.

> **Tip:** Audit trail reviews should not be limited to reactive investigations. Conduct proactive audits on employee patient records, VIP records, and records of patients involved in high-profile incidents.

> **Tip:** For patient merge operations, always independently verify both records before executing. MPI confidence scores are helpful but are not a substitute for human review of the clinical content.

> **Tip:** When setting up HIGH sensitivity on a patient record, notify the patient's treatment team in advance so they can verify their names are on the authorized provider list before access is restricted.

> **Tip:** Document all ROI denials with specific HIPAA or regulatory citations. Clear documentation of denial reasons protects the facility in case of complaints or legal challenges.

---

## Screenshots Reference

The following screenshots are referenced throughout this section:

- ![ROI request list with status and due dates](screenshots/him-roi-requests.png)
- ![Record tracking chart locations and status](screenshots/him-record-tracking.png)
- ![Incomplete records with aging color indicators](screenshots/him-incomplete-records.png)
- ![Audit trail filtered results](screenshots/him-audit-trail.png)
- ![ICD-10 Browser search and hierarchy](screenshots/him-icd10-browser.png)
- ![Security sensitivity settings and categories](screenshots/him-security-sensitivity.png)
