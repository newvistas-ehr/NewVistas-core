# Emergency and Mass Casualty

This section covers the emergency department, mass casualty incident management, and event capture tools in NewVistas. These modules support emergency patient tracking, disaster response with START triage, resource management during mass casualty events, and clinical encounter documentation for workload reporting.

**Routes:** /ed, /mass-casualty, /event-capture

**Primary Roles:** Emergency Department Physicians, Emergency Nurses, Triage Officers, Incident Commanders, Event Capture Clerks, DSS Coordinators, Administrative Officers

---

## Emergency Department (/ed)

The Emergency Department page provides tools for patient tracking, registration, and operational statistics. This page is also covered in the ADT and Bed Management documentation (adt-bed-management.md) as it integrates with the facility's admission, discharge, and transfer workflows.

### Tracking Board

The ED Tracking Board displays all patients currently in the emergency department with their triage level, location, provider, and status.

#### ESI Triage Levels

The Emergency Severity Index (ESI) is used to prioritize patients based on acuity and expected resource needs. Each ESI level is color-coded on the tracking board.

| ESI Level | Color | Description | Expected Disposition |
|-----------|-------|-------------|---------------------|
| **ESI 1** | Red | Immediate life-saving intervention required | Resuscitation; highest priority |
| **ESI 2** | Orange | High-risk situation, confused/lethargic/disoriented, or severe pain/distress | Should not wait; immediate evaluation |
| **ESI 3** | Yellow | Stable but expected to require two or more resources (labs, imaging, IV fluids, etc.) | May wait for evaluation; moderate acuity |
| **ESI 4** | Green | Stable, expected to require one resource (e.g., single lab test, X-ray, or prescription) | May wait; lower acuity |
| **ESI 5** | Blue | Stable, expected to require no resources (e.g., medication refill, simple wound check) | May wait; lowest acuity |

The tracking board displays the following information for each patient:

- Patient name and identifier
- ESI triage level (color-coded)
- Chief complaint
- Arrival time
- Time in department
- Assigned bed/location
- Assigned provider
- Current status (Waiting, In Progress, Disposition Pending, Ready for Discharge, etc.)
- Nursing alerts and flags

> **Note:** The tracking board refreshes automatically. Patients are sorted by ESI level (highest acuity first) and then by arrival time within each level.

### Register Patient

Register a new patient arriving at the emergency department:

1. Click **Register Patient** on the ED page.
2. If the patient is already in the system, search by name, date of birth, or SSN (last 4).
3. If the patient is new, enter basic demographics (name, date of birth, gender, contact information).
4. Assign the initial **ESI triage level** based on the triage nurse's assessment.
5. Enter the **chief complaint**.
6. Assign a **bed/location** in the ED.
7. Assign a **provider**.
8. Click **Register** to add the patient to the tracking board.

> **Tip:** For patients arriving by ambulance, pre-registration may be started using information from the EMS report before the patient arrives.

### ED Statistics

The ED Statistics section provides operational metrics for department management:

| Metric | Description |
|--------|-------------|
| **Average Wait Time** | Mean time from arrival to first provider contact, by ESI level |
| **LWBS Rate** | Percentage of patients who Left Without Being Seen |
| **Door-to-Provider Time** | Average elapsed time from registration to initial physician evaluation |
| **Boarding Time** | Average time admitted patients spend waiting in the ED for an inpatient bed |
| **Current Census** | Number of patients currently in the ED |
| **Arrivals (24h)** | Number of patients who arrived in the last 24 hours |
| **Discharges (24h)** | Number of patients discharged from the ED in the last 24 hours |
| **Admissions (24h)** | Number of patients admitted to inpatient from the ED in the last 24 hours |

> **Tip:** Monitor LWBS rate and boarding time closely. High LWBS rates may indicate excessive wait times, while prolonged boarding times indicate inpatient capacity issues that need to be escalated.

---

## Mass Casualty (/mass-casualty)

The Mass Casualty page supports incident management during mass casualty incidents (MCIs), natural disasters, and other emergency events. It provides tools for incident creation, START triage, casualty tracking, resource management, and incident closure.

![Mass Casualty tracking board showing triage categories and patient dispositions](screenshots/mass-casualty-tracking-board.png)

### Incident Creation

When a mass casualty event occurs, an incident record must be created to coordinate the response.

#### Incident Fields

- **Incident Name** -- Descriptive name for the event (e.g., "Multi-Vehicle Accident I-95 Mile 42")
- **Incident Type** -- Classification of the event
- **Location** -- Where the event occurred
- **Date/Time** -- When the event began
- **Incident Commander** -- The person designated as Incident Commander per the facility's emergency operations plan
- **Status** -- Active or Closed

#### Incident Types

| Type | Description |
|------|-------------|
| **NATURAL_DISASTER** | Hurricane, earthquake, tornado, flood, wildfire, or other natural event |
| **MCI** | Mass Casualty Incident (multi-vehicle accident, building collapse, explosion, etc.) |
| **HAZMAT** | Hazardous materials release or exposure event |
| **ACTIVE_SHOOTER** | Active shooter or hostile event |
| **PANDEMIC** | Pandemic or large-scale infectious disease outbreak |
| **OTHER** | Events not fitting the above categories |

#### Creating an Incident

1. Navigate to the Mass Casualty page (/mass-casualty).
2. Click **New Incident**.
3. Enter the incident name, type, location, and date/time.
4. Designate the **Incident Commander**.
5. Click **Activate Incident**.

> **Warning:** Activating a mass casualty incident triggers facility-wide notifications and may initiate the emergency operations plan. Ensure this action is authorized by the appropriate authority before proceeding.

### START Triage

The Simple Triage and Rapid Treatment (START) system is used for initial field triage of mass casualty victims. START classifies patients into four categories based on their immediate clinical status.

#### Triage Categories

| Category | Tag Color | Designation | Criteria | Priority |
|----------|-----------|-------------|----------|----------|
| **IMMEDIATE** | Red | T1 | Life-threatening injuries that require immediate intervention; patient can survive with treatment | Highest -- treat first |
| **DELAYED** | Yellow | T2 | Serious injuries that are not immediately life-threatening; treatment can be delayed without significant risk | Second priority |
| **MINOR** | Green | T3 | Walking wounded; injuries that do not require immediate medical attention | Third priority -- "walking wounded" |
| **EXPECTANT** | Black | T4 | Injuries so severe that survival is unlikely even with treatment; comfort care only | Lowest -- palliative care |

![START triage tags showing color-coded categories](screenshots/start-triage-tags.png)

#### START Triage Algorithm

The START algorithm uses three simple assessments to categorize patients:

1. **Can the patient walk?**
   - Yes: Tag as MINOR (Green/T3)
   - No: Proceed to step 2

2. **Is the patient breathing?**
   - No: Open the airway. If breathing starts, tag as IMMEDIATE (Red/T1). If not, tag as EXPECTANT (Black/T4).
   - Yes: Check respiratory rate. If > 30 breaths/minute, tag as IMMEDIATE (Red/T1). If <= 30, proceed to step 3.

3. **Check perfusion (capillary refill or radial pulse):**
   - Capillary refill > 2 seconds or no radial pulse: Tag as IMMEDIATE (Red/T1)
   - Capillary refill <= 2 seconds and radial pulse present: Check mental status. If patient cannot follow simple commands, tag as IMMEDIATE (Red/T1). If patient can follow commands, tag as DELAYED (Yellow/T2).

#### Performing Triage

1. From the active incident, click **Triage Patient**.
2. Enter or scan the **Tag Number** (pre-printed triage tags).
3. Enter the patient's name if known (can be updated later for unidentified patients).
4. Select the **Triage Category** (IMMEDIATE, DELAYED, MINOR, or EXPECTANT) based on the START assessment.
5. Enter the **Chief Injury** (brief description of the primary injury).
6. Click **Save**.

> **Note:** During the initial surge, speed is critical. Enter only the minimum required information (tag number and triage category) during initial triage. Patient identification and detailed injury documentation can be completed during secondary assessment.

### Casualty Tracking

Once patients are triaged, the system tracks their movement and disposition throughout the event.

#### Tracking Fields

- **Tag Number** -- Unique identifier from the triage tag
- **Patient Name** -- Name of the casualty (may be "Unknown" initially)
- **Triage Category** -- Current START category (IMMEDIATE, DELAYED, MINOR, EXPECTANT)
- **Chief Injury** -- Primary injury description
- **Disposition** -- Current location/status of the casualty

#### Disposition Status

| Disposition | Description |
|-------------|-------------|
| **AT_SCENE** | Patient is still at the scene of the incident |
| **EN_ROUTE** | Patient is being transported to the facility |
| **ARRIVED** | Patient has arrived at the facility |
| **ADMITTED** | Patient has been admitted to inpatient care |
| **DISCHARGED** | Patient has been treated and released |
| **MORGUE** | Patient is deceased and has been moved to the morgue |

#### Re-Triage

Patient conditions may change during an MCI. Re-triage allows the category to be updated as conditions evolve.

1. Locate the patient on the casualty tracking board.
2. Click **Re-Triage**.
3. Select the new triage category based on the patient's current condition.
4. Document the reason for the category change.
5. Click **Save**.

> **Tip:** Re-triage regularly, especially for DELAYED (Yellow) patients. A DELAYED patient whose condition deteriorates should be re-triaged to IMMEDIATE. Conversely, an IMMEDIATE patient who has been stabilized may be re-triaged to DELAYED to free up resources.

### Resource Tracking

During a mass casualty event, resource availability must be monitored continuously to support decision-making.

![Resource tracking dashboard showing bed, staff, and supply availability](screenshots/mass-casualty-resource-tracking.png)

#### Tracked Resources

| Resource | Metrics |
|----------|---------|
| **ICU Beds** | Total capacity, currently occupied, available, surge capacity |
| **Med/Surg Beds** | Total capacity, currently occupied, available, surge capacity |
| **OR Availability** | Number of operating rooms available, staffed, and in use |
| **Staff** | Available physicians, nurses, and support staff by specialty |
| **Blood Products** | Inventory of blood products by type (O-neg, O-pos, A-pos, etc.) |
| **Transport Units** | Ambulances and other transport vehicles available |

#### Updating Resources

1. Click the **Resources** section on the active incident page.
2. Update the counts for each resource category as conditions change.
3. The dashboard automatically calculates remaining capacity and highlights critical shortages in red.

> **Warning:** Accurate resource tracking is essential for effective incident management. Designate a specific individual to maintain resource counts throughout the event. Inaccurate counts can lead to patients being directed to facilities that cannot accommodate them.

### Incident Closure

When the mass casualty event has been resolved, the incident should be formally closed.

#### Closing an Incident

1. Ensure all casualties have been dispositioned (all patients accounted for with a final disposition).
2. Click **Close Incident** on the incident page.
3. Review and confirm the **Final Counts by Category**:
   - Total IMMEDIATE (Red/T1) patients
   - Total DELAYED (Yellow/T2) patients
   - Total MINOR (Green/T3) patients
   - Total EXPECTANT (Black/T4) patients
   - Total fatalities
4. Document **Resource Utilization** -- summary of resources consumed during the event.
5. Document **Lessons Learned** -- observations about what worked well and what needs improvement.
6. Click **Confirm Closure**.

> **Note:** The incident closure report becomes a permanent record and is used for after-action reviews, quality improvement, and regulatory reporting. Complete it thoroughly.

---

## Event Capture (/event-capture)

The Event Capture page documents clinical encounters and procedures for workload reporting and cost allocation. Each encounter links to a Decision Support System (DSS) unit and captures procedure codes (CPT) and diagnoses (ICD-10).

![Event Capture form showing encounter details, procedures, and diagnoses](screenshots/event-capture-form.png)

### Encounters

Each encounter represents a single clinical interaction between a provider and a patient.

#### Encounter Fields

- **Patient** -- The patient involved in the encounter
- **Provider** -- The clinician who provided the service
- **Date/Time** -- When the encounter occurred
- **DSS Unit** -- The departmental unit under which the encounter is recorded
- **Encounter Type** -- Classification of the encounter

#### Encounter Types

| Type | Description |
|------|-------------|
| **FACE_TO_FACE** | In-person clinical encounter at the facility |
| **TELEPHONE** | Clinical encounter conducted by telephone |
| **TELEHEALTH** | Clinical encounter conducted via video telehealth |
| **GROUP** | Group session (e.g., group therapy, health education class) |
| **E_CONSULT** | Electronic consult (provider-to-provider consultation without patient present) |

### Procedure Codes (CPT)

Each encounter can include one or more procedure codes from the Current Procedural Terminology (CPT) system.

- **CPT Code** -- The numeric procedure code
- **Description** -- Description of the procedure
- **Quantity** -- Number of units (e.g., number of lesions treated, therapy units)
- **Modifiers** -- CPT modifiers that provide additional information about the service (e.g., -25 for significant, separately identifiable E/M service; -59 for distinct procedural service)

#### Adding a Procedure

1. In the encounter form, click **Add Procedure**.
2. Search for the CPT code by number or description.
3. Select the code from the search results.
4. Enter the **Quantity** (defaults to 1).
5. Add any applicable **Modifiers**.
6. Click **Save**.

### Diagnoses (ICD-10)

Each encounter can include one or more ICD-10 diagnosis codes.

- **ICD-10 Code** -- The alphanumeric diagnosis code
- **Description** -- Description of the diagnosis
- **Primary Indicator** -- Whether this is the primary diagnosis for the encounter (exactly one diagnosis must be marked as primary)
- **POA (Present on Admission)** -- For inpatient encounters, whether the condition was present at the time of admission. Values: Yes, No, Unknown, Clinically Undetermined, Exempt

#### Adding a Diagnosis

1. In the encounter form, click **Add Diagnosis**.
2. Search for the ICD-10 code by number or description.
3. Select the code from the search results.
4. If this is the primary reason for the encounter, check the **Primary** indicator.
5. For inpatient encounters, set the **POA** status.
6. Click **Save**.

### DSS Units

Decision Support System (DSS) units represent departmental accounting units used for workload reporting and cost allocation. Each encounter must be associated with a DSS unit.

#### Common DSS Stop Codes

| Stop Code | Description |
|-----------|-------------|
| **301** | General Internal Medicine |
| **322** | General Surgery |
| **323** | Orthopedic Surgery |
| **350** | Optometry |
| **401** | General Nursing |
| **502** | General Psychiatry |
| **509** | Substance Use Disorder |
| **524** | Social Work |
| **674** | Physical Therapy |

> **Note:** DSS stop codes determine how workload credit is allocated and directly affect facility funding calculations. Ensure the correct stop code is selected for each encounter.

### Recording an Encounter

1. Navigate to the Event Capture page (/event-capture).
2. Click **New Encounter**.
3. Select the **Patient** and **Provider**.
4. Enter the encounter **Date/Time**.
5. Select the **DSS Unit** (stop code) for the encounter.
6. Select the **Encounter Type** (Face to Face, Telephone, Telehealth, Group, or E-Consult).
7. Add one or more **Procedure Codes** with quantities and modifiers.
8. Add one or more **Diagnoses** and designate the primary diagnosis.
9. Review the encounter details for accuracy.
10. Click **Submit Encounter**.

> **Tip:** For recurring encounters (e.g., weekly therapy sessions), use the copy feature to pre-populate fields from a previous encounter. Update the date, any changed procedures, and diagnoses as needed.

---

## Common Workflows

### Mass Casualty Response

1. **Activate the incident** -- Create a new incident on the Mass Casualty page with the incident type, location, and Incident Commander.
2. **Begin START triage** -- As casualties arrive or are assessed at the scene, perform START triage and enter triage tags into the system.
3. **Track casualties** -- Monitor the tracking board as patients are transported, arrive at the facility, and are dispositioned.
4. **Monitor resources** -- Continuously update resource availability. Escalate to hospital leadership if critical resources are depleted.
5. **Re-triage as needed** -- Reassess patients whose conditions change and update their triage category.
6. **Document dispositions** -- Record the final disposition for each casualty (admitted, discharged, transferred, or deceased).
7. **Close the incident** -- Once all casualties are accounted for, close the incident with final counts, resource utilization, and lessons learned.

### ED to Inpatient Admission

1. **Register the patient** in the ED and assign an ESI triage level.
2. **Complete the ED evaluation** -- Document the encounter, procedures, and diagnoses using Event Capture.
3. **Determine disposition** -- If admission is required, request an inpatient bed through the ADT system.
4. **Transfer the patient** -- Update the tracking board when the patient leaves the ED for the inpatient unit.
5. **Complete ED documentation** -- Ensure all ED encounter documentation, orders, and results are finalized.

### Workload Capture for Clinic Encounters

1. At the end of each clinic session, navigate to Event Capture.
2. For each patient seen, create an encounter record with the appropriate DSS unit and encounter type.
3. Add all CPT procedure codes performed during the encounter.
4. Add the ICD-10 diagnoses addressed during the encounter, marking the primary diagnosis.
5. Submit all encounters. Review the daily workload summary to ensure accuracy.

---

## Tips and Best Practices

1. **Maintain accurate ESI triage.** Triage is the foundation of ED patient prioritization. Use the ESI algorithm consistently and re-triage patients whose conditions change while waiting.

2. **Practice mass casualty procedures regularly.** Conduct tabletop exercises and full-scale drills at least annually. Ensure all staff are familiar with the Mass Casualty page and START triage procedures.

3. **Keep the tracking board current.** An outdated tracking board undermines situational awareness. Designate staff to update patient statuses in real time during both routine ED operations and mass casualty events.

4. **Capture workload data promptly.** Event Capture encounters should be documented on the same day as the clinical encounter. Delayed data entry increases errors and affects facility workload reports.

5. **Verify DSS stop codes.** Incorrect stop codes result in misattributed workload. When in doubt, consult your DSS coordinator or ADPAC for the correct code.

6. **Use the correct encounter type.** Distinguish between face-to-face, telephone, telehealth, group, and e-consult encounters. Each type is counted differently in national workload reports.

7. **Always designate a primary diagnosis.** Every encounter must have exactly one primary ICD-10 diagnosis. The primary diagnosis drives clinical classification and reimbursement.

8. **Document lessons learned thoroughly.** After every mass casualty incident or drill, complete the lessons learned section. These observations drive improvements in future response capabilities.
