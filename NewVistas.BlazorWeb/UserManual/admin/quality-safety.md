# Quality, Safety, and Infection Control

This module covers the quality management, patient safety, infection control, patient advocacy, suicide prevention, clinical registries, and polytrauma/TBI programs within NewVistas. These modules support the continuous improvement, risk management, and regulatory compliance functions that are essential to safe healthcare delivery.

**Intended Audience:** Quality Management Officers, Patient Safety Managers, Infection Control Practitioners, Risk Managers, Patient Advocates, Suicide Prevention Coordinators, and Registry Coordinators.

**VistA File References:** File #680 (Quality Management), File #745 (Healthcare-Associated Infection), File #200.5 (Safety Program).

**Primary Routes:** `/quality-management`, `/patient-advocate`, `/infection-control`, `/suicide-prevention`, `/clinical-registries`, `/polytrauma`, `/audit-trail`.

---

## Quality Management (/quality-management)

The Quality Management module is the central hub for incident reporting, peer review, and root cause analysis. It supports the facility's quality improvement program and compliance with VHA, The Joint Commission, and NCPS (National Center for Patient Safety) requirements.

### Tab 1: Incident Reporting

The Incident Reporting tab provides a structured form for capturing patient safety events and near misses.

#### Incident Report Form Fields

| Field | Required | Description |
|-------|----------|-------------|
| Patient ID | Yes | The patient involved in the incident (or "No Patient" for environmental/system incidents). |
| Occurrence Date | Yes | Date and time the incident occurred. |
| Discovery Date | No | Date and time the incident was discovered (if different from occurrence). |
| Category | Yes | Type of incident (see category list below). |
| Severity | Yes | Severity level of the incident (see severity scale below). |
| Location | Yes | Physical location where the incident occurred (ward, clinic, department). |
| Description | Yes | Detailed narrative description of what happened. |
| Immediate Actions Taken | No | Actions taken at the time of the incident to mitigate harm. |
| Staff Involved | No | Staff members involved in or witness to the incident. |
| Contributing Factors | No | Factors that contributed to the incident (staffing, equipment, communication, etc.). |

#### Incident Categories

| Category | Description |
|----------|-------------|
| MEDICATION | Medication errors, adverse drug reactions, wrong dose/drug/route/patient. |
| FALL | Patient falls, including falls with and without injury. |
| PROCEDURE | Wrong site, wrong procedure, wrong patient, retained foreign body, procedural complications. |
| DIAGNOSTIC | Delayed diagnosis, missed diagnosis, wrong diagnosis, lost or mislabeled specimens. |
| EQUIPMENT | Equipment malfunction, failure, or misuse. |
| COMMUNICATION | Communication failures, handoff errors, missing or incorrect information transfer. |
| SECURITY | Unauthorized access, missing patient, elopement, workplace violence. |
| OTHER | Incidents not fitting the above categories. |

#### Severity Scale

| Severity | Badge Color | Description |
|----------|-------------|-------------|
| Near Miss | Gray | An event that could have caused harm but did not reach the patient. No actual injury. |
| Minor | Blue | Event reached the patient but caused no harm or only minimal temporary harm requiring no additional intervention. |
| Moderate | Yellow | Event reached the patient and caused temporary harm requiring intervention (additional treatment, extended stay). |
| Major | Orange | Event reached the patient and caused permanent harm or required major intervention to sustain life. |
| Sentinel Event | Red | An unexpected occurrence involving death or serious physical or psychological injury, or the risk thereof. |

![Incident report form showing category, severity badges, and description fields](screenshots/quality-incident-form.png)

![Severity badges showing color-coded severity levels from Near Miss through Sentinel Event](screenshots/quality-severity-badges.png)

> **Warning:** Sentinel Events require a Root Cause Analysis (RCA) to be completed within 45 calendar days and notification to the National Center for Patient Safety (NCPS). Failure to report and investigate sentinel events is a serious regulatory compliance violation.

#### Incident Status Workflow

```
REPORTED → UNDER_REVIEW → RCA_REQUESTED → RESOLVED → CLOSED
```

| Status | Description |
|--------|-------------|
| REPORTED | Incident report submitted. Initial triage pending. |
| UNDER_REVIEW | Quality management team is reviewing the incident. |
| RCA_REQUESTED | A Root Cause Analysis has been initiated for this incident. |
| RESOLVED | Corrective actions have been identified, implemented, or planned. |
| CLOSED | Investigation complete. All corrective actions verified. Case closed. |

#### Submitting an Incident Report

1. Open the Quality Management module and navigate to the Incident Reporting tab.
2. Click **New Incident Report**.
3. Enter the Patient ID (or select "No Patient" for environmental incidents).
4. Record the Occurrence Date and Discovery Date.
5. Select the incident Category.
6. Assess and select the Severity level.
7. Enter the Location where the incident occurred.
8. Write a detailed Description of what happened, using objective and factual language.
9. Document any Immediate Actions Taken at the time.
10. Identify Staff Involved and Contributing Factors as applicable.
11. Click **Submit** to file the report.
12. The report enters REPORTED status and is routed to the quality management team for triage.

### Tab 2: Peer Reviews

The Peer Review tab manages clinical peer review cases where the quality of care provided by an individual practitioner is evaluated by clinical peers.

| Column | Description |
|--------|-------------|
| Case ID | System-assigned identifier. |
| Provider | The provider whose care is being reviewed. |
| Service | Clinical service (Medicine, Surgery, Psychiatry, etc.). |
| Trigger | What prompted the review (incident report, mortality review, random sample, external complaint). |
| Review Date | Date the peer review was conducted. |
| Outcome | The peer review determination. |
| Status | PENDING, IN_REVIEW, COMPLETED. |

#### Peer Review Outcomes

| Outcome | Description |
|---------|-------------|
| Meets Standard | The care provided met the expected standard of practice. No further action needed. |
| Opportunity for Improvement | The care was acceptable overall but areas for improvement were identified. Educational feedback provided to the provider. |
| Below Standard | The care did not meet the expected standard of practice. Referred to the Medical Staff Office for further action. |

![Peer review outcome showing determination and supporting narrative](screenshots/quality-peer-review.png)

> **Note:** Peer review records are protected under 38 U.S.C. 5705 (Confidentiality of Medical Quality-Assurance Records). These records are not subject to FOIA, discovery in legal proceedings, or release outside the peer review process. All peer review participants must understand and comply with this protection.

### Tab 3: RCA Dashboard

The Root Cause Analysis Dashboard provides metrics for tracking RCA activity and outcomes.

| Metric | Description |
|--------|-------------|
| Open RCAs | Number of RCAs currently in progress. |
| Overdue RCAs | RCAs past the 45-day completion deadline. |
| Completed This Quarter | Number of RCAs completed in the current quarter. |
| Top Root Causes | Most frequently identified root causes across all RCAs. |
| Action Item Completion Rate | Percentage of RCA action items completed on time. |

### Sentinel Event Investigation

When a sentinel event is identified, the following investigation process is required:

1. **Immediate Response** -- Ensure the patient (if applicable) is safe and receiving appropriate care. Secure any equipment or evidence related to the event.
2. **Initial Notification** -- Notify facility leadership (Chief of Staff, Director, Patient Safety Manager) within 24 hours.
3. **NCPS Notification** -- Report the sentinel event to the National Center for Patient Safety within the required timeframe.
4. **RCA Team Assembly** -- Convene a multidisciplinary RCA team that includes subject matter experts, frontline staff, and quality/safety leadership. The team should not include individuals directly involved in the event.
5. **Information Gathering** -- Collect all relevant information: medical records, witness statements, equipment logs, policies, procedures, and environmental factors.
6. **Root Cause Analysis** -- Conduct the RCA using the NCPS methodology. Identify the root causes (system-level) rather than assigning individual blame.
7. **Action Plan Development** -- Develop specific, measurable, and sustainable corrective actions for each identified root cause. Assign owners and deadlines.
8. **Report Submission** -- Complete the RCA report and submit to NCPS within 45 calendar days of the event. Present findings and action plan to facility leadership.

> **Tip:** Focus RCA findings on system-level causes rather than individual performance. The goal is to prevent recurrence by improving systems, not to assign blame.

---

## Patient Advocate (/patient-advocate)

The Patient Advocate module manages patient and family complaints, compliments, congressional inquiries, and related advocacy activities.

### Tab 1: Complaints

The Complaints tab tracks patient and family concerns through investigation and resolution.

#### Complaint Categories

| Category | Description |
|----------|-------------|
| QUALITY_OF_CARE | Concerns about the clinical care received. |
| ACCESS | Difficulty obtaining appointments, long wait times, geographic barriers. |
| COMMUNICATION | Communication issues between staff and patient/family. |
| ENVIRONMENT | Facility cleanliness, comfort, safety, or accessibility concerns. |
| BILLING | Billing disputes, copay concerns, insurance issues. |
| STAFF_CONDUCT | Concerns about staff behavior, attitude, or professionalism. |
| PRIVACY | Perceived or actual privacy violations. |
| WAIT_TIME | Excessive wait times in clinic, ED, or for procedures. |
| OTHER | Concerns not fitting the above categories. |

#### Complaint Status Workflow

```
OPEN → INVESTIGATING → RESOLVED → CLOSED
```

| Status | Description |
|--------|-------------|
| OPEN | Complaint received and logged. Not yet assigned for investigation. |
| INVESTIGATING | Complaint assigned to an advocate or subject matter expert for investigation. |
| RESOLVED | Investigation complete. Resolution communicated to the patient. |
| CLOSED | Case closed. Follow-up complete and documentation finalized. |

#### Filing a Complaint

1. Open the Patient Advocate module and navigate to the Complaints tab.
2. Click **New Complaint**.
3. Enter the patient ID (or complainant information if the complaint is from a family member or representative).
4. Select the complaint category.
5. Enter a detailed description of the concern.
6. Record any desired resolution expressed by the patient/family.
7. Assign the complaint to an advocate for investigation.
8. Click **Submit**.

![Patient complaint tracking showing categories, status, and assigned advocate](screenshots/quality-complaint-tracking.png)

### Tab 2: Congressional Inquiries

The Congressional Inquiries tab tracks inquiries received from members of Congress on behalf of constituents.

| Column | Description |
|--------|-------------|
| Inquiry ID | System-assigned identifier. |
| Congress Member | Name and office of the congressional member. |
| Patient/Constituent | The individual on whose behalf the inquiry was made. |
| Date Received | Date the inquiry was received at the facility. |
| Subject | Brief description of the inquiry topic. |
| Response Due | Deadline for the facility's response. |
| Status | RECEIVED, IN_PROGRESS, RESPONSE_SENT, CLOSED. |
| Assigned To | Staff member responsible for drafting the response. |

> **Warning:** Congressional inquiries have strict response timelines set by VACO (VA Central Office). Late responses reflect poorly on the facility and may trigger escalation. Monitor the Response Due date closely and prioritize these cases.

### Tab 3: Dashboard

The Patient Advocate Dashboard provides operational and trend metrics.

| Metric | Description |
|--------|-------------|
| Open Complaints | Number of complaints in OPEN or INVESTIGATING status. |
| Average Resolution Time | Mean days from OPEN to RESOLVED. |
| Complaints This Month | Total complaints received in the current month. |
| Top Categories | Most frequent complaint categories. |
| Satisfaction Rate | Percentage of complainants who reported satisfaction with the resolution. |
| Congressional Inquiries Open | Number of congressional inquiries pending response. |

---

## Infection Control (/infection-control)

The Infection Control module provides surveillance, tracking, and reporting capabilities for healthcare-associated infections (HAIs) and infectious disease outbreaks.

### Tab 1: HAI Cases

The HAI Cases tab tracks individual healthcare-associated infection cases.

#### HAI Types

| Type | Full Name | Description |
|------|-----------|-------------|
| CLABSI | Central Line-Associated Bloodstream Infection | Bloodstream infection in a patient with a central venous catheter. |
| CAUTI | Catheter-Associated Urinary Tract Infection | Urinary tract infection in a patient with an indwelling urinary catheter. |
| SSI | Surgical Site Infection | Infection at or near the surgical incision site within 30-90 days of the procedure. |
| VAP | Ventilator-Associated Pneumonia | Pneumonia developing in a patient on mechanical ventilation for more than 48 hours. |
| CDI | Clostridioides difficile Infection | Healthcare-onset CDI (symptom onset more than 3 days after admission). |

#### HAI Case Status

| Status | Description |
|--------|-------------|
| SUSPECTED | Case identified based on initial clinical criteria. Confirmation pending. |
| CONFIRMED | Case confirmed by laboratory results and NHSN criteria. |
| RESOLVED | Infection treated and resolved. |
| EXCLUDED | Case reviewed and determined not to meet HAI criteria. |

#### HAI Case Detail

Each HAI case contains the following detailed information:

| Field | Description |
|-------|-------------|
| Patient | Patient ID and demographics. |
| HAI Type | CLABSI, CAUTI, SSI, VAP, or CDI. |
| Onset Date | Date the infection was first identified. |
| Culture Source | Specimen type (blood, urine, wound, sputum, stool). |
| Gram Stain | Gram stain result (gram-positive, gram-negative, mixed). |
| Pathogen | Identified organism(s). |
| Susceptibility | Antibiotic susceptibility profile (Sensitive, Intermediate, Resistant for each tested antibiotic). |
| Device Days | Number of days the associated device was in place (for CLABSI, CAUTI, VAP). Used for rate calculations. |
| Isolation Precautions | Type of isolation precautions implemented (Contact, Droplet, Airborne, Enhanced Contact). |
| Location | Ward/unit where the patient was at the time of onset. |
| Contributing Factors | Identified risk factors (prolonged device use, immunosuppression, recent surgery, etc.). |

![HAI case detail showing pathogen, susceptibility, and isolation precautions](screenshots/quality-hai-case.png)

### Tab 2: Outbreaks

The Outbreaks tab manages the declaration, investigation, and containment of infectious disease outbreaks.

| Column | Description |
|--------|-------------|
| Outbreak ID | System-assigned identifier. |
| Pathogen | The causative organism. |
| Declaration Date | Date the outbreak was declared. |
| Location | Ward(s) or area(s) affected. |
| Linked Cases | Number of HAI cases linked to this outbreak. |
| Status | ACTIVE, CONTAINED, or RESOLVED. |
| Containment Measures | Summary of containment actions in effect. |

#### Declaring an Outbreak

1. When epidemiologic evidence suggests a cluster of related infections, click **Declare Outbreak** on the Outbreaks tab.
2. Enter the suspected pathogen, affected location(s), and date of declaration.
3. Link existing HAI cases to the outbreak.
4. Document initial containment measures (enhanced cleaning, cohorting, screening, visitor restrictions, etc.).
5. Submit the outbreak declaration.

### Infection Outbreak Response

The following procedure should be followed when an outbreak is declared:

1. **Notify Leadership** -- Immediately notify the Chief of Staff, Nurse Executive, and facility Director of the outbreak declaration.
2. **Activate Response Team** -- Convene the multidisciplinary infection control response team (Infection Control, Nursing, Environmental Services, Laboratory, Pharmacy, affected clinical service).
3. **Implement Containment** -- Enact containment measures: enhanced environmental cleaning, patient cohorting, contact precautions, active surveillance cultures, and admission/transfer restrictions as clinically appropriate.
4. **Investigate Source** -- Conduct an epidemiologic investigation to identify the source of the outbreak, including environmental cultures, common exposure analysis, and molecular typing if available.
5. **Monitor and Resolve** -- Continue surveillance until no new cases are identified for an appropriate period (typically 2 incubation periods). Update the outbreak status to CONTAINED and then RESOLVED when criteria are met.

### Tab 3: Antibiogram

The Antibiogram tab displays a matrix view of cumulative antibiotic susceptibility data for the facility, organized by organism and reporting period.

| Row | Description |
|-----|-------------|
| Organism | Each row represents a bacterial pathogen (e.g., E. coli, S. aureus, P. aeruginosa). |

| Column | Description |
|--------|-------------|
| Antibiotic | Each column represents an antibiotic tested (e.g., Ampicillin, Ciprofloxacin, Vancomycin). |
| Cell Value | Percentage of isolates susceptible to the antibiotic for that organism during the reporting period. |

Color coding:

| Susceptibility % | Color | Interpretation |
|-------------------|-------|----------------|
| 80% or higher | Green | Good susceptibility. Appropriate for empiric therapy. |
| 60% to 79% | Yellow | Moderate susceptibility. Use with caution for empiric therapy. |
| Below 60% | Red | Poor susceptibility. Avoid for empiric therapy unless culture-confirmed. |

> **Note:** The antibiogram is critical for empiric therapy decisions. Providers use it to select appropriate antibiotics before culture and susceptibility results are available. The antibiogram should be updated at least annually and distributed to all prescribers. Infection Control should review the antibiogram with the Antimicrobial Stewardship Committee quarterly.

![Antibiogram matrix showing organism-antibiotic susceptibility percentages](screenshots/quality-antibiogram.png)

---

## Suicide Prevention (/suicide-prevention)

The Suicide Prevention module supports the facility's suicide prevention program by managing the high-risk roster, safety plans, and follow-up tracking.

> **Note:** This module is shared with the Mental Health clinical module. See the clinician documentation at [clinician/mental-health.md](../clinician/mental-health.md) for the full clinical perspective on suicide prevention.

### Tab 1: High-Risk Roster

The High-Risk Roster lists all patients currently flagged as at elevated risk for suicide, with their assigned risk level.

| Risk Level | Badge Color | Description |
|------------|-------------|-------------|
| Level 1 - Acute | Red | Immediate risk. Patient requires constant observation and crisis intervention. |
| Level 2 - High | Orange | High chronic risk. Active safety plan required. Frequent follow-up (within 7 days). |
| Level 3 - Intermediate | Yellow | Moderate risk. Safety plan in place. Follow-up within 30 days. |
| Level 4 - Low | Blue | Low but identified risk. Periodic screening and follow-up. |
| Level 5 - Minimal | Green | Minimal risk. No active safety concerns. Standard screening schedule. |

> **Warning:** Patients at Risk Level 1 (Acute) require immediate clinical intervention. If you identify a Level 1 patient who is not receiving active care, notify the Mental Health service and the Suicide Prevention Coordinator immediately.

### Tab 2: Safety Plans

Safety plans are structured documents created collaboratively with patients to help them manage suicidal thoughts. Each plan includes warning signs, coping strategies, reasons for living, social supports, professional contacts, and environmental safety measures.

### Tab 3: Follow-Up Tracking

Follow-up tracking ensures that patients on the high-risk roster receive timely outreach and clinical follow-up.

| Column | Description |
|--------|-------------|
| Patient | Patient name and ID. |
| Risk Level | Current risk level. |
| Last Contact | Date of the most recent clinical contact. |
| Next Follow-Up Due | Date the next follow-up contact is required. |
| Status | ON_TRACK, DUE, or OVERDUE. |
| Assigned Coordinator | Suicide prevention coordinator responsible for this patient. |

---

## Clinical Case Registries (/clinical-registries)

The Clinical Case Registries module manages disease-specific registries for population health tracking and quality improvement.

### Available Registries

| Registry | Description |
|----------|-------------|
| HIV | Patients with HIV/AIDS, tracking viral load, CD4 counts, ART adherence, and care engagement. |
| Hepatitis C (HepC) | Patients with hepatitis C, tracking treatment status, SVR achievement, and fibrosis staging. |
| Diabetes | Patients with diabetes, tracking HbA1c, foot exams, eye exams, nephropathy screening, and cardiovascular risk. |
| Asthma | Patients with asthma, tracking controller medication use, exacerbation frequency, and spirometry results. |

### Enrolled Patients Tab

Each registry has an enrolled patients tab showing all patients currently in the registry with their key clinical indicators.

| Column | Description |
|--------|-------------|
| Patient | Patient name and ID. |
| Enrollment Date | Date the patient was added to the registry. |
| Last Visit | Date of the most recent clinical encounter related to the registry condition. |
| Key Indicators | Registry-specific clinical values (e.g., HbA1c for Diabetes, viral load for HIV). |
| Care Gaps | Identified care gaps (overdue screenings, missed appointments, etc.). |
| Status | ACTIVE, INACTIVE, or DECEASED. |

### Dashboard

The registry dashboard provides aggregate metrics including:

- **Total Enrolled** -- Number of patients in the registry.
- **Enrollment Trends** -- Chart showing enrollment over time.
- **Care Gap Summary** -- Number and percentage of patients with identified care gaps.
- **Outcome Metrics** -- Registry-specific outcomes (e.g., percentage of HIV patients with undetectable viral load, percentage of diabetes patients with HbA1c below 9%).

---

## Polytrauma / TBI (/polytrauma)

The Polytrauma and Traumatic Brain Injury (TBI) module supports screening, registry management, and clinical coordination for veterans with polytraumatic injuries and TBI.

### Tab 1: TBI Screenings

The TBI Screenings tab tracks the administration and results of the VA TBI screening tool.

| Column | Description |
|--------|-------------|
| Patient | Patient name and ID. |
| Screening Date | Date the TBI screening was administered. |
| Screener | Staff member who administered the screen. |
| Result | POSITIVE, NEGATIVE, or INDETERMINATE. |
| Follow-Up | Whether a follow-up comprehensive evaluation has been scheduled or completed (for positive screens). |

| Result | Description |
|--------|-------------|
| POSITIVE | Screening criteria met. Patient requires a comprehensive TBI evaluation. |
| NEGATIVE | Screening criteria not met. No further TBI evaluation needed at this time. |
| INDETERMINATE | Screening could not be completed or results are equivocal. Rescreening or clinical evaluation recommended. |

### Tab 2: Polytrauma Registry

The Polytrauma Registry tracks patients with multiple traumatic injuries requiring coordinated rehabilitation care.

| Column | Description |
|--------|-------------|
| Patient | Patient name and ID. |
| Injury Date | Date of the polytraumatic injury. |
| Injury Types | Categories of injuries (TBI, orthopedic, amputation, burns, sensory, psychological). |
| Polytrauma Level | Level of polytrauma system care (I, II, III, IV, or V based on facility designation). |
| Rehabilitation Status | ACTIVE, MAINTENANCE, or COMPLETED. |
| Care Coordinator | Assigned polytrauma care coordinator. |

### Tab 3: Dashboard

The Polytrauma Dashboard provides metrics for the polytrauma program:

| Metric | Description |
|--------|-------------|
| Total TBI Screens | Number of TBI screenings administered. |
| Positive Rate | Percentage of screenings with POSITIVE results. |
| Registry Enrollment | Number of patients in the polytrauma registry. |
| Active Rehabilitation | Number of patients in active rehabilitation programs. |
| Consult Completion | Percentage of TBI-positive patients who completed comprehensive evaluation. |

---

## Audit Trail (/audit-trail)

The Audit Trail module is shared with Health Information Management (HIM) and is documented in detail in [him.md](him.md). In the context of quality and safety, the audit trail is used for:

- **Incident Investigation** -- Reviewing system access and data changes related to a patient safety incident.
- **Privacy Breach Assessment** -- Determining whether unauthorized access to patient records occurred.
- **Compliance Monitoring** -- Verifying that sensitive record access is appropriate and justified.
- **Peer Review Documentation** -- Tracking access to peer review records to ensure 38 U.S.C. 5705 protections are maintained.

---

## Tips and Best Practices

> **Tip:** Encourage a culture of reporting by emphasizing that incident reports are for system improvement, not individual punishment. High reporting rates correlate with safer facilities.

> **Tip:** File incident reports as close to the time of the event as possible. Delayed reporting leads to less accurate descriptions and may miss contributing factors.

> **Tip:** For sentinel events, begin the RCA within 72 hours while details are fresh and witnesses are available. Do not wait for the 45-day deadline to begin work.

> **Tip:** Update the antibiogram at least annually and distribute it to all prescribing providers. Outdated antibiograms lead to inappropriate empiric therapy and contribute to antibiotic resistance.

> **Tip:** Review the high-risk suicide prevention roster daily. Patients who miss follow-up appointments should trigger immediate outreach.

> **Tip:** When investigating infection outbreaks, involve Environmental Services early. Environmental contamination is a frequently identified source that can be addressed quickly with enhanced cleaning protocols.

> **Tip:** Congressional inquiry responses should be factual, concise, and free of clinical jargon. Have the response reviewed by the Patient Advocate and Public Affairs before submission.

> **Tip:** Use clinical registry care gap reports to drive proactive outreach. Contacting patients before they become overdue on screenings improves outcomes and reduces the workload of managing overdue patients.

---

## Screenshots Reference

The following screenshots are referenced throughout this section:

- ![Incident report form with category and severity fields](screenshots/quality-incident-form.png)
- ![Severity badges from Near Miss through Sentinel Event](screenshots/quality-severity-badges.png)
- ![Peer review outcome with determination](screenshots/quality-peer-review.png)
- ![HAI case detail with pathogen and susceptibility](screenshots/quality-hai-case.png)
- ![Antibiogram matrix with susceptibility percentages](screenshots/quality-antibiogram.png)
- ![Patient complaint tracking with categories and status](screenshots/quality-complaint-tracking.png)
- ![High-risk suicide prevention roster with risk level badges](screenshots/quality-high-risk-roster.png)
