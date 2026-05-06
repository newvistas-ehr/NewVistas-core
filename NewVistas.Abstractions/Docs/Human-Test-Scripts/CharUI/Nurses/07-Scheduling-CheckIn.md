# Scheduling & Check-In -- Nurse CharUI Human Test Script

## Prerequisites

- **Login:** NURSE1 / Password: `smythVista1`
- **Security Keys:** ORELSE, GMRV VITALS, GMRA ALLERGY, GMPL PROBLEM, SD SCHEDULING
- **Patient:** Select a patient with demo data loaded.
- **Pre-conditions:**
  1. SiloHost and WebServer running.
  2. Demo scheduling data loaded: `POST /api/scheduling/demo/load?patientId={patientId}`
  3. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: View Upcoming Appointments (Happy Path)

### Steps

1. At the Main Menu, type: `SC` and press Enter.
2. At the Appointments menu, type: `1` (Upcoming Appointments).

### Expected Result

- Table displays: #, Date/Time, Clinic, Provider, Status.
- Only future appointments with Scheduled/CheckedIn status.

---

## Scenario 2: View All Appointments

### Steps

1. At the Appointments menu, type: `2` (All Appointments).

### Expected Result

- All appointments displayed regardless of status and date.

---

## Scenario 3: Schedule a New Appointment -- Regular (Happy Path)

### Steps

1. At the Appointments menu, type: `3` (Schedule New Appointment).
2. Available clinics are displayed.
3. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Clinic Name | `PRIMARY CARE CLINIC A` |
| Appointment Date | `04/10/2026` |
| Time (HH:mm) | `09:00` |
| Duration (minutes) | `30` |
| Purpose (optional) | `Follow-up blood pressure check` |
| Type (REGULAR, WALKIN, TELEPHONE) | `REGULAR` |

4. Confirm: `Y`

### Expected Result

- `Appointment scheduled: [appointment-ID]`
- Verify in upcoming appointments list.

---

## Scenario 4: Schedule a Walk-In Appointment

### Steps

1. At the Appointments menu, type: `3`.
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Clinic Name | `URGENT CARE` |
| Appointment Date | `T` (Today) |
| Time (HH:mm) | `11:00` |
| Duration (minutes) | `20` |
| Purpose | `Wound check, suture removal` |
| Type | `WALKIN` |

3. Confirm: `Y`

### Expected Result

- Walk-in appointment scheduled for today.

---

## Scenario 5: Schedule a Telephone Visit

### Steps

1. At the Appointments menu, type: `3`.
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Clinic Name | `PRIMARY CARE CLINIC A` |
| Appointment Date | `T+3` |
| Time (HH:mm) | `14:00` |
| Duration (minutes) | `15` |
| Purpose | `Lab results callback` |
| Type | `TELEPHONE` |

3. Confirm: `Y`

### Expected Result

- Telephone appointment scheduled.

---

## Scenario 6: Cancel Scheduling

### Steps

1. At the Appointments menu, type: `3`.
2. Fill in fields.
3. At confirmation, type: `N`.

### Expected Result

- Appointment NOT scheduled.

---

## Scenario 7: Check In a Patient (Happy Path -- Primary Nursing Workflow)

### Steps

1. At the Appointments menu, type: `4` (Check In).
2. A numbered list of upcoming appointments appears.
3. Select the appointment by number.

### Expected Result

- `Patient checked in.`
- The appointment status changes to CHECKED IN.
- Verify in upcoming appointments -- status shows CHECKED IN.

---

## Scenario 8: Check In -- No Upcoming Appointments

### Steps

1. Ensure no upcoming appointments exist.
2. Type: `4` (Check In).

### Expected Result

- Empty list or message indicating no upcoming appointments.

---

## Scenario 9: Cancel an Appointment (Happy Path)

### Steps

1. At the Appointments menu, type: `5` (Cancel Appointment).
2. Select an upcoming appointment.
3. Confirm: `Y`

### Expected Result

- `Appointment cancelled.`
- Status changes to CANCELLED.

---

## Scenario 10: Decline Cancelling an Appointment

### Steps

1. Type: `5`.
2. Select an appointment.
3. At confirmation, type: `N`.

### Expected Result

- Appointment remains unchanged.

---

## Scenario 11: Return to Main Menu

### Steps

1. At the Appointments menu, type `Q` or `^`.

### Expected Result

- Returns to the Main Menu.
