# ADT and Bed Management

This module covers Admission, Discharge, and Transfer (ADT) operations, bed board management, and Emergency Department patient tracking. Together these modules provide real-time visibility into patient flow, bed availability, and ward census across the facility.

**Intended Audience:** ADT Coordinators, Bed Control Staff, Nursing Supervisors, ED Registration Staff, and House Officers.

**VistA File References:** File #40.8 (ADT/Transfer), File #405 (Patient Movement), File #42 (Ward Location).

**Primary Routes:** `/adt`, `/beds`, `/ed`.

---

## ADT (/adt)

The ADT module is the central hub for managing all patient movements within the facility: admissions, transfers between wards, and discharges.

> **Warning:** Always verify patient identity using at least two identifiers (name plus DOB, or name plus last-4 SSN) before performing any ADT operation. Incorrect patient movements can have serious clinical and safety consequences.

### Tab 1: Patient Movements

The Patient Movements tab displays a chronological log of all patient movement events at the facility.

| Column | Description |
|--------|-------------|
| Patient Name | Full name of the patient. |
| Patient ID | System-assigned patient identifier. |
| Movement Type | ADMISSION, TRANSFER, or DISCHARGE. |
| From Location | Ward or location the patient moved from (blank for admissions). |
| To Location | Ward or location the patient moved to (blank for discharges). |
| Date/Time | Date and time the movement was recorded. |
| Status | ADMITTED, TRANSFERRED, or DISCHARGED. |
| Provider | Attending or responsible provider at the time of movement. |
| Entered By | Staff member who recorded the movement. |

The table can be filtered by date range, movement type, ward, and status.

![ADT patient movements log showing admissions, transfers, and discharges](screenshots/adt-patient-movements.png)

### Tab 2: Ward Census

The Ward Census tab provides a real-time view of all patients currently located on a specific ward. Select a ward from the dropdown to see its current patients.

| Column | Description |
|--------|-------------|
| Patient Name | Full name of the patient. |
| Patient ID | System-assigned identifier. |
| Bed | Assigned bed within the ward. |
| Admission Date | Date of the patient's admission to the facility. |
| Transfer Date | Date of the most recent transfer to this ward (if different from admission). |
| Attending Provider | Current attending physician. |
| Diagnosis | Primary admitting or working diagnosis. |
| Days on Ward | Number of days the patient has been on this ward. |

> **Tip:** The ward census is updated in real time as movements are recorded. Use this view for shift handoff and daily rounding preparation.

### Tab 3: Ward Directory

The Ward Directory tab lists all wards in the facility with their configuration and capacity.

| Column | Description |
|--------|-------------|
| Ward Name | Display name of the ward (e.g., 4 East, ICU, SICU). |
| Ward Type | ACUTE, ICU, STEP_DOWN, REHAB, LONG_TERM, DOMICILIARY, PSYCHIATRIC. |
| Total Beds | Total bed capacity of the ward. |
| Available Beds | Number of currently available beds. |
| Occupancy Rate | Percentage of beds currently occupied. |
| Status | OPEN (accepting patients) or CLOSED (not accepting patients). |

### Key Actions

#### Admitting a Patient

1. Open the ADT module and click **Admit Patient**.
2. Enter or look up the Patient ID.
3. Verify enrollment and eligibility status.
4. Select the admitting ward and bed assignment.
5. Enter the admitting diagnosis.
6. Assign the attending provider.
7. Enter the admission date and time (defaults to current date/time).
8. Click **Admit** to record the admission.
9. The patient now appears on the ward census for the assigned ward.

> **Note:** Admissions require an active enrollment status. If the patient is not enrolled, complete registration and enrollment first (see [registration.md](registration.md)).

#### Recording a Transfer

1. Locate the patient in the Patient Movements tab or Ward Census.
2. Click **Transfer** on the patient's row.
3. Select the destination ward and bed.
4. Enter the reason for transfer.
5. Update the attending provider if the transfer involves a change of service.
6. Click **Transfer** to record the movement.
7. The patient's ward census entry moves from the origin ward to the destination ward.

#### Discharging a Patient

1. Locate the patient in the Patient Movements tab or Ward Census.
2. Click **Discharge** on the patient's row.
3. Select the discharge disposition (Home, Skilled Nursing Facility, Against Medical Advice, Expired, Transfer to Another Facility, etc.).
4. Enter the discharge date and time (defaults to current date/time).
5. Verify that all discharge orders have been completed.
6. Click **Discharge** to record the discharge.
7. The patient is removed from the ward census and the bed becomes available (after cleaning).

> **Tip:** Ensure discharge orders (medications, follow-up appointments, referrals) are completed before recording the discharge. The discharge action in ADT does not validate pending orders.

---

## Bed Management (/beds)

The Bed Management module provides a visual bed board and operational statistics for facility-wide bed availability and utilization.

### Tab 1: Bed Board

The bed board displays a visual grid of all facility beds, organized by ward and room. Each bed is represented as a card showing the bed identifier, current status, and patient name (if occupied).

![Bed board showing color-coded bed status across multiple wards](screenshots/beds-bed-board.png)

#### Bed Status Colors

| Color | Status | Description |
|-------|--------|-------------|
| Green | Available | Bed is clean, ready, and available for patient assignment. |
| Blue | Occupied | Bed is currently assigned to a patient. The patient's name and admitting diagnosis are displayed. |
| Yellow | Reserved | Bed has been reserved for a pending admission or transfer but is not yet occupied. |
| Red | Blocked | Bed is blocked and cannot be assigned. Reasons include infection control isolation, equipment placement, or administrative hold. |
| Gray | Out of Service / Maintenance | Bed is offline for maintenance, repair, or renovation. Not available for patient assignment. |

#### Bed Status Lifecycle

```
AVAILABLE → RESERVED → OCCUPIED → CLEANING → AVAILABLE
                                            → BLOCKED
                                            → OUT_OF_SERVICE
```

| Transition | Trigger |
|------------|---------|
| AVAILABLE to RESERVED | Bed reserved for a pending admission or transfer. |
| RESERVED to OCCUPIED | Patient admitted or transferred into the bed. |
| OCCUPIED to CLEANING | Patient discharged or transferred out. Bed requires environmental services. |
| CLEANING to AVAILABLE | Environmental services confirms bed is clean and ready. |
| Any to BLOCKED | Administrative action to block the bed (infection control, etc.). |
| Any to OUT_OF_SERVICE | Maintenance or facilities management takes the bed offline. |
| BLOCKED/OUT_OF_SERVICE to AVAILABLE | Block or maintenance cleared by authorized staff. |

#### Managing Beds

To change a bed's status:

1. Click on the bed card in the bed board grid.
2. A detail panel opens showing the bed's full status, history, and assigned patient (if occupied).
3. Select the new status from the available options (options depend on current status).
4. If blocking a bed, enter the reason for the block.
5. Click **Update** to save the change.

> **Note:** Only users with bed management security keys can change bed statuses. Clinical staff can view the bed board but cannot modify it.

### Tab 2: Statistics

The Statistics tab provides aggregate bed utilization metrics for the facility.

| Metric | Description |
|--------|-------------|
| Total Beds | Total number of beds in the facility across all wards. |
| Available | Number of beds in AVAILABLE status, ready for assignment. |
| Occupied | Number of beds in OCCUPIED status. |
| Reserved | Number of beds in RESERVED status for pending admissions/transfers. |
| Blocked | Number of beds in BLOCKED status. |
| Out of Service | Number of beds in OUT_OF_SERVICE or MAINTENANCE status. |
| Cleaning | Number of beds awaiting environmental services. |
| Occupancy Rate | (Occupied / (Total - Out of Service - Blocked)) expressed as a percentage. |

Statistics can be filtered by ward, ward type, and date to view trends over time.

> **Tip:** Monitor the occupancy rate daily. When facility-wide occupancy exceeds 85%, consider activating surge capacity protocols and increasing discharge planning efforts.

---

## Emergency Department (/ed)

The Emergency Department module provides real-time tracking of patients from arrival through disposition, using the Emergency Severity Index (ESI) triage system.

### Tab 1: Tracking Board

The ED Tracking Board is the primary operational view for ED staff, showing all patients currently in the Emergency Department.

![ED tracking board showing patients with ESI triage levels and status](screenshots/ed-tracking-board.png)

#### Tracking Board Columns

| Column | Description |
|--------|-------------|
| Patient Name | Full name or alias if unidentified. |
| Patient ID | System-assigned identifier (or temporary ID for unregistered patients). |
| Arrival Time | Date and time the patient arrived in the ED. |
| ESI Level | Emergency Severity Index triage level (1-5). |
| Chief Complaint | Primary complaint or reason for visit. |
| Location | Current location within the ED (triage, bed number, waiting room). |
| Provider | Assigned emergency provider. |
| Status | Current status in the ED workflow. |
| Time in ED | Elapsed time since arrival. |
| Disposition | Discharge, admit, transfer, or pending. |

#### ESI Triage Levels

The Emergency Severity Index (ESI) is a five-level triage system used to prioritize patients based on acuity and expected resource needs.

| ESI Level | Name | Color | Description | Response |
|-----------|------|-------|-------------|----------|
| 1 | Immediate | Red | Life-threatening condition requiring immediate intervention (cardiac arrest, severe trauma, airway compromise). | Immediate resuscitation. No waiting. |
| 2 | Emergent | Orange | High-risk situation, confused/lethargic/disoriented, severe pain/distress (chest pain, stroke symptoms, acute psychosis). | Seen within 10 minutes. |
| 3 | Urgent | Yellow | Stable but requires multiple resources (labs, imaging, IV medications, specialty consult). | Seen as soon as possible based on availability. |
| 4 | Less Urgent | Green | Stable, requires one resource (simple laceration repair, single X-ray, prescription). | May wait. Lower priority than levels 1-3. |
| 5 | Non-Urgent | Blue | Stable, requires no resources beyond examination (medication refill, minor complaint, chronic issue). | Longest acceptable wait. Consider alternatives to ED. |

> **Warning:** ESI Level 1 and Level 2 patients require immediate priority and must be seen ahead of all other patients regardless of arrival time. Delays in treating Level 1 and 2 patients can result in death or permanent disability. If a Level 1 patient arrives, all available resuscitation resources must be mobilized immediately.

#### Status Workflow in the ED

```
Arrived → Triaged → Assigned → In Treatment → Disposition Determined → Discharged / Admitted / Transferred
```

### Tab 2: Register Patient

The Register Patient tab is used to register new ED arrivals in the system.

1. If the patient is known, enter their Patient ID or search by name/DOB.
2. If the patient is unknown (unidentified, altered mental status), create a temporary registration with available information.
3. Enter the chief complaint.
4. Record the arrival date and time.
5. Assign the initial ESI triage level (performed by triage nurse).
6. Assign a location within the ED (triage area, waiting room, or bed).
7. Click **Register** to add the patient to the tracking board.

> **Note:** For unidentified patients, use a temporary identifier (e.g., "Doe, John" with a system-generated temporary ID). Update the record with the actual identity as soon as it is established.

### Tab 3: ED Statistics

The ED Statistics tab provides operational performance metrics essential for quality management and throughput optimization.

| Metric | Description |
|--------|-------------|
| Average Wait Time | Mean time from arrival to first provider contact, broken down by ESI level. |
| Bed Occupancy | Current percentage of ED beds occupied. |
| LWBS Rate | Left Without Being Seen rate, expressed as a percentage of all ED visits. |
| Door-to-Provider Time | Median time from patient arrival to first physician or advanced practice provider evaluation. |
| Boarding Time | Average time admitted patients spend in the ED waiting for an inpatient bed, from disposition decision to actual bed assignment. |
| Patients in Waiting Room | Current count of patients awaiting a bed or provider. |
| Average Length of Stay | Mean total time patients spend in the ED from arrival to departure. |
| Admissions per Hour | Rolling rate of ED patients admitted to inpatient units. |

> **Tip:** Door-to-provider time above 30 minutes for ESI Level 2 patients or above 60 minutes for ESI Level 3 patients indicates a throughput bottleneck. Investigate causes (bed availability, staffing, boarding) and consider activating surge protocols.

![ED statistics dashboard showing wait times, occupancy, and throughput metrics](screenshots/ed-statistics.png)

---

## Common Workflows

### Emergency Admission from the ED

1. In the ED Tracking Board, locate the patient whose disposition has been determined as "Admit."
2. Click **Admit** on the patient's row.
3. The system opens the ADT admission form pre-populated with the patient's ED information.
4. Select the admitting ward, bed, and attending provider.
5. Enter the admitting diagnosis.
6. Complete the admission. The patient is removed from the ED tracking board and appears on the ward census.

### Intra-Facility Transfer

1. Open the ADT module and locate the patient in the Ward Census for their current ward.
2. Click **Transfer**.
3. Select the destination ward and bed using the bed board to identify available beds.
4. Enter the reason for transfer and update the attending provider if applicable.
5. Confirm the transfer. The patient moves from the origin ward census to the destination ward census.

### Discharge Planning

1. Receive notification from the clinical team that the patient is ready for discharge.
2. Verify that all discharge orders are completed (prescriptions, follow-up appointments, referrals).
3. Open the ADT module and locate the patient.
4. Click **Discharge** and select the disposition (Home, SNF, etc.).
5. Record the discharge time.
6. The bed status changes to CLEANING and environmental services is notified.

### Bed Turnaround After Discharge

1. When a patient is discharged, the bed status automatically changes to CLEANING.
2. Environmental services staff clean and prepare the bed.
3. After cleaning, environmental services updates the bed status to AVAILABLE in the Bed Management module.
4. The bed now appears as green (available) on the bed board and is available for the next admission or transfer.

---

## Tips and Best Practices

> **Tip:** Use the bed board proactively during morning and afternoon bed huddles. Identifying expected discharges early in the day improves afternoon admission flow.

> **Tip:** Block beds promptly when infection control isolation is required. Delayed blocking can result in inadvertent exposure.

> **Tip:** Review ED boarding times regularly. Boarding times above 4 hours are associated with adverse outcomes and indicate the need for inpatient capacity management interventions.

> **Tip:** For mass casualty or surge situations, the ED tracking board can be used in conjunction with the Emergency Operations module (see [emergency.md](emergency.md)) to coordinate patient flow across the facility.

> **Warning:** Never discharge a patient from ADT without confirming with the clinical team that the discharge is authorized. ADT discharge is an administrative action that does not replace clinical discharge orders.

> **Tip:** When reserving beds for planned admissions (e.g., scheduled surgeries), set the reservation early in the day to ensure the bed is not assigned to an unplanned admission.

---

## Screenshots Reference

The following screenshots are referenced throughout this section:

- ![ADT patient movements log](screenshots/adt-patient-movements.png)
- ![Bed board with color-coded bed statuses](screenshots/beds-bed-board.png)
- ![ED tracking board with ESI triage levels](screenshots/ed-tracking-board.png)
- ![ED statistics dashboard](screenshots/ed-statistics.png)
