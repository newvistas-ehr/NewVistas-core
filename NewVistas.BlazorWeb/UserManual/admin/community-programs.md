# Community and Extended Care Programs

This section covers the community-based and extended care program management tools in NewVistas. These modules support home telehealth monitoring, home health and community care, geriatric and extended care services, voluntary service administration, and research/IRB management.

**Routes:** /home-telehealth, /home-health, /geriatrics, /voluntary-service, /research-irb

**Primary Roles:** Home Telehealth Coordinators, Care Coordinators, HBPC Clinicians, GEC Coordinators, CLC Staff, Voluntary Service Officers, Research Coordinators, IRB Administrators

---

## Home Telehealth (/home-telehealth)

The Home Telehealth page manages remote patient monitoring programs that allow Veterans to transmit vital signs and health data from their homes. The system tracks enrollment, vital sign readings, devices, and clinical alerts. It is organized into four tabs.

### Tab 1: Enrollment

Manage patient enrollment in home telehealth monitoring programs.

#### Enrollment Status

- **ENROLLED** -- The patient is actively enrolled and transmitting readings
- **DISENROLLED** -- The patient has been removed from the program

Each enrollment record includes:
- Patient name and identifier
- Enrollment date
- Disenrollment date (if applicable)
- Program type
- Assigned care coordinator
- Assigned devices

#### Program Types

| Program | Target Population | Key Monitored Parameters |
|---------|------------------|-------------------------|
| **GENERAL** | Patients with multiple chronic conditions | Varies by patient; typically BP, weight, and symptoms |
| **CHF** | Congestive Heart Failure patients | Daily weight, BP, heart rate, edema assessment, dyspnea |
| **COPD** | Chronic Obstructive Pulmonary Disease patients | SpO2, peak flow, respiratory symptoms, exacerbation signs |
| **DIABETES** | Diabetes patients | Blood glucose (fasting and post-meal), weight, foot inspection |
| **HYPERTENSION** | Hypertension patients | Blood pressure (morning and evening), medication adherence |
| **MENTAL_HEALTH** | Mental health patients | Mood scale, sleep quality, symptom checklist, medication adherence |

#### Enrolling a Patient

1. Click the **Enrollment** tab.
2. Click **Enroll Patient**.
3. Search for and select the patient.
4. Choose the appropriate **Program Type** based on the patient's clinical needs.
5. Assign the patient to a care coordinator.
6. Assign one or more monitoring devices from the device inventory (see Tab 3: Devices).
7. Review and adjust the alert thresholds if the program defaults are not appropriate for this patient.
8. Click **Submit Enrollment**.

> **Note:** Patients can be enrolled in multiple programs simultaneously (e.g., CHF and DIABETES). Each program's thresholds and monitoring schedules are applied independently.

#### Disenrolling a Patient

1. Locate the patient on the Enrollment tab.
2. Click **Disenroll**.
3. Select a reason for disenrollment (e.g., clinical improvement, patient request, transfer, deceased).
4. Enter any notes regarding the disenrollment.
5. Click **Confirm Disenrollment**.

> **Tip:** When disenrolling a patient, ensure that assigned devices are returned and reassigned. Check the Devices tab for the patient's device assignments.

### Tab 2: Readings

View vital sign readings transmitted by patients through their home telehealth devices.

![Home Telehealth readings list with alert flags for out-of-range values](screenshots/home-telehealth-readings.png)

#### Vital Sign Types and Auto-Alert Thresholds

The system automatically flags readings that fall outside predefined thresholds. Default thresholds are set by program type but can be customized per patient.

| Vital Sign | Unit | Low Alert Threshold | High Alert Threshold |
|------------|------|---------------------|----------------------|
| **BP (Systolic)** | mmHg | < 90 | > 180 |
| **BP (Diastolic)** | mmHg | < 60 | > 110 |
| **HR (Heart Rate)** | bpm | < 50 | > 120 |
| **WEIGHT** | lbs | > 3 lbs gain in 24 hours | > 5 lbs gain in 7 days |
| **GLUCOSE** | mg/dL | < 70 | > 300 |
| **SPO2** | % | < 90 | N/A |
| **TEMPERATURE** | degrees F | < 96.0 | > 101.5 |
| **PEAK_FLOW** | L/min | < 50% of personal best | N/A |

Each reading record displays:
- **Date/Time** -- When the reading was taken
- **Vital Sign Type** -- The type of measurement
- **Value** -- The measured value with units
- **Device** -- The device used to capture the reading
- **Alert Flag** -- A visual indicator (yellow for WARNING, red for CRITICAL) if the reading is out of range

> **Warning:** Out-of-range readings automatically generate alerts on the Alerts tab. Review all flagged readings promptly. A delay in responding to critical readings can result in adverse patient outcomes.

#### Reviewing Readings

1. Click the **Readings** tab.
2. Filter by patient, date range, or vital sign type.
3. Review readings for trends and out-of-range values.
4. Click on any reading to view detailed information including the device used and any associated alerts.

### Tab 3: Devices

Manage the inventory of home telehealth devices.

Each device record includes:
- **Device Type** -- Blood pressure monitor, glucometer, pulse oximeter, scale, thermometer, peak flow meter, etc.
- **Model** -- Manufacturer and model name
- **Serial Number** -- Unique device identifier
- **Assignment** -- The patient currently assigned to the device, or "Unassigned"
- **Last Reading** -- Date and time of the most recent reading received from the device
- **Status** -- Active, Inactive, or Needs Replacement

#### Device Management

- **Assign Device** -- Link a device to a patient's enrollment
- **Unassign Device** -- Remove a device from a patient (e.g., upon disenrollment)
- **Mark for Replacement** -- Flag a device that is malfunctioning or nearing end of life
- **Retire Device** -- Remove a device from the active inventory

### Tab 4: Alerts

Clinical alerts are generated when patient readings exceed defined thresholds or when expected readings are not received.

#### Alert Severity

| Severity | Meaning | Expected Response |
|----------|---------|-------------------|
| **WARNING** | A reading is outside the normal range but not immediately dangerous | Review within the same business day |
| **CRITICAL** | A reading indicates a potentially dangerous clinical condition | Review and respond immediately |

#### Alert Status Workflow

```
ACTIVE → ACKNOWLEDGED → ESCALATED → RESOLVED
```

- **ACTIVE** -- The alert has been generated and is awaiting clinician review
- **ACKNOWLEDGED** -- A clinician has reviewed the alert and is taking or planning action
- **ESCALATED** -- The alert has been escalated to a higher level of care (e.g., physician notification, ED referral, 911)
- **RESOLVED** -- The clinical concern has been addressed and the alert is closed

#### Responding to Alerts

1. Click the **Alerts** tab.
2. Review active alerts, sorted by severity (CRITICAL alerts appear first).
3. Click on an alert to view the triggering reading, patient details, and clinical context.
4. Click **Acknowledge** to indicate you are reviewing the alert.
5. Contact the patient by telephone or secure message. Document the contact and clinical findings.
6. If the situation requires higher-level intervention, click **Escalate** and select the escalation path (PCP notification, ED referral, 911).
7. Once the clinical concern is addressed, click **Resolve** and document the outcome.

> **Tip:** Configure your notification preferences to receive real-time notifications for CRITICAL alerts via MailMan or email. This ensures prompt response even when you are not actively viewing the Home Telehealth dashboard.

---

## Home Health / Community Care (/home-health)

The Home Health page supports Home Based Primary Care (HBPC) and Home Health Care (HHC) programs that provide in-home clinical services to Veterans. It is organized into three tabs.

### Tab 1: HBPC Patient

View and manage individual patient enrollment records for the Home Based Primary Care program.

#### Patient Record Fields

- **Enrollment Status** -- Current status (Enrolled, Pending, Disenrolled)
- **Enrollment Date** -- Date the patient was enrolled in HBPC
- **Primary Diagnosis** -- The primary condition driving HBPC enrollment
- **ADL Score** -- Activities of Daily Living functional score
- **IADL Score** -- Instrumental Activities of Daily Living score (cooking, shopping, medication management, finances, etc.)
- **Care Team** -- Assigned interdisciplinary care team members
- **Emergency Contact** -- Patient's primary emergency contact
- **Care Plan** -- Current care plan goals, interventions, and target dates

#### Enrolling a Patient

1. Click the **HBPC Patient** tab.
2. Click **Enroll Patient**.
3. Search for and select the patient.
4. Enter the enrollment details including primary diagnosis, ADL and IADL scores, and assigned care team.
5. Document the initial care plan with goals and interventions.
6. Click **Submit Enrollment**.

### Tab 2: Registry

The Registry provides a roster view of all patients enrolled in home health programs with filtering and caseload management tools.

- **Patient Roster** -- Complete list of enrolled patients with key clinical and demographic information
- **Active Patients** -- Count of currently enrolled patients
- **Filter Options** -- Filter the roster by:
  - Care team
  - Program type
  - Enrollment date range
  - Patient status
  - Geographic area
- **Caseload Summary** -- View each team member's current patient load to support workload balancing

> **Tip:** Use the caseload summary during team meetings to identify capacity for new enrollments and ensure equitable distribution of patients across team members.

### Tab 3: HHC Visits

Track and document all home health care visits.

#### Visit Log

The visit log displays all visits in chronological order with:
- Patient name
- Visit date and time
- Visit type
- Clinician name and discipline
- Duration
- Status (Scheduled, Completed, Missed, Cancelled)

#### Visit Types

| Visit Type | Description |
|------------|-------------|
| **ROUTINE** | Regularly scheduled home visit per the care plan |
| **URGENT** | Unscheduled visit in response to an acute need or change in condition |
| **ADMISSION** | Initial home visit for a newly enrolled patient |
| **DISCHARGE** | Final visit at disenrollment or transfer from the program |
| **FOLLOW_UP** | Follow-up after hospitalization, change in condition, or medication change |

#### Recording a Visit

1. Click the **HHC Visits** tab.
2. Click **Record Visit**.
3. Select the patient from the active roster.
4. Choose the **Visit Type**.
5. Enter the visit date, start time, end time, and calculated duration.
6. Select the **Discipline** of the visiting clinician (Social Work, Nursing, Physician, Physical Therapy, Occupational Therapy, Speech Therapy, Dietetics, etc.).
7. Document findings, interventions performed, and follow-up plan.
8. Click **Save Visit**.

#### Overdue Visits

Patients who are past due for their next scheduled visit are highlighted on the visit log. The overdue list shows:
- Patient name
- Last visit date
- Expected visit date
- Number of days overdue

> **Warning:** Review the overdue visits list daily. Missed home visits may indicate a change in the patient's condition, transportation issues, or other concerns that require follow-up.

### Dashboard

The Home Health dashboard provides operational metrics:

| Metric | Description |
|--------|-------------|
| **Active Patients** | Total number of currently enrolled patients |
| **Monthly Visits** | Count of visits completed in the current month |
| **Overdue Visits** | Number of patients with overdue visits |
| **Average Visit Duration** | Mean duration of visits across all disciplines |

---

## Geriatrics & Extended Care (/geriatrics)

The Geriatrics & Extended Care (GEC) page supports comprehensive geriatric assessment, Community Living Center (CLC) admission management, and census tracking. It is organized into three tabs.

### Tab 1: Assessments

GEC assessments evaluate the functional, cognitive, and medical status of patients to determine the appropriate level of care.

#### Assessment Types

| Type | When Used |
|------|-----------|
| **Initial** | First assessment upon referral to GEC services |
| **Annual** | Yearly reassessment for patients in ongoing GEC programs |
| **Change in Condition** | Triggered by a significant change in the patient's clinical or functional status |
| **Discharge** | Completed when a patient is discharged from a GEC program or CLC |

#### MDS ADL Scoring

The Minimum Data Set (MDS) Activities of Daily Living scoring system evaluates functional status across seven components. Each component is scored from 0 (Independent) to 4 (Total Dependence).

| Component | What Is Assessed | Score 0 | Score 1 | Score 2 | Score 3 | Score 4 |
|-----------|-----------------|---------|---------|---------|---------|---------|
| **Bed Mobility** | Ability to move in bed (turning, repositioning) | Independent | Supervision only | Limited physical assistance | Extensive physical assistance | Total dependence |
| **Transfer** | Ability to move between surfaces (bed to chair) | Independent | Supervision only | Limited physical assistance | Extensive physical assistance | Total dependence |
| **Locomotion** | Ability to move within the living environment | Independent | Supervision only | Limited physical assistance | Extensive physical assistance | Total dependence |
| **Dressing** | Ability to dress and undress | Independent | Supervision only | Limited physical assistance | Extensive physical assistance | Total dependence |
| **Eating** | Ability to eat and drink | Independent | Supervision only | Limited physical assistance | Extensive physical assistance | Total dependence |
| **Toilet Use** | Ability to use the toilet | Independent | Supervision only | Limited physical assistance | Extensive physical assistance | Total dependence |
| **Personal Hygiene** | Ability to maintain personal hygiene (bathing, grooming) | Independent | Supervision only | Limited physical assistance | Extensive physical assistance | Total dependence |

![GEC ADL scoring form with seven component sliders and total score](screenshots/geriatrics-adl-scoring.png)

#### ADL Score Interpretation

The total ADL score is the sum of all seven component scores (range 0-28):

| Total Score Range | Interpretation | Recommended Care Level |
|-------------------|---------------|----------------------|
| **0-7** | Independent / Minimal Assistance | Community-based care; patient can likely manage at home with minimal support |
| **8-14** | Moderate Assistance | Enhanced home care, adult day health care, or assisted living may be appropriate |
| **15-21** | Substantial Assistance | Skilled nursing, CLC placement, or intensive home care may be indicated |
| **22-28** | Dependent / Total Care | CLC long-term care or equivalent institutional care is typically required |

> **Note:** The ADL score is one component of the comprehensive GEC assessment. Clinical judgment, cognitive status, medical complexity, caregiver availability, patient preference, and safety considerations must all factor into the level-of-care determination.

#### Cognitive Status

In addition to ADL scoring, the GEC assessment includes a cognitive status evaluation:
- Brief Interview for Mental Status (BIMS) score
- Cognitive Performance Scale (CPS) rating
- Behavioral observations
- Delirium screening

#### Completing a GEC Assessment

1. Navigate to the Geriatrics page (/geriatrics) and click the **Assessments** tab.
2. Click **New Assessment**.
3. Select the patient and choose the assessment type (Initial, Annual, Change in Condition, or Discharge).
4. Score each of the seven ADL components using the sliders or dropdowns (0-4 for each).
5. The system automatically calculates the total ADL score and displays the interpretation.
6. Complete the cognitive status evaluation.
7. Enter clinical findings, recommendations, and the proposed care plan.
8. Click **Save** to save as a draft or **Complete** to finalize.

### Tab 2: CLC Admissions

The CLC Admissions tab manages admissions to the Community Living Center.

#### Admission Types

| Type | Description | Typical Duration |
|------|-------------|-----------------|
| **SHORT_STAY** | Post-acute rehabilitation or short-term skilled nursing care | Up to 90 days |
| **LONG_STAY** | Ongoing custodial or skilled nursing care for patients who cannot return home | Indefinite |
| **RESPITE** | Temporary admission to provide caregiver relief | Up to 30 days |
| **HOSPICE** | Comfort-focused end-of-life care in the CLC setting | Variable; based on patient's condition |
| **REHABILITATION** | Intensive rehabilitation following surgery, stroke, hip fracture, or other acute event | Varies by program (typically 2-6 weeks) |

#### Admission Fields

- **Patient** -- The patient being admitted
- **Admission Date** -- Date of admission to the CLC
- **Admission Type** -- One of the types listed above
- **Unit Assignment** -- The specific CLC unit or ward
- **Attending Physician** -- The physician responsible for the patient's CLC care
- **Primary Diagnosis** -- The primary diagnosis driving the admission
- **Expected Length of Stay** -- Estimated duration of the CLC stay

#### Admitting a Patient

1. Click the **CLC Admissions** tab.
2. Click **New Admission**.
3. Search for and select the patient. Verify identity.
4. Select the **Admission Type**.
5. Assign the patient to a **Unit** (based on availability and care needs).
6. Enter the attending physician, primary diagnosis, and expected length of stay.
7. Click **Submit Admission**.

> **Note:** Ensure a GEC assessment has been completed before admitting a patient to the CLC. The assessment provides the clinical justification for placement.

### Tab 3: Dashboard

The GEC Dashboard provides a real-time view of CLC operations and census.

![CLC census dashboard showing occupancy and length of stay metrics](screenshots/geriatrics-clc-census.png)

#### Census Information

| Field | Description |
|-------|-------------|
| **Patient Name** | Name and identifier of the admitted patient |
| **Unit** | CLC unit where the patient is located |
| **Admission Date** | Date the patient was admitted |
| **Admission Type** | Type of CLC admission |
| **LOS (Length of Stay)** | Number of days since admission |
| **Status** | Current patient status |

#### Patient Status

| Status | Description |
|--------|-------------|
| **ADMITTED** | Patient is currently on the CLC unit |
| **DISCHARGED** | Patient has been discharged from the CLC |
| **TRANSFERRED** | Patient has been transferred to another unit or facility |
| **LOA (Leave of Absence)** | Patient is temporarily away from the CLC (e.g., family visit, external appointment) |

#### Dashboard Metrics

- **Total Census** -- Current number of admitted patients
- **Average LOS** -- Average length of stay by admission type
- **Occupancy Rate** -- Percentage of available beds occupied, by unit
- **Pending Admissions** -- Patients approved for admission but not yet admitted
- **Upcoming Discharges** -- Patients with planned discharge dates within the next 7 days

---

## Voluntary Service (/voluntary-service)

The Voluntary Service page manages the facility's volunteer program, including volunteer registration, assignment tracking, hours logging, program management, and recognition. It is organized into four tabs.

### Tab 1: Volunteers

Manage the roster of registered volunteers.

#### Volunteer Status

| Status | Description |
|--------|-------------|
| **ACTIVE** | Volunteer is currently participating and available for assignment |
| **INACTIVE** | Volunteer is registered but not currently participating (e.g., seasonal volunteer) |
| **ON_LEAVE** | Volunteer is on an approved leave of absence with a planned return date |
| **TERMINATED** | Volunteer has been permanently removed from the program |

Each volunteer record includes:
- Full name and contact information (phone, email, address)
- Date of registration
- Current status
- Skills and certifications (e.g., CPR, wheelchair assistance, language skills)
- Availability (days of the week and time slots)
- Background check status and date
- Emergency contact
- Cumulative hours and recognition level

#### Registering a New Volunteer

1. Click the **Volunteers** tab.
2. Click **Register Volunteer**.
3. Enter the volunteer's personal information and emergency contact.
4. Document skills, certifications, and availability.
5. Record the background check status and completion date.
6. Click **Save**.

### Tab 2: Assignments

Track where volunteers are assigned and what duties they perform.

- **Service Area** -- Department, clinic, or unit where the volunteer works
- **Supervisor** -- Staff member responsible for overseeing the volunteer
- **Duties** -- Description of the volunteer's assigned responsibilities
- **Schedule** -- Regular days and times of the assignment
- **Start Date / End Date** -- Duration of the assignment

#### Creating an Assignment

1. Click the **Assignments** tab.
2. Click **New Assignment**.
3. Select the volunteer from the roster.
4. Select the service area and supervisor.
5. Describe the duties and set the schedule.
6. Enter the start date and, if known, the end date.
7. Click **Save**.

### Tab 3: Hours

Log and review volunteer hours for tracking, reporting, and recognition purposes.

Each hours entry includes:
- **Volunteer** -- Name of the volunteer
- **Date** -- Date the hours were worked
- **Hours** -- Number of hours volunteered
- **Service Area** -- Where the hours were performed
- **Activity Description** -- Brief summary of activities performed

#### Logging Hours

1. Click the **Hours** tab.
2. Click **Log Hours**.
3. Select the volunteer from the roster.
4. Enter the date, number of hours, service area, and activity description.
5. Click **Save**.

> **Tip:** Encourage volunteers to log their hours at the end of each shift. Timely logging ensures accurate records and prevents milestones from being missed.

### Tab 4: Programs

Manage volunteer programs and special initiatives.

- **Program Name** -- Name of the volunteer program (e.g., No Veteran Dies Alone, Creative Arts, Patient Transport)
- **Description** -- Purpose, activities, and goals of the program
- **Coordinator** -- Staff member managing the program
- **Active Volunteers** -- Count of volunteers currently assigned to the program
- **Status** -- Active or Inactive

### Recognition Milestones

The system automatically tracks cumulative volunteer hours and identifies volunteers who have reached recognition milestones.

| Milestone | Hours Required | Recognition |
|-----------|---------------|-------------|
| **Bronze** | 100 hours | Bronze volunteer pin and certificate of appreciation |
| **Silver** | 500 hours | Silver volunteer pin and certificate of recognition |
| **Gold** | 1,000 hours | Gold volunteer pin and certificate of excellence |
| **Platinum** | 5,000 hours | Platinum volunteer pin and special recognition ceremony |
| **Lifetime** | 10,000 hours | Lifetime achievement award and permanent recognition display |

![Voluntary service dashboard showing hours summary and milestone progress](screenshots/voluntary-service-dashboard.png)

> **Note:** The system flags volunteers who are within 10% of their next recognition milestone so you can plan the appropriate recognition event in advance.

---

## Research / IRB (/research-irb)

The Research / IRB (Institutional Review Board) page manages research studies, subject enrollment, adverse event reporting, and IRB oversight. It supports the full lifecycle of clinical research from protocol submission through study closure.

### Studies

#### Study Fields

- **Protocol Number** -- Unique identifier for the research protocol
- **Title** -- Full title of the research study
- **Principal Investigator (PI)** -- The lead researcher responsible for the study
- **Co-Investigators** -- Additional researchers involved in the study
- **Sponsor** -- Funding source or sponsoring organization
- **IRB Approval Date** -- Date the IRB approved the protocol
- **IRB Expiration Date** -- Date by which continuing review is required

#### Study Types

| Type | Description |
|------|-------------|
| **INTERVENTIONAL** | Study involves an intervention (drug, device, procedure) applied to participants |
| **OBSERVATIONAL** | Study observes participants without intervening; data collection only |
| **REGISTRY** | Systematic collection of data for a defined population (e.g., disease registry) |
| **RETROSPECTIVE** | Study analyzes previously collected data (chart review) |
| **QUALITY_IMPROVEMENT** | Project aimed at improving clinical processes or outcomes (may not require full IRB review) |

#### Study Status Workflow

```
SUBMITTED → UNDER_REVIEW → APPROVED → ACTIVE → CLOSED
                                  ↘ SUSPENDED
                                  ↘ EXPIRED
```

- **SUBMITTED** -- Protocol has been submitted to the IRB for review
- **UNDER_REVIEW** -- The IRB is actively reviewing the protocol
- **APPROVED** -- The IRB has approved the protocol; study may begin enrollment
- **ACTIVE** -- The study is actively enrolling or following participants
- **SUSPENDED** -- The study has been temporarily halted (e.g., safety concern, protocol deviation)
- **CLOSED** -- The study has been completed and closed
- **EXPIRED** -- The IRB approval has expired without renewal; study activities must cease until re-approved

> **Warning:** Studies with EXPIRED status must not enroll new subjects or conduct study procedures until the IRB approval is renewed. Monitor expiration dates proactively.

### Subjects

#### Subject Enrollment

Each subject record includes:
- Subject identifier (study-specific, de-identified)
- Enrollment date
- Randomization group (if applicable)
- Consent status
- Study arm assignment
- Visit schedule and compliance

#### Consent Status

| Status | Description |
|--------|-------------|
| **PENDING** | Consent form has been provided but not yet signed |
| **OBTAINED** | Informed consent has been signed and documented |
| **DECLINED** | The potential subject declined to participate |
| **WITHDRAWN** | The subject withdrew their consent after initially agreeing |

#### Randomization

For randomized studies, the system supports:
- Randomization assignment based on the study protocol
- Blinding management (single-blind, double-blind, open-label)
- Randomization log with timestamps and assignments

#### Enrolling a Subject

1. Navigate to the Research / IRB page (/research-irb).
2. Select the study from the study list.
3. Click **Enroll Subject**.
4. Enter the subject identifier and verify eligibility criteria.
5. Document the consent process and set the consent status to **OBTAINED**.
6. If the study is randomized, click **Randomize** to assign the subject to a study arm.
7. Click **Save Enrollment**.

> **Note:** Subjects cannot be enrolled until the study status is APPROVED or ACTIVE. Ensure consent is obtained before any study procedures are performed.

### Adverse Events

Adverse events occurring during the course of a study must be documented and reported.

#### Adverse Event Fields

- **Date of Event** -- When the adverse event occurred
- **Description** -- Detailed description of the event
- **Severity** -- Classification of the event's severity
- **Relatedness** -- Assessment of whether the event is related to the study intervention
- **Outcome** -- Current outcome of the event
- **SAE Flag** -- Whether the event qualifies as a Serious Adverse Event

#### Severity Levels

| Severity | Description |
|----------|-------------|
| **MILD** | Awareness of sign or symptom but easily tolerated; no intervention required |
| **MODERATE** | Discomfort sufficient to interfere with usual activities; may require intervention |
| **SEVERE** | Incapacitating; inability to perform usual activities; requires medical intervention |
| **LIFE_THREATENING** | Immediate risk of death from the event |
| **FATAL** | Death resulted from the adverse event |

#### Relatedness Assessment

- **Unrelated** -- The event is clearly not related to the study intervention
- **Unlikely** -- The event is doubtfully related to the study intervention
- **Possible** -- The event may be related to the study intervention
- **Probable** -- The event is likely related to the study intervention
- **Definite** -- The event is clearly related to the study intervention

#### SAE (Serious Adverse Event) Flag

An adverse event is flagged as an SAE if it results in any of the following:
- Death
- Life-threatening condition
- Inpatient hospitalization or prolongation of existing hospitalization
- Persistent or significant disability
- Congenital anomaly or birth defect
- Other medically important event

> **Warning:** Serious Adverse Events must be reported to the IRB within the timeframe specified in the protocol (typically 24 hours for fatal or life-threatening events, 5 business days for other SAEs). Timely reporting is a regulatory requirement.

#### Reporting an Adverse Event

1. Select the study and the affected subject.
2. Click **Report Adverse Event**.
3. Enter the date, description, and severity.
4. Assess the relatedness to the study intervention.
5. Document the current outcome.
6. If the event meets SAE criteria, check the **SAE** flag.
7. Click **Submit Report**.

### Dashboard

The Research Dashboard provides an overview of all research activities at the facility.

![Research study list with status indicators and enrollment counts](screenshots/research-study-list.png)

| Metric | Description |
|--------|-------------|
| **Active Studies** | Count of studies currently in ACTIVE status |
| **Pending Reviews** | Count of protocols currently UNDER_REVIEW by the IRB |
| **Expiring Approvals** | Studies with IRB approval expiring within the next 30 days |
| **Total Enrolled Subjects** | Aggregate count of subjects enrolled across all active studies |
| **Open Adverse Events** | Count of adverse events that have not yet been resolved |

> **Tip:** Review the dashboard weekly to identify studies approaching their IRB expiration date. Submit continuing review applications at least 60 days before expiration to allow time for IRB processing.

---

## Common Workflows

### Enrolling a Patient in Home Telehealth

1. Identify a patient who would benefit from remote monitoring based on their chronic condition management needs.
2. Navigate to Home Telehealth (/home-telehealth) and click **Enroll Patient** on the Enrollment tab.
3. Select the appropriate program type and assign devices.
4. Configure patient-specific alert thresholds if the defaults are not appropriate.
5. Educate the patient on device use and the daily monitoring routine.
6. Monitor the Readings and Alerts tabs during the first week to ensure the patient is transmitting correctly and to catch any setup issues.

### GEC Assessment and CLC Placement

1. Complete a GEC assessment on the Geriatrics page (/geriatrics). Score all seven ADL components and the cognitive status evaluation.
2. Review the total ADL score and interpretation. Discuss findings with the interdisciplinary team.
3. If CLC placement is indicated, identify the appropriate admission type and available unit.
4. Coordinate with the patient, family, and current care team regarding the transition plan.
5. Complete the CLC admission on the CLC Admissions tab. Monitor the census dashboard for updates.

### Managing a Research Study Lifecycle

1. Submit the study protocol on the Research / IRB page (/research-irb). Ensure all required documents (protocol, consent form, investigator brochure) are attached.
2. Monitor the study status as it progresses through IRB review.
3. Once approved, begin subject enrollment. Document consent for each subject.
4. Monitor for adverse events throughout the study. Report SAEs within the required timeframes.
5. Submit continuing review applications before the IRB approval expires.
6. When the study is complete, submit a final report and close the study.

---

## Screenshots Reference

| Screenshot | Description |
|------------|-------------|
| ![Home Telehealth readings](screenshots/home-telehealth-readings.png) | Home Telehealth readings with alert flags for out-of-range values |
| ![GEC ADL scoring](screenshots/geriatrics-adl-scoring.png) | GEC assessment with MDS ADL scoring components |
| ![CLC census](screenshots/geriatrics-clc-census.png) | CLC census dashboard with occupancy and LOS metrics |
| ![Voluntary service dashboard](screenshots/voluntary-service-dashboard.png) | Voluntary service dashboard with hours and milestone tracking |
| ![Research study list](screenshots/research-study-list.png) | Research study list with status and enrollment counts |
