# Social Work

This section covers the Social Work clinical tools available in NewVistas for Clinical Social Workers, Case Managers, and Discharge Planners. These modules support psychosocial assessment, referral management, home-based care coordination, geriatric extended care, home telehealth monitoring, and voluntary service administration.

**VistA File References:** #200.5 (Social Work Patient), #750 (Home Based Primary Care), #25.1 (Geriatrics & Extended Care)

**Primary Roles:** Clinical Social Workers, Case Managers, Discharge Planners, HBPC Coordinators, GEC Coordinators, Voluntary Service Officers

---

## Social Work (/social-work)

The Social Work page is the central hub for psychosocial assessments and social service referrals. It is organized into two tabs: **Assessments** and **Referrals**.

![Social Work main page with Assessments tab selected](screenshots/social-work-assessments.png)

### Tab 1: Assessments

Assessments document the psychosocial status of a patient and identify areas where social work intervention is needed. Each assessment captures housing, employment, social support, risk factors, and clinical recommendations.

#### Assessment Types

The following assessment types are available:

| Type | Description |
|------|-------------|
| **Psychosocial** | Comprehensive evaluation of a patient's social, emotional, and environmental functioning |
| **FunctionalStatus** | Assessment of the patient's ability to perform activities of daily living |
| **DischargeRisk** | Evaluation of factors that may complicate safe discharge from inpatient care |
| **HomelessRisk** | Screening for current or imminent risk of homelessness |
| **SubstanceUse** | Assessment of substance use history, current use, and treatment needs |
| **DomesticViolence** | Screening and safety assessment for intimate partner or domestic violence |
| **Bereavement** | Evaluation of grief, loss, and bereavement support needs |
| **CaregiverStress** | Assessment of caregiver burden, coping, and support needs |
| **Other** | General-purpose assessment for situations not covered by the specific types above |

#### Assessment Status

Each assessment progresses through the following statuses:

- **Draft** -- The assessment has been started but is not yet complete. Draft assessments can be edited freely.
- **Completed** -- All required fields have been filled in and the assessment is ready for signature.
- **Signed** -- The assessment has been electronically signed by the social worker and becomes part of the permanent medical record.

> **Warning:** Once an assessment is signed, it cannot be edited. If corrections are needed, create an addendum or a new assessment referencing the original.

#### Risk Level

Every assessment includes a risk level classification:

| Risk Level | Meaning |
|------------|---------|
| **Unknown** | Risk has not yet been determined |
| **Low** | Minimal risk factors identified; routine follow-up appropriate |
| **Moderate** | Some risk factors present; enhanced monitoring or intervention recommended |
| **High** | Significant risk factors identified; immediate intervention plan required |
| **Critical** | Imminent danger or crisis; requires immediate action and safety planning |

#### Assessment Fields

The following fields are captured on each assessment:

- **Housing Status** -- Current living situation. Values:
  - `HOUSED` -- Stable permanent housing
  - `HOMELESS` -- Currently without housing
  - `AT RISK` -- At risk of losing current housing
  - `TRANSITIONAL` -- In transitional housing program
  - `SHELTER` -- Currently residing in a shelter
  - `INSTITUTIONAL` -- Residing in an institutional setting (nursing home, group home, etc.)

- **Employment Status** -- Current employment situation. Values:
  - `EMPLOYED` -- Currently working (full-time or part-time)
  - `UNEMPLOYED` -- Not currently employed and seeking work
  - `RETIRED` -- Retired from employment
  - `DISABLED` -- Unable to work due to disability
  - `STUDENT` -- Currently enrolled in an educational program

- **Social Support** -- Strength of the patient's social support network. Values:
  - `STRONG` -- Robust support network with multiple reliable contacts
  - `ADEQUATE` -- Sufficient support for current needs
  - `POOR` -- Limited or absent support network; isolation risk

- **Abuse Concerns** -- Free-text field to document any concerns about abuse, neglect, or exploitation
- **Safety Plan** -- Documentation of any safety plan developed with the patient
- **Discharge Disposition** -- Planned discharge destination (home, skilled nursing, assisted living, etc.)
- **Discharge Plan** -- Detailed narrative of the discharge plan including services arranged
- **Recommendations** -- Clinical recommendations for follow-up services, referrals, and interventions
- **Notes** -- Additional free-text documentation

#### Creating an Assessment

1. Select a patient using the patient search at the top of the page.
2. Click the **Assessments** tab if it is not already selected.
3. Click the **New Assessment** button.
4. Select the appropriate **Assessment Type** from the dropdown.
5. Complete all required fields, including Housing Status, Employment Status, and Social Support.
6. Set the **Risk Level** based on your clinical judgment.
7. Enter any applicable notes in the Abuse Concerns, Safety Plan, Discharge Disposition, Discharge Plan, and Recommendations fields.
8. Click **Save as Draft** to save your work in progress, or **Complete** to finalize the assessment.
9. Once completed, click **Sign** to apply your electronic signature.

> **Tip:** Save your assessment as a draft frequently if you are gathering information over multiple interactions. Drafts are preserved between sessions.

![Assessment form showing all fields and risk level selector](screenshots/social-work-assessment-form.png)

### Tab 2: Referrals

Referrals connect patients with internal VA programs and external community resources. Each referral tracks the type of service needed, priority, and current status.

#### Referral Types

| Type | Description |
|------|-------------|
| **Housing** | Referral to housing programs (HUD-VASH, SSVF, GPD, etc.) |
| **Benefits** | Assistance with VA benefits, pension, compensation claims |
| **Counseling** | Referral to individual, group, or family counseling services |
| **VA Programs** | Referral to specific VA programs (CWT, VR&E, Caregiver Support, etc.) |
| **Community Resources** | Connection to community-based organizations and services |
| **Medical** | Referral for medical services or specialty care coordination |
| **Financial** | Financial counseling, emergency financial assistance, or aid |
| **Legal** | Referral to legal aid services or VA legal programs |
| **Other** | Referrals not covered by the categories above |

#### Referral Priority and Expected Response Times

| Priority | Expected Response Time | Use When |
|----------|----------------------|----------|
| **ROUTINE** | Days to weeks | Standard referral with no immediate safety concern |
| **URGENT** | 24-48 hours | Time-sensitive situation requiring prompt attention |
| **EMERGENT** | Same day | Immediate safety concern, crisis, or life-threatening situation |

> **Warning:** EMERGENT referrals should be accompanied by direct contact with the receiving service. Do not rely solely on the electronic referral for emergent situations.

#### Referral Status Workflow

Referrals progress through the following statuses:

```
PENDING → ACCEPTED → IN_PROGRESS → COMPLETED
                  ↘ ON_HOLD
                  ↘ DECLINED
                  ↘ CANCELLED
```

- **PENDING** -- Referral has been submitted and is awaiting review by the receiving service
- **ACCEPTED** -- The receiving service has acknowledged and accepted the referral
- **IN_PROGRESS** -- The referred service is actively being provided
- **COMPLETED** -- The referred service has been delivered and the referral is closed
- **ON_HOLD** -- The referral is temporarily paused (e.g., patient unavailable)
- **DECLINED** -- The receiving service has declined the referral (reason documented)
- **CANCELLED** -- The referral has been cancelled by the referring social worker

#### Creating a Referral

1. Select a patient using the patient search.
2. Click the **Referrals** tab.
3. Click the **New Referral** button.
4. Select the **Referral Type** from the dropdown.
5. Set the **Priority** level (Routine, Urgent, or Emergent).
6. Enter a description of the reason for referral and any relevant clinical details.
7. Specify the **Referred To** service or program.
8. Click **Submit Referral**.

> **Note:** You will receive a notification when the status of your referral changes. Monitor the referral list for updates from the receiving service.

![Referral list showing multiple referrals with status indicators](screenshots/social-work-referral-list.png)

---

## Notes (/notes)

The Notes page allows social workers to create clinical documentation associated with a patient encounter. Social work note types integrate with the TIU (Text Integration Utilities) document system.

### Social Work Note Types

| Note Type | Purpose |
|-----------|---------|
| **Social Work Assessment** | Comprehensive psychosocial assessment documentation |
| **Discharge Planning Note** | Documentation of discharge planning activities, barriers, and arrangements |
| **Case Management Note** | Ongoing case management contacts, coordination, and follow-up |
| **Crisis Intervention Note** | Documentation of crisis situations, interventions, and safety plans |
| **Community Resource Referral Note** | Record of community resource connections and referral outcomes |
| **Group Note** | Documentation of group sessions (support groups, psychoeducation, etc.) |

### Creating a Social Work Note

1. Navigate to the **Notes** page (/notes).
2. Select the patient.
3. Click **New Note**.
4. Select the appropriate **Note Type** from the list above.
5. Enter the note content, including assessment findings, interventions, and plan.
6. Click **Save** to save as a draft or **Sign** to finalize.

> **Tip:** Use standardized templates when available. Templates ensure consistent documentation and help meet compliance requirements for social work charting.

---

## Home Health (/home-health)

The Home Health page supports Home Based Primary Care (HBPC) and Home Health Care (HHC) programs. It is organized into three tabs.

![Home Health page showing HBPC patient enrollment](screenshots/home-health-visit-log.png)

### Tab 1: HBPC Patient

This tab displays enrollment information and the clinical record for patients enrolled in the Home Based Primary Care program.

- **Enrollment Status** -- Whether the patient is currently enrolled, pending enrollment, or disenrolled
- **Enrollment Date** -- Date the patient was enrolled in HBPC
- **Primary Care Team** -- The assigned HBPC interdisciplinary team
- **ADL/IADL Scores** -- Functional status scores for activities of daily living
- **Care Plan** -- Current care plan goals and interventions
- **Emergency Contact** -- Primary emergency contact information

#### Enrolling a Patient in HBPC

1. Navigate to the **Home Health** page and select the **HBPC Patient** tab.
2. Search for the patient by name or ID.
3. Click **Enroll Patient**.
4. Complete the enrollment form including program assignment, primary diagnosis, and care team.
5. Document baseline ADL and IADL scores.
6. Click **Submit Enrollment**.

### Tab 2: Registry

The Registry tab provides a roster view of all patients currently enrolled in home health programs. It supports filtering by team, program, and enrollment status.

- **Patient Roster** -- List of all enrolled patients with key demographic and clinical information
- **Filter Options** -- Filter by care team, program type, enrollment date range, or patient status
- **Caseload Summary** -- Aggregate counts showing each team member's current caseload

> **Tip:** Use the caseload summary to balance assignments across team members and identify capacity for new enrollments.

### Tab 3: HHC Visits

The HHC Visits tab tracks all home health care visits, including scheduled and completed visits.

- **Visit Log** -- Chronological list of all home health visits with date, type, clinician, and duration
- **Record Visit** -- Form to document a new visit including findings, interventions, and plan
- **Overdue Visits** -- Highlighted list of patients who are past due for their next scheduled visit

#### Visit Types

| Visit Type | Description |
|------------|-------------|
| ROUTINE | Regularly scheduled home visit |
| URGENT | Unscheduled visit in response to a patient need |
| ADMISSION | Initial visit for a newly enrolled patient |
| DISCHARGE | Final visit upon disenrollment or transfer |
| FOLLOW_UP | Follow-up visit after a change in condition or hospitalization |

#### Recording a Home Health Visit

1. Click the **HHC Visits** tab.
2. Click **Record Visit**.
3. Select the patient from the active roster.
4. Choose the **Visit Type**.
5. Enter the visit date, start time, and duration.
6. Select the discipline of the visiting clinician (Social Work, Nursing, Physician, etc.).
7. Document findings, interventions performed, and the follow-up plan.
8. Click **Save Visit**.

> **Note:** Overdue visits are highlighted in red on the visit log. Review these daily to ensure continuity of care.

---

## Geriatrics & Extended Care (/geriatrics)

The Geriatrics & Extended Care (GEC) page supports assessment, Community Living Center (CLC) admission management, and census tracking. It is organized into three tabs.

### Tab 1: Assessments

GEC assessments evaluate a patient's functional, cognitive, and medical status to determine the appropriate level of care.

#### Assessment Types

- **Initial** -- First assessment upon referral to GEC
- **Annual** -- Yearly reassessment for patients in ongoing GEC programs
- **Change in Condition** -- Reassessment triggered by a significant change in the patient's status
- **Discharge** -- Assessment completed at the time of discharge from a GEC program

#### MDS ADL Scoring

The Minimum Data Set (MDS) Activities of Daily Living (ADL) scoring system evaluates functional status across seven components. Each component is scored from 0 to 4.

| Component | Score 0 | Score 1 | Score 2 | Score 3 | Score 4 |
|-----------|---------|---------|---------|---------|---------|
| **Bed Mobility** | Independent | Supervision | Limited assistance | Extensive assistance | Total dependence |
| **Transfer** | Independent | Supervision | Limited assistance | Extensive assistance | Total dependence |
| **Locomotion** | Independent | Supervision | Limited assistance | Extensive assistance | Total dependence |
| **Dressing** | Independent | Supervision | Limited assistance | Extensive assistance | Total dependence |
| **Eating** | Independent | Supervision | Limited assistance | Extensive assistance | Total dependence |
| **Toilet Use** | Independent | Supervision | Limited assistance | Extensive assistance | Total dependence |
| **Personal Hygiene** | Independent | Supervision | Limited assistance | Extensive assistance | Total dependence |

#### ADL Score Interpretation

The total ADL score is the sum of all seven component scores (range 0-28):

| Total Score Range | Interpretation | Care Level |
|-------------------|---------------|------------|
| **0-7** | Independent / Minimal Assistance | Community-based care likely appropriate |
| **8-14** | Moderate Assistance | Enhanced home care or assisted living may be needed |
| **15-21** | Substantial Assistance | Skilled nursing or CLC placement may be indicated |
| **22-28** | Dependent / Total Care | CLC or long-term institutional care required |

> **Note:** ADL scores are one component of the overall GEC assessment. Clinical judgment, cognitive status, caregiver availability, and patient preference must all be considered when making placement recommendations.

![GEC ADL scoring form with seven component sliders](screenshots/geriatrics-adl-scoring.png)

### Tab 2: CLC Admissions

The CLC Admissions tab manages admissions to the Community Living Center (the VA's long-term care facility, formerly called Nursing Home Care Unit).

#### Admission Types

| Type | Description | Typical Duration |
|------|-------------|-----------------|
| **SHORT_STAY** | Acute or post-acute rehabilitation | Up to 90 days |
| **LONG_STAY** | Ongoing custodial or skilled nursing care | Indefinite |
| **RESPITE** | Temporary admission to provide caregiver relief | Up to 30 days |
| **HOSPICE** | Comfort-focused end-of-life care | Variable |
| **REHABILITATION** | Intensive rehabilitation following surgery, stroke, or injury | Varies by program |

#### Admission Fields

- **Admission Date** -- Date the patient is admitted to the CLC
- **Admission Type** -- One of the types listed above
- **Unit Assignment** -- The specific CLC unit or ward
- **Attending Physician** -- The physician responsible for the patient's CLC care
- **Diagnosis** -- Primary diagnosis for the admission
- **Expected Length of Stay** -- Estimated duration of the CLC stay

#### Admitting a Patient to the CLC

1. Click the **CLC Admissions** tab.
2. Click **New Admission**.
3. Select the patient and verify their identity.
4. Choose the **Admission Type**.
5. Assign the patient to a **Unit**.
6. Enter the attending physician, primary diagnosis, and expected length of stay.
7. Click **Submit Admission**.

### Tab 3: Dashboard

The GEC Dashboard provides an at-a-glance view of the current CLC census and operational metrics.

- **Active Patients** -- Count and list of all currently admitted CLC patients
- **Length of Stay (LOS) Tracking** -- Average and individual LOS statistics by admission type
- **Occupancy Rate** -- Current bed utilization by unit
- **Pending Admissions** -- Patients awaiting CLC admission
- **Upcoming Discharges** -- Patients with planned discharge dates in the near future

---

## Home Telehealth (/home-telehealth)

The Home Telehealth page manages remote patient monitoring programs, including enrollment, vital sign readings, device management, and clinical alerts. It is organized into four tabs.

### Tab 1: Enrollment

Manage patient enrollment in home telehealth monitoring programs.

- **Enrollment Status** -- ENROLLED or DISENROLLED
- **Enrollment Date** -- Date the patient was enrolled
- **Disenrollment Date** -- Date the patient was removed from the program (if applicable)

#### Program Types

| Program | Description |
|---------|-------------|
| **GENERAL** | General remote monitoring for patients with multiple chronic conditions |
| **CHF** | Congestive Heart Failure monitoring (weight, BP, symptoms) |
| **COPD** | Chronic Obstructive Pulmonary Disease monitoring (SpO2, peak flow, symptoms) |
| **DIABETES** | Diabetes monitoring (glucose, weight, diet adherence) |
| **HYPERTENSION** | Hypertension monitoring (BP, medication adherence) |
| **MENTAL_HEALTH** | Mental health monitoring (mood, sleep, symptom tracking) |

#### Enrolling a Patient

1. Click the **Enrollment** tab.
2. Click **Enroll Patient**.
3. Select the patient.
4. Choose the appropriate **Program Type**.
5. Assign monitoring devices (see the Devices tab).
6. Set alert thresholds if different from the program defaults.
7. Click **Submit Enrollment**.

### Tab 2: Readings

View and manage vital sign readings submitted by patients through their home telehealth devices.

#### Vital Sign Types and Auto-Alert Thresholds

| Vital Sign | Unit | Low Alert Threshold | High Alert Threshold |
|------------|------|---------------------|----------------------|
| **BP (Systolic)** | mmHg | < 90 | > 180 |
| **BP (Diastolic)** | mmHg | < 60 | > 110 |
| **HR (Heart Rate)** | bpm | < 50 | > 120 |
| **WEIGHT** | lbs | > 3 lbs gain in 24h | > 5 lbs gain in 7 days |
| **GLUCOSE** | mg/dL | < 70 | > 300 |
| **SPO2** | % | < 90 | N/A |
| **TEMPERATURE** | F | < 96.0 | > 101.5 |
| **PEAK_FLOW** | L/min | < 50% of personal best | N/A |

Each reading displays:
- Date and time of the reading
- Vital sign type and value
- Device used to capture the reading
- Alert flag (if the reading is out of the threshold range)

> **Warning:** Out-of-range readings automatically generate alerts. Review alerts promptly in the Alerts tab to ensure timely clinical response.

![Home Telehealth readings list with alert indicators](screenshots/home-telehealth-readings.png)

### Tab 3: Devices

Manage the inventory of home telehealth devices assigned to patients.

- **Device Type** -- Blood pressure monitor, glucometer, pulse oximeter, scale, thermometer, peak flow meter, etc.
- **Model** -- Manufacturer and model of the device
- **Serial Number** -- Unique device identifier
- **Assignment** -- Patient currently assigned to the device (or unassigned)
- **Last Reading** -- Date and time of the most recent reading received from the device
- **Status** -- Active, inactive, or in need of replacement

### Tab 4: Alerts

Clinical alerts are generated automatically when patient readings exceed defined thresholds, or manually by care coordinators.

#### Alert Severity

| Severity | Meaning | Response Expectation |
|----------|---------|---------------------|
| **WARNING** | Reading is outside normal range but not immediately dangerous | Review within the same business day |
| **CRITICAL** | Reading indicates a potentially dangerous condition | Review and respond immediately |

#### Alert Status Workflow

```
ACTIVE → ACKNOWLEDGED → ESCALATED → RESOLVED
```

- **ACTIVE** -- Alert has been generated and requires attention
- **ACKNOWLEDGED** -- A clinician has reviewed the alert and is taking action
- **ESCALATED** -- The alert has been escalated to a higher level of care (e.g., PCP notification, ED referral)
- **RESOLVED** -- The clinical concern has been addressed and the alert is closed

#### Responding to an Alert

1. Click the **Alerts** tab.
2. Review the list of active alerts, sorted by severity (CRITICAL first).
3. Click on an alert to view the associated reading and patient details.
4. Click **Acknowledge** to indicate you are reviewing the alert.
5. Contact the patient and/or take appropriate clinical action.
6. If the situation requires higher-level intervention, click **Escalate** and document the reason.
7. Once the situation is resolved, click **Resolve** and document the outcome.

> **Tip:** Set up your notification preferences to receive real-time alerts for CRITICAL readings. This ensures you can respond even when not actively viewing the dashboard.

---

## Voluntary Service (/voluntary-service)

The Voluntary Service page manages the volunteer program, including volunteer registration, assignment tracking, hours logging, and program management. It is organized into four tabs.

### Tab 1: Volunteers

Manage the roster of registered volunteers.

#### Volunteer Status

| Status | Description |
|--------|-------------|
| **ACTIVE** | Volunteer is currently participating in the program |
| **INACTIVE** | Volunteer is registered but not currently participating |
| **ON_LEAVE** | Volunteer is on an approved leave of absence |
| **TERMINATED** | Volunteer has been removed from the program |

Each volunteer record includes:
- Name and contact information
- Date of registration
- Current status
- Skills and certifications
- Availability (days of the week and hours)
- Background check status
- Emergency contact

### Tab 2: Assignments

Track where volunteers are assigned within the facility.

- **Service Area** -- The department or unit where the volunteer works
- **Supervisor** -- Staff member responsible for overseeing the volunteer
- **Schedule** -- Days and times of the assignment
- **Start Date / End Date** -- Duration of the assignment

### Tab 3: Hours

Log and review volunteer hours for recognition and reporting purposes.

- **Date** -- Date the hours were worked
- **Hours** -- Number of hours volunteered
- **Service Area** -- Where the hours were performed
- **Activity Description** -- Brief summary of activities performed

#### Logging Volunteer Hours

1. Click the **Hours** tab.
2. Click **Log Hours**.
3. Select the volunteer from the roster.
4. Enter the date, number of hours, and service area.
5. Provide a brief description of the activities performed.
6. Click **Save**.

### Tab 4: Programs

Manage volunteer programs and special initiatives.

- **Program Name** -- Name of the volunteer program
- **Description** -- Purpose and activities of the program
- **Coordinator** -- Staff member managing the program
- **Active Volunteers** -- Count of volunteers currently assigned to the program

### Recognition Milestones

Volunteers are recognized for their cumulative hours of service at the following milestones:

| Milestone | Hours Required | Recognition |
|-----------|---------------|-------------|
| **Bronze** | 100 hours | Bronze volunteer pin and certificate |
| **Silver** | 500 hours | Silver volunteer pin and certificate |
| **Gold** | 1,000 hours | Gold volunteer pin and certificate |
| **Platinum** | 5,000 hours | Platinum volunteer pin and special recognition |
| **Lifetime** | 10,000 hours | Lifetime achievement award and permanent recognition |

> **Tip:** The system automatically tracks cumulative hours and will flag volunteers who are approaching a recognition milestone so you can plan the appropriate ceremony.

![Voluntary service hours log with recognition milestone indicators](screenshots/voluntary-service-hours.png)

---

## Common Workflows

### Inpatient Discharge Planning

This workflow is used when preparing a patient for discharge from an inpatient setting.

1. **Assess the patient's discharge needs** -- Complete a Discharge Risk assessment on the Social Work page (/social-work). Document housing status, social support, functional status, and any barriers to safe discharge.
2. **Identify required services** -- Based on the assessment, determine what post-discharge services are needed (home health, equipment, transportation, follow-up appointments, community resources).
3. **Create referrals** -- Submit referrals for each identified service on the Referrals tab. Set priority to URGENT for services that must be in place before discharge.
4. **Coordinate with the care team** -- Document coordination activities in a Discharge Planning Note (/notes). Communicate the discharge plan to nursing, physicians, and other team members.
5. **Confirm arrangements** -- Verify that all referrals have been accepted and services are scheduled. Update the discharge disposition on the assessment.
6. **Complete discharge documentation** -- Finalize the Discharge Planning Note, sign the assessment, and ensure the patient and family have received discharge instructions.

### Homeless Veteran Outreach

This workflow supports outreach and engagement with Veterans experiencing homelessness.

1. **Screen for homelessness risk** -- Complete a Homeless Risk assessment on the Social Work page. Document current housing status and contributing factors.
2. **Assess immediate needs** -- Determine if the Veteran needs emergency shelter, food, clothing, or medical care. For emergent situations, set the risk level to Critical.
3. **Connect with housing programs** -- Create a Housing referral. Include eligibility information for HUD-VASH, SSVF, GPD, and other VA homeless programs.
4. **Coordinate benefits enrollment** -- Submit a Benefits referral if the Veteran is not receiving all entitled benefits. Assist with compensation, pension, and healthcare enrollment.
5. **Document and follow up** -- Create a Case Management Note documenting the encounter, services provided, and follow-up plan. Schedule the next contact.

### GEC Assessment and CLC Placement

This workflow covers the process from initial GEC referral through CLC admission.

1. **Complete the GEC assessment** -- Navigate to the Geriatrics page (/geriatrics) and complete an Initial assessment. Score all seven ADL components using the MDS scoring system.
2. **Determine level of care** -- Review the total ADL score and cognitive assessment. Consult with the GEC team to determine the appropriate level of care.
3. **Arrange CLC admission** -- If CLC placement is indicated, navigate to the CLC Admissions tab. Select the appropriate admission type and unit.
4. **Coordinate the transition** -- Work with the current care team and the CLC to arrange the transfer. Document the transition plan in a Discharge Planning Note.
5. **Complete admission documentation** -- Once the patient arrives at the CLC, confirm the admission in the system and update the GEC dashboard.

---

## Tips and Best Practices

1. **Complete assessments promptly.** Psychosocial assessments should be initiated within 24 hours of admission for inpatients and at the first encounter for outpatients. Timely documentation supports continuity of care and discharge planning.

2. **Use the correct referral priority.** Reserve EMERGENT priority for true safety concerns and crises. Overuse of high-priority referrals delays response to genuinely urgent situations.

3. **Monitor referral status regularly.** Check the Referrals tab daily for status updates. Follow up on referrals that remain in PENDING status for more than 48 hours.

4. **Document discharge barriers early.** Identify and document barriers to discharge (housing, transportation, caregiver availability, equipment needs) as soon as they are identified. Early identification allows more time for resolution.

5. **Review the Home Health overdue visits list daily.** Patients who miss scheduled home visits may be experiencing a change in condition, transportation issues, or other problems that require follow-up.

6. **Use ADL scores consistently.** When scoring ADL components, apply the MDS definitions consistently. If you are unsure about a score, consult with the GEC team or refer to the MDS manual.

7. **Keep volunteer records current.** Update volunteer status promptly when volunteers go on leave or are terminated. Accurate records support compliance and recognition tracking.

8. **Coordinate across programs.** Many patients are enrolled in multiple social work programs simultaneously (e.g., HBPC and Home Telehealth). Use the Notes page to document cross-program coordination and avoid duplication of services.
