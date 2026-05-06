# Scheduling and Appointments

This module covers appointment scheduling, clinic management, wait list administration, and patient recall operations. Scheduling is one of the most frequently used administrative modules, supporting both front-desk staff and clinic coordinators in managing patient access to care.

**Intended Audience:** Scheduling Clerks, Clinic Coordinators, AMEA Staff, and Supervisors.

**VistA File References:** File #44 (Hospital Location), File #44.4 (Appointment Wait List).

**Primary Routes:** `/scheduling`, `/appointment-waitlist`, `/patient-recall`.

---

## Scheduling (/scheduling)

The Scheduling module provides a 3-tab interface for managing appointments, booking new visits, and viewing clinic information.

### Tab 1: My Schedule

The My Schedule tab displays a table of all appointments for the logged-in user's clinic or assigned clinics.

#### Appointment Table Columns

| Column | Description |
|--------|-------------|
| Date/Time | Scheduled date and time of the appointment. |
| Clinic | Name of the clinic where the appointment is scheduled. |
| Provider | Assigned provider for the appointment. |
| Purpose | Reason for the visit (free-text or selected from templates). |
| Type | REGULAR, FOLLOW-UP, URGENT, or CONSULT. |
| Duration | Length of appointment in minutes. |
| Status | Current appointment status (see workflow below). |
| Actions | Available actions based on current status. |

#### Status Workflow

Appointments follow a defined status progression:

```
Scheduled → Checked In → Completed
                       → Cancelled
         → No-Show
```

| Status | Color | Description |
|--------|-------|-------------|
| Scheduled | Blue | Appointment is booked and confirmed. Patient has not yet arrived. |
| Checked In | Yellow | Patient has arrived and been checked in at the front desk. |
| Completed | Green | Visit has been completed. Patient has been checked out. |
| Cancelled | Red | Appointment was cancelled before the visit occurred. |
| No-Show | Gray | Patient did not arrive for the scheduled appointment and was not cancelled in advance. |

![Schedule view showing appointments with color-coded status indicators](screenshots/scheduling-status-colors.png)

#### Actions by Status

The available actions change based on the current appointment status:

| Current Status | Available Actions |
|----------------|-------------------|
| Scheduled | **Check In** -- Mark the patient as arrived. **Cancel** -- Cancel the appointment. **No-Show** -- Mark the patient as a no-show (use after the appointment time has passed). |
| Checked In | **Check Out** -- Complete the visit and mark as Completed. |
| Completed | No actions available (terminal status). |
| Cancelled | No actions available (terminal status). |
| No-Show | No actions available (terminal status). |

#### Performing a Check-In

1. Locate the patient's appointment in the schedule table.
2. Verify the patient's identity using at least two identifiers.
3. Click **Check In** in the Actions column.
4. Confirm the check-in in the dialog that appears.
5. The status changes to Checked In (yellow) and a timestamp is recorded.

> **Tip:** Check patients in promptly upon arrival. The check-in time is used for wait time calculations and clinic performance metrics.

#### Performing a Check-Out

1. After the visit is complete, locate the patient's appointment (status should be Checked In).
2. Click **Check Out** in the Actions column.
3. Confirm the check-out in the dialog.
4. The status changes to Completed (green) and the visit duration is recorded.

#### Recording a No-Show

1. After the scheduled appointment time has passed and the patient has not arrived, locate the appointment.
2. Click **No-Show** in the Actions column.
3. Confirm the no-show in the dialog.
4. The status changes to No-Show (gray).

> **Note:** No-show tracking is important for clinic utilization reporting and patient access management. Patterns of no-shows may trigger outreach or waitlist management actions.

### Tab 2: Schedule Appointment

The Schedule Appointment tab provides a form for booking new appointments.

![Appointment booking form showing required fields and conflict detection](screenshots/scheduling-booking-form.png)

#### Required Fields

| Field | Required | Description |
|-------|----------|-------------|
| Patient ID | Yes | The patient for whom the appointment is being booked. Use the patient lookup to find the correct ID. |
| Clinic | Yes | The clinic where the appointment will take place. Selected from a dropdown of active clinics. |
| Date/Time | Yes | The desired date and time. Must be in the future and within the clinic's operating hours. |
| Duration | Yes | Appointment length. Options: 15, 20, 30, 45, 60, or 90 minutes. Default is determined by the clinic's standard appointment length. |
| Provider | No | The specific provider to see. If left blank, any available provider in the clinic may be assigned. |
| Purpose | No | Reason for the visit. Free-text or selected from clinic-specific templates. |
| Type | No | REGULAR (default), FOLLOW-UP, URGENT, or CONSULT. |

#### Booking an Appointment

1. Open the Schedule Appointment tab.
2. Enter the Patient ID (or use the lookup button to search).
3. Select the Clinic from the dropdown.
4. Choose the desired Date and Time.
5. Select the appointment Duration.
6. Optionally specify a Provider, Purpose, and Type.
7. Click **Schedule**.
8. If no conflicts are detected, the appointment is created and a confirmation is displayed.
9. If a conflict is detected, a warning dialog appears (see Conflict Detection below).

#### Conflict Detection

The system performs two types of conflict checking before creating an appointment:

- **Daily Capacity Check** -- Verifies that the clinic has not exceeded its maximum number of appointments for the selected date.
- **Time-Slot Overlap Check** -- Verifies that the requested time slot does not overlap with an existing appointment for the same patient or provider.

If a conflict is detected:

1. A warning dialog appears describing the conflict.
2. For daily capacity conflicts, the options are to select a different date or to override (requires authorization).
3. For time-slot overlaps, the appointment cannot be double-booked unless the **emergency double-book override** is used.

> **Warning:** The emergency double-book override should only be used for urgent or emergent clinical needs. Overuse of double-booking degrades clinic operations and patient wait times. All double-book overrides are logged for supervisory review.

### Tab 3: Clinics

The Clinics tab provides a searchable directory of all clinics in the facility.

![Clinic directory showing searchable list with status indicators](screenshots/scheduling-clinic-directory.png)

#### Clinic Directory Columns

| Column | Description |
|--------|-------------|
| Clinic Name | Display name of the clinic. |
| Division | Facility division or building where the clinic is located. |
| Stop Code | VA Clinic Stop Code used for workload reporting and VERA credit. |
| Appointment Length | Default appointment duration in minutes for this clinic. |
| Status | ACTIVE (accepting appointments) or INACTIVE (not currently operational). |

The search bar filters the clinic list by name, division, or stop code. Only ACTIVE clinics are available for appointment booking.

---

## Appointment Wait List (/appointment-waitlist)

The Appointment Wait List module manages patients who are waiting for appointments when no suitable time slots are immediately available.

> **Note:** This feature requires the **APPOINTMENT_WAITLIST** feature flag to be enabled. Contact your system administrator if this module is not visible.

### Adding a Patient to the Wait List

1. Open the Appointment Wait List page.
2. Click **Add to Wait List**.
3. Complete the required and optional fields:

| Field | Required | Description |
|-------|----------|-------------|
| Patient ID | Yes | The patient to be wait-listed. |
| Clinic | Yes | The desired clinic. |
| Type | No | FOLLOW-UP, NEW, PROCEDURE, or CONSULT. |
| Priority | No | ROUTINE (default), URGENT, or STAT. |
| Preferred Provider | No | Specific provider requested by the patient. |
| Desired Date Range Start | No | Earliest acceptable appointment date. |
| Desired Date Range End | No | Latest acceptable appointment date. |
| Notes | No | Additional context about the wait list request. |

4. Click **Save** to add the patient to the wait list.

### Wait List Status Workflow

```
WAITING → OFFERED → BOOKED
                  → DECLINED → WAITING (re-enters queue)
       → CANCELLED
       → EXPIRED
```

| Status | Color | Description |
|--------|-------|-------------|
| WAITING | Blue | Patient is in the queue. No appointment has been offered yet. |
| OFFERED | Purple | An available slot has been identified and offered to the patient. |
| BOOKED | Green | Patient accepted the offered slot and an appointment has been created. |
| DECLINED | Orange | Patient declined the offered slot. The entry returns to WAITING status for the next available slot. |
| CANCELLED | Gray | Wait list entry cancelled by patient request or administrative action. |
| EXPIRED | Red | The desired date range has passed without a successful booking. Requires follow-up. |

![Wait list showing entries with priority badges and status colors](screenshots/scheduling-waitlist.png)

### Processing the Wait List

When a clinic slot becomes available:

1. Open the wait list for the relevant clinic.
2. Sort by priority (STAT first, then URGENT, then ROUTINE) and by date added.
3. Select the highest-priority patient and click **Offer Appointment**.
4. Contact the patient to offer the available slot.
5. If the patient accepts, click **Book** to create the appointment. The wait list status changes to BOOKED.
6. If the patient declines, click **Decline**. The status changes to DECLINED and then automatically returns to WAITING for the next available slot.

### Clinic Pending Tab

The Clinic Pending tab shows up to 50 wait list entries for a selected clinic, sorted by priority and wait time. This view is designed for daily wait list management by clinic coordinators.

### Dashboard

The wait list dashboard provides multi-filter search and aggregate metrics:

- **Total Waiting** -- Number of patients currently in WAITING status.
- **Average Wait Time** -- Mean number of days patients have been in the queue.
- **Conversion Rate** -- Percentage of wait list entries that result in booked appointments.
- **Expired Rate** -- Percentage of entries that expired without booking.

Filters are available for clinic, date range, priority, type, and status.

---

## Patient Recall (/patient-recall)

The Patient Recall module manages proactive outreach to patients for scheduled follow-up care. It is shared with the Registration module and is documented in detail here for scheduling context.

> **Note:** This feature requires the **PATIENT_RECALL** feature flag to be enabled. Contact your system administrator if this module is not visible.

### Recall Types

| Type | Description |
|------|-------------|
| FOLLOW-UP | Post-visit follow-up for ongoing conditions. |
| ANNUAL_EXAM | Yearly comprehensive examination. |
| LAB_RECHECK | Follow-up laboratory work to monitor values. |
| CHRONIC_CARE | Ongoing management of chronic conditions. |
| IMMUNIZATION | Scheduled vaccinations or booster doses. |
| SCREENING | Preventive screening (cancer, AAA, etc.). |
| PROCEDURE | Scheduled procedural follow-up. |

### Status Workflow

```
PENDING → LETTER_SENT → CONTACTED → APPOINTMENT_SCHEDULED → COMPLETED
                                                           → CANCELLED
       → OVERDUE
```

| Status | Description |
|--------|-------------|
| PENDING | Recall created. No outreach attempted yet. |
| LETTER_SENT | Recall letter mailed to patient's address on file. |
| CONTACTED | Patient reached by phone, secure message, or other means. |
| APPOINTMENT_SCHEDULED | Patient has a confirmed appointment for the recalled visit. |
| COMPLETED | Patient completed the recalled visit. |
| CANCELLED | Recall cancelled (patient declined, moved, deceased, etc.). |
| OVERDUE | Recall due date has passed without resolution. |

### Creating a Recall

1. Open the Patient Recall page.
2. Click **Add Recall**.
3. Enter the Patient ID, recall type, due date, responsible clinic, and notes.
4. Click **Save**.
5. The recall is created in PENDING status.

### Managing Recalls

From the recall list, staff can:

- **Send Letter** -- Generate and send a recall letter. Updates status to LETTER_SENT.
- **Log Contact** -- Record a phone call or secure message. Updates status to CONTACTED.
- **Link Appointment** -- Associate a booked appointment with the recall. Updates status to APPOINTMENT_SCHEDULED.
- **Complete** -- Mark the recall as completed after the patient's visit.
- **Cancel** -- Cancel the recall with a reason.

### Overdue Tab

The Overdue tab lists all recalls where the due date has passed and the status has not reached APPOINTMENT_SCHEDULED or COMPLETED. This list should be reviewed daily by clinic coordinators and acted upon promptly.

### Dashboard

The recall dashboard provides:

- **Total Active Recalls** -- Number of recalls in non-terminal status.
- **Overdue Count** -- Number of recalls past their due date.
- **Completion Rate** -- Percentage of recalls that reach COMPLETED status.
- **Trends** -- Charts showing recall volume and completion rates by clinic and type over time.

![Patient recall list with overdue indicators and status tracking](screenshots/scheduling-recall-list.png)

---

## Common Workflows

### Scheduling a New Patient Appointment

1. Search for the patient in Patient Lookup to verify identity and obtain the Patient ID.
2. Open the Scheduling module, Tab 2 (Schedule Appointment).
3. Enter the Patient ID, select the clinic, choose the date/time and duration.
4. Set the Type to REGULAR or NEW as appropriate.
5. Click **Schedule** and confirm no conflicts.
6. Provide the patient with appointment confirmation details (date, time, clinic location, any preparation instructions).

### Processing Morning Check-Ins

1. Open the Scheduling module, Tab 1 (My Schedule).
2. Filter to today's date.
3. As each patient arrives, verify identity and click **Check In**.
4. If the patient has a copay due, direct them to the cashier window before or after the visit per local policy.

### Handling a Cancelled Appointment with Wait List

1. When an appointment is cancelled, note the clinic, date, time, and duration of the freed slot.
2. Open the Appointment Wait List for that clinic.
3. Identify the highest-priority WAITING entry that matches the available slot.
4. Contact the patient and offer the slot.
5. If accepted, book the appointment. If declined, move to the next candidate.

---

## Tips and Best Practices

> **Tip:** Review the wait list daily, especially after cancellations. Prompt backfill of cancelled slots improves clinic utilization and reduces patient wait times.

> **Tip:** Use the URGENT and STAT priority levels on the wait list judiciously. Overuse diminishes their effectiveness for truly time-sensitive needs.

> **Tip:** When scheduling CONSULT-type appointments, verify that the consult request has been accepted by the receiving clinic before booking.

> **Tip:** Train patients to cancel appointments at least 24 hours in advance. This allows time for wait list backfill and reduces no-show rates.

> **Tip:** Review the recall Overdue tab weekly at minimum. Overdue recalls represent care gaps that may have clinical consequences.

---

## Screenshots Reference

The following screenshots are referenced throughout this section:

- ![Schedule view with color-coded appointment statuses](screenshots/scheduling-status-colors.png)
- ![Appointment booking form with conflict detection](screenshots/scheduling-booking-form.png)
- ![Clinic directory with searchable list](screenshots/scheduling-clinic-directory.png)
- ![Wait list with priority badges and status colors](screenshots/scheduling-waitlist.png)
- ![Patient recall list with overdue indicators](screenshots/scheduling-recall-list.png)
