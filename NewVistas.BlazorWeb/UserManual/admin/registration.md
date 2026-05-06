# Registration and Eligibility

This module covers all aspects of patient registration, eligibility determination, and identity management within NewVistas. It maps to the core VistA registration functions and is the foundation upon which all other clinical and administrative modules depend.

**Intended Audience:** Registration Clerks, Enrollment Clerks, ADT Coordinators, Eligibility Technicians, and MPI Coordinators.

**VistA File References:** File #2 (Patient), File #40.8 (ADT/Transfer), File #44 (Hospital Location/Scheduling).

---

## Patient Lookup (/patient-lookup)

The Patient Lookup page is the primary search interface for locating existing patient records. It should be the first step before any registration activity to prevent creation of duplicate records.

> **Note:** Always search for an existing patient record before creating a new one. Duplicate records create serious clinical safety risks and are difficult to resolve after the fact.

### Search Interface

The search bar at the top of the page accepts multiple search criteria:

- **Patient Name** -- Last name, or Last,First format. Partial matches are supported.
- **Last 4 of SSN** -- The last four digits of the Social Security Number for targeted lookup.
- **Date of Birth** -- In MM/DD/YYYY format to narrow results.
- **Patient ID** -- The system-assigned patient identifier for direct lookup.

To perform a search:

1. Enter one or more search criteria in the search bar.
2. Click **Search** or press Enter.
3. Review the results table below.

### Results Table

The results table displays matching patients with the following columns:

| Column | Description |
|--------|-------------|
| Name | Full name (Last, First Middle) |
| SSN | Masked format (XXX-XX-1234), showing only the last four digits |
| DOB | Date of birth |
| Patient ID | System-assigned unique identifier |
| Status | Active, Inactive, or Deceased |

Click any row to open the patient detail view.

### Detail View

The detail view shows a summary panel with the patient's key demographics, active flags, and enrollment status. From this view you can:

- **Edit Patient** -- Opens the Patient Edit page for this patient.
- **View Registration** -- Opens the full Registration module for this patient.
- **Schedule Appointment** -- Navigates to the Scheduling module with this patient pre-selected.
- **View Chart** -- Opens the clinical chart (requires clinical access keys).

![Patient Lookup results showing search bar, results table, and detail panel](screenshots/patient-lookup-results.png)

### Quick Actions

Quick action buttons appear in the detail view for common follow-up tasks:

- **Print Face Sheet** -- Generates a printable demographics summary.
- **Print Wristband** -- Sends a wristband label to the configured printer.
- **Copy Patient ID** -- Copies the patient ID to the clipboard for use in other modules.

---

## Patient Edit (/patient-edit)

The Patient Edit page provides a comprehensive 5-tab interface for maintaining all patient demographic and military service information. Changes are saved per-tab when you click **Save**.

> **Warning:** Changing a patient's SSN or Date of Birth triggers an automatic Master Patient Index (MPI) review. The change will be held pending until MPI review is completed. Do not make these changes without supervisor authorization.

![Patient Edit demographics tab showing form fields and save button](screenshots/patient-edit-demographics.png)

### Tab 1: Demographics

Core identity fields that are used across all clinical and administrative functions.

| Field | Required | Description |
|-------|----------|-------------|
| Name (Last, First, Middle) | Yes | Legal name as it appears on government-issued identification. |
| Sex | Yes | Administrative sex for registration purposes. |
| Date of Birth | No | Used for age calculations, eligibility, and clinical decision support. |
| Social Security Number | No | Primary identifier for VA benefits and MPI correlation. Displayed masked except during edit. |
| Marital Status | No | Single, Married, Divorced, Widowed, Separated, or Unknown. Affects means test calculations. |
| Religion | No | Recorded for pastoral care and advance directive purposes. |
| Ethnicity | No | Self-reported. Used for population health reporting. |
| Race | No | Self-reported. Multiple selections allowed. |
| Preferred Language | No | Primary language for communication and interpreter services. |

To update demographics:

1. Navigate to the Demographics tab.
2. Modify the desired fields.
3. Click **Save** at the bottom of the tab.
4. Verify the confirmation message appears.

### Tab 2: Address

Mailing and residential address information.

| Field | Description |
|-------|-------------|
| Street Address Line 1 | Primary street address. |
| Street Address Line 2 | Apartment, suite, or unit number. |
| City | City or municipality. |
| State | State or territory (dropdown selection). |
| Zip Code | 5-digit or ZIP+4 format. |
| County | County of residence. Used for catchment area determination. |

> **Tip:** The county field is used to determine the patient's catchment area for facility assignment and travel benefit calculations. Ensure this is accurate.

### Tab 3: Contact

Communication channels and employment information.

| Field | Description |
|-------|-------------|
| Phone (Residence) | Home telephone number. |
| Phone (Work) | Work telephone number. |
| Phone (Cell) | Mobile telephone number. Preferred for appointment reminders. |
| Email Address | Used for secure messaging and appointment notifications. |
| Employer Name | Current employer, if applicable. |
| Employer Phone | Employer contact number. |

### Tab 4: Emergency Contact

Emergency contact information for clinical emergencies and next-of-kin notification.

| Field | Description |
|-------|-------------|
| Contact Name | Full name of emergency contact. |
| Relationship | Relationship to patient (Spouse, Parent, Child, Sibling, Friend, Other). |
| Phone Number | Primary contact number. |
| Street Address | Mailing address of emergency contact. |
| City, State, Zip | Location of emergency contact. |

### Tab 5: Veteran/Military

Military service history and VA-specific benefit indicators.

| Field | Description |
|-------|-------------|
| Veteran (Y/N) | Whether the patient is a veteran of the U.S. Armed Forces. |
| Branch of Service | Army, Navy, Air Force, Marines, Coast Guard, Space Force, or National Guard. |
| Entry Date | Date of entry into active duty. |
| Separation Date | Date of separation from active duty. |
| Service Era | Vietnam, Gulf War, OEF/OIF/OND, Peacetime, etc. |
| Service Connected % | Combined service-connected disability rating (0-100%). |
| POW (Y/N) | Former Prisoner of War status. Affects priority group and copay exemption. |
| Combat Veteran (Y/N) | Combat theater service. Provides 5-year enhanced eligibility. |
| Purple Heart (Y/N) | Purple Heart recipient. Provides priority group 3 enrollment. |
| Agent Orange Exposure (Y/N) | Presumptive exposure to Agent Orange/herbicide agents. |

---

## Registration (/registration)

The Registration module provides a 6-tab interface for managing enrollment, flags, screening, relationships, financial information, and treating facility data. This is the primary workspace for enrollment clerks.

![Registration enrollment tab showing priority group and copay status](screenshots/registration-enrollment.png)

### Tab 1: Enrollment

Manages the patient's VA healthcare enrollment status and priority group assignment.

| Field | Description |
|-------|-------------|
| Enrollment Status | NOT_ENROLLED, PENDING, ENROLLED, REJECTED, CANCELLED. |
| Application Date | Date the enrollment application was received. |
| Priority Group | Groups 1 through 8, assigned based on SC%, income, and special categories. |
| Subgroup | Further classification within the priority group. |
| Copay Exemption Status | Whether the patient is exempt from copayments and the reason. |
| Enrollment Category | Category of enrollment (e.g., Combat Veteran, Purple Heart, Humanitarian). |

**Priority Group Overview:**

| Group | Criteria |
|-------|----------|
| 1 | SC 50% or higher, or unemployable due to SC condition |
| 2 | SC 30-40% |
| 3 | POW, Purple Heart, Medal of Honor, SC 10-20% |
| 4 | Catastrophically disabled |
| 5 | Non-service-connected, low income, pension recipient |
| 6 | Certain war veterans, Agent Orange, ionizing radiation |
| 7 | Non-service-connected, income above threshold, agrees to copay |
| 8 | Non-service-connected, income above threshold (subgroups a-g) |

### Tab 2: PRF Flags

Patient Record Flags (PRFs) are alerts attached to a patient record that display prominently when the record is accessed. They are used to communicate critical information about behavioral, clinical, or administrative concerns.

> **Warning:** Patient Record Flags are nationally visible across all VA facilities. Exercise extreme care when creating or modifying PRFs. All PRF actions are audited and must comply with VHA Directive requirements.

Flag categories include:

- **Behavioral** -- Disruptive behavior, threats, violence history.
- **Clinical** -- Allergies requiring special precautions, infectious disease alerts.
- **Administrative** -- Missing/incomplete documentation, identity verification required.

Each flag has a status (ACTIVE or INACTIVE), an assignment date, a review date, and narrative text explaining the reason for the flag.

### Tab 3: MST History

Military Sexual Trauma (MST) screening records. MST screening is required for all veterans and is used to determine eligibility for MST-related care at no cost.

| Field | Description |
|-------|-------------|
| Screening Date | Date the MST screening was conducted. |
| Screener | Staff member who administered the screening. |
| Screen Result | POSITIVE, NEGATIVE, or DECLINED. |
| Notes | Additional clinical notes (optional). |

> **Note:** MST-related care is provided at no cost regardless of SC status, priority group, or enrollment. A positive MST screen does not require a formal claim.

### Tab 4: Relations

Emergency contacts, next of kin, spouse, guardians, and other designated individuals. This tab aggregates all relationship records for the patient.

| Relationship Type | Description |
|-------------------|-------------|
| Emergency Contact | Person to contact in clinical emergencies. |
| Next of Kin | Legal next of kin for notification and decision-making. |
| Spouse | Married spouse (affects means test calculations). |
| Guardian | Legal guardian, if applicable (e.g., for incompetent patients). |
| Designee | Authorized representative for VA benefits. |
| Power of Attorney | Healthcare or general POA. |

### Tab 5: Income/Household

Financial information used for means test calculations and copay determination.

| Field | Description |
|-------|-------------|
| Income Year | Tax year for reported income. |
| Gross Annual Income | Total income before deductions. |
| Net Worth | Total assets minus liabilities. |
| Spouse Income | Spouse's gross annual income, if applicable. |
| Number of Dependents | Dependents claimed for means test. |
| Deductible Expenses | Unreimbursed medical expenses, funeral/burial, education. |

### Tab 6: Treating Facilities

Lists all VA facilities where the patient currently receives or has previously received care. This information is sourced from the Master Patient Index and is used for care coordination.

| Field | Description |
|-------|-------------|
| Facility Name | Name of the VA facility. |
| Station Number | VA station identifier. |
| Last Treated Date | Most recent date of service at the facility. |
| Status | Active or Historical. |

---

## Means Test (/means-test)

The Means Test module determines a patient's financial eligibility for VA healthcare benefits and copayment obligations. It implements the annual means test and geographic means test (GMT) required by federal law.

![Means Test form showing income entry, thresholds, and copay determination](screenshots/means-test-form.png)

### Status Workflow

The means test follows a defined status progression:

```
NOT_TESTED → IN_PROGRESS → COMPLETED → ADJUDICATED
                                     → REQUIRES_REVIEW
```

| Status | Description |
|--------|-------------|
| NOT_TESTED | No means test on file. Patient may be pending initial test. |
| IN_PROGRESS | Means test has been started but not yet completed. |
| COMPLETED | All required information has been entered and calculated. |
| ADJUDICATED | Final determination has been made by authorized staff. |
| REQUIRES_REVIEW | Automated checks flagged the test for manual review (e.g., income discrepancy). |

### Income Thresholds

Two threshold calculations are applied:

- **National Means Test (NMT)** -- Based on national VA income thresholds adjusted for dependents. Patients above the NMT threshold are in Priority Group 7 or 8.
- **Geographic Means Test (GMT)** -- Based on HUD median income for the patient's geographic area. Provides a more favorable threshold for patients in high-cost areas.

### Copay Determination

Based on the means test result, patients are classified into one of three copay categories:

| Category | Criteria |
|----------|----------|
| Exempt | SC 50%+, POW, Medal of Honor, catastrophically disabled, income below NMT, or other qualifying exemption. |
| Required (Reduced) | Income above NMT but below GMT. Reduced copay rates apply. |
| Required (Full) | Income above both NMT and GMT thresholds. Full copay rates apply. |

### Using the Means Test Module

1. Open the Means Test page and enter or select the patient.
2. Verify the income year is correct (defaults to current tax year).
3. Enter gross annual income, net worth, spouse income, dependents, and deductible expenses.
4. Click **Calculate** to run the threshold comparison.
5. Review the result showing NMT and GMT comparison with the determined copay category.
6. If the result is correct, click **Complete** to finalize.
7. If the result requires review (e.g., hardship claim), set status to REQUIRES_REVIEW and add notes.

### Means Test History

The history section shows all prior means tests for the patient, including the date, income year, result, and adjudicating staff member. Means tests are retained indefinitely and cannot be deleted.

### Hardship Review

Patients who exceed income thresholds but claim financial hardship may request a hardship determination. This requires:

1. A signed hardship application from the patient.
2. Documentation of unusual medical expenses, catastrophic loss, or other extenuating circumstances.
3. Review and approval by an authorized fiscal officer.
4. Manual override of the copay category in the means test record.

> **Tip:** Hardship reviews should be processed within 10 business days of receipt to avoid delays in patient care.

---

## Service Connected (/service-connected)

The Service Connected module manages a patient's service-connected disability ratings and their impact on VA benefits.

| Field | Description |
|-------|-------------|
| Combined SC Percentage | The overall combined rating from 0% to 100%, calculated using the VA combined ratings formula (not simple addition). |
| Individual Conditions | Each service-connected condition with its individual percentage rating. |
| Effective Date | The date the rating became effective per the VBA determination. |

### Impact on Other Modules

The service-connected percentage affects multiple areas of the system:

| SC Percentage | Impact |
|---------------|--------|
| 0% (SC) | Eligible for VA care for SC conditions at no cost. |
| 10-20% | Priority Group 3. Copay may apply for non-SC care. |
| 30-40% | Priority Group 2. Eligible for beneficiary travel. |
| 50%+ | Priority Group 1. Exempt from all copayments. |
| 100% | Priority Group 1. Exempt from copays. Eligible for additional benefits (dental, caregiver support). |

> **Note:** Patients with SC 50% or higher are exempt from all VA copayments, including pharmacy, outpatient, and inpatient copays. Ensure the SC percentage is accurately recorded to prevent incorrect copay billing.

### Updating SC Ratings

1. Verify the updated rating from the VBA Rating Decision letter.
2. Open the Service Connected page for the patient.
3. Add or update individual conditions and their percentages.
4. The system automatically recalculates the combined rating using the VA combined ratings formula.
5. Click **Save** to record the changes.
6. Verify that the patient's priority group and copay exemption status have been updated accordingly.

---

## Beneficiary Travel (/beneficiary-travel)

The Beneficiary Travel module manages travel reimbursement claims for eligible veterans traveling to and from VA healthcare appointments.

### Eligibility

The following patients are eligible for beneficiary travel reimbursement:

- Service-connected rating of 30% or higher.
- Receiving VA pension benefits.
- Income below the VA national income threshold (means test).
- Traveling for a service-connected condition (any SC percentage).
- Approved hardship determination.

### Claim Lifecycle

```
SUBMITTED → APPROVED → PAID
                    → DENIED
         → CANCELLED
```

| Status | Description |
|--------|-------------|
| SUBMITTED | Claim filed by the patient or clerk. |
| APPROVED | Claim reviewed and approved for payment. |
| PAID | Payment has been processed (direct deposit or check). |
| DENIED | Claim denied due to ineligibility or documentation issues. |
| CANCELLED | Claim withdrawn by the patient or voided by staff. |

### Mileage Calculation

The reimbursement amount is calculated as:

**Reimbursement = (Round-Trip Miles x GSA Mileage Rate) - Deductible**

- **Round-Trip Miles** -- Calculated from the patient's home address to the VA facility and back, using the shortest route.
- **GSA Mileage Rate** -- The current General Services Administration rate for privately owned vehicles.
- **Deductible** -- A per-trip deductible that is applied (waived for certain categories such as SC 30%+).

### Filing a Travel Claim

1. Open the Beneficiary Travel page and enter or select the patient.
2. Verify eligibility based on SC rating, pension status, or means test.
3. Enter the appointment date and the facility visited.
4. Confirm the round-trip mileage (auto-calculated from address on file, with manual override available).
5. Review the calculated reimbursement amount.
6. Submit the claim.

> **Tip:** Encourage patients to file travel claims within 30 days of the appointment for timely processing. Claims filed after 30 days may require additional documentation.

---

## Patient Recall (/patient-recall)

The Patient Recall module manages proactive patient outreach for follow-up appointments, annual exams, and other scheduled care needs.

> **Note:** This feature requires the **PATIENT_RECALL** feature flag to be enabled. Contact your system administrator if this module is not visible.

### Recall Types

| Type | Description |
|------|-------------|
| FOLLOW-UP | Post-visit follow-up for ongoing conditions. |
| ANNUAL_EXAM | Yearly comprehensive examination. |
| LAB_RECHECK | Follow-up laboratory work to monitor values. |
| CHRONIC_CARE | Ongoing management of chronic conditions (diabetes, hypertension, etc.). |
| IMMUNIZATION | Scheduled vaccinations or booster doses. |
| SCREENING | Preventive screening (cancer screening, AAA, etc.). |
| PROCEDURE | Scheduled procedural follow-up. |

### Status Workflow

```
PENDING → LETTER_SENT → CONTACTED → APPOINTMENT_SCHEDULED → COMPLETED
                                                           → CANCELLED
       → OVERDUE
```

| Status | Description |
|--------|-------------|
| PENDING | Recall entry created, no outreach attempted yet. |
| LETTER_SENT | Recall letter mailed to patient. |
| CONTACTED | Patient reached by phone, secure message, or other contact. |
| APPOINTMENT_SCHEDULED | Patient has a confirmed appointment. |
| COMPLETED | Patient completed the recalled visit. |
| CANCELLED | Recall cancelled (patient declined, moved, deceased, etc.). |
| OVERDUE | Recall date has passed without patient contact or appointment. |

### Managing Recalls

1. From the Patient Recall page, click **Add Recall**.
2. Enter the patient ID, recall type, due date, and responsible clinic.
3. Add notes describing the reason for recall.
4. Click **Save** to create the recall entry.
5. As outreach progresses, update the status through each stage.
6. Once the patient completes the appointment, mark the recall as COMPLETED.

The **Overdue** tab highlights all recalls past their due date that have not reached APPOINTMENT_SCHEDULED or COMPLETED status.

The **Dashboard** provides aggregate views of recall volumes, completion rates, and overdue trends by clinic and recall type.

---

## Master Patient Index (/mpi)

The Master Patient Index (MPI) module provides tools for searching, matching, and correlating patient identities across facilities. It is the authoritative source for patient identity resolution.

![MPI search interface showing cross-facility results and confidence scores](screenshots/mpi-search.png)

### Tab 1: Patient Search

Search for patients across the MPI by name, SSN, or Integration Control Number (ICN).

| Search Field | Description |
|--------------|-------------|
| Patient Name | Last name, or Last,First format. |
| SSN | Full or last-4 Social Security Number. |
| ICN | Integration Control Number (national unique identifier). |

Results display matches from all facilities in the MPI with the facility name, station number, and local patient ID at each site.

### Tab 2: Patient Match

The Patient Match tab is used to evaluate potential duplicate records. The system calculates a confidence score based on multiple matching criteria.

| Confidence Score | Interpretation | Action |
|------------------|----------------|--------|
| Greater than 90% | Likely match | Review and confirm as duplicate, then initiate merge. |
| 70% to 90% | Possible match | Manual review required. Compare demographics carefully. |
| Less than 70% | Unlikely match | Typically distinct patients. No action needed unless other evidence exists. |

Matching criteria include: name similarity, SSN match, DOB match, gender match, and address proximity.

### Tab 3: MPI Status

The MPI Status tab provides operational metrics for the local facility's MPI integration:

| Metric | Description |
|--------|-------------|
| Total Patients | Number of patients in the local MPI index. |
| Cross-Facility Links | Number of patients with records at multiple facilities. |
| Pending Correlations | Records awaiting identity resolution. |
| Last Sync | Timestamp of the most recent MPI synchronization. |

---

## Patient Merge (/patient-merge)

The Patient Merge module provides the ability to merge duplicate patient records into a single surviving record. This is one of the most consequential operations in the system.

> **Note:** This feature requires the **PATIENT_MERGE** feature flag to be enabled. Contact your system administrator if this module is not visible.

> **Warning:** Patient merge is an irreversible operation. Once two records are merged, they cannot be separated. All data from the source (duplicate) record is moved to the target (surviving) record, and the source record is permanently marked as merged.

![Patient Merge side-by-side comparison of source and target records](screenshots/patient-merge-comparison.png)

### Merge Process

The merge process involves selecting a source record (the duplicate to be retired) and a target record (the surviving record that will receive all data).

1. Enter the **Source Patient ID** (the duplicate record to be retired).
2. Enter the **Target Patient ID** (the surviving record to retain).
3. Review the side-by-side comparison showing demographics, appointments, orders, notes, and other data from both records.
4. Verify that the target record has the correct demographic information. If not, update the target record first via Patient Edit before proceeding.
5. Check the **confirmation checkbox** acknowledging that the merge is irreversible.
6. Click **Merge Records**.
7. Wait for the confirmation message indicating the merge completed successfully.

> **Warning:** The source patient record will be permanently retired after the merge. All clinical data, appointments, orders, notes, lab results, and other records from the source will be transferred to the target. The source patient ID will be marked as merged and will redirect to the target record in future lookups.

### What Gets Merged

All data from the source record is moved to the target, including:

- Appointments (past and future)
- Clinical orders
- Progress notes and TIU documents
- Lab results
- Radiology reports
- Pharmacy records
- Problem list entries
- Consult requests
- Vital signs

### Post-Merge Verification

After a merge:

1. Open the target patient record and verify that data from both records is present.
2. Check that no duplicate entries were created for the same clinical events.
3. Verify that the source patient ID redirects to the target record.
4. Notify any clinicians who had the source patient on their active patient lists.

---

## Common Workflows

### New Patient Registration

1. Search for the patient in Patient Lookup to confirm no existing record exists.
2. If no match is found, click **New Patient** to open a blank Patient Edit form.
3. Enter required demographics (Name, Sex) and all available information across all five tabs.
4. Save the patient record.
5. Navigate to Registration and complete enrollment (Tab 1), add any known relationships (Tab 4), and enter income information (Tab 5) if available.
6. Complete the Means Test to determine copay category.
7. Print the face sheet and have the patient verify all information for accuracy.

### Inpatient Admission

1. Look up the patient in Patient Lookup and verify identity (name, DOB, last-4 SSN).
2. Confirm enrollment status is active and eligibility is current in the Registration module.
3. Navigate to the ADT module and select **Admit Patient**.
4. Complete the admission form with ward assignment, admitting diagnosis, and attending provider, then submit.

---

## Tips and Best Practices

> **Tip:** Always verify patient identity using at least two identifiers (name plus DOB, or name plus last-4 SSN) before making any changes to a patient record.

> **Tip:** When entering addresses, use the standardized USPS format for street addresses to ensure consistency and accurate mileage calculations for beneficiary travel.

> **Tip:** Review the patient's treating facilities list periodically. Stale facility associations can cause confusion during care coordination.

> **Tip:** Means tests should be completed annually. Set up patient recalls with the ANNUAL_EXAM type to prompt yearly re-evaluation.

> **Tip:** When creating PRF flags, include specific and objective language describing the concern. Avoid subjective or inflammatory characterizations.

> **Tip:** For patients with complex eligibility (e.g., combat veteran enhanced eligibility within 5 years of separation), document the eligibility basis in the enrollment notes to facilitate future re-verification.

---

## Screenshots Reference

The following screenshots are referenced throughout this section:

- ![Patient Lookup results showing search bar and results table](screenshots/patient-lookup-results.png)
- ![Patient Edit demographics tab](screenshots/patient-edit-demographics.png)
- ![Registration enrollment tab with priority group assignment](screenshots/registration-enrollment.png)
- ![Means Test form with income thresholds and copay determination](screenshots/means-test-form.png)
- ![MPI search interface with cross-facility results](screenshots/mpi-search.png)
- ![Patient Merge side-by-side comparison](screenshots/patient-merge-comparison.png)
