# Scheduling & Appointments -- Physician CharUI Human Test Script

## Prerequisites

- **Login:** DOCTOR1 / Password: `smythVista1`
- **Security Keys:** PROVIDER, ORES, TIU SIGN, GMRA ALLERGY, GMRV VITALS, GMPL PROBLEM
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

- A table displays with columns: #, Date/Time, Clinic, Provider, Status.
- Only future appointments with Status: Scheduled or CheckedIn appear.
- Sorted by date/time ascending.

---

## Scenario 2: View All Appointments

### Steps

1. At the Appointments menu, type: `2` (All Appointments).

### Expected Result

- A table displays ALL appointments regardless of status and date.
- Includes past appointments (COMPLETED, CANCELLED, NO SHOW) and future ones.
- Columns: #, Date/Time, Clinic, Provider, Status (or Type).

---

## Scenario 3: Schedule a New Appointment -- Regular (Happy Path)

### Steps

1. At the Appointments menu, type: `3` (Schedule New Appointment).
2. The terminal displays available clinics:
   ```
   Available Clinics:
   1. PRIMARY CARE CLINIC A
   2. CARDIOLOGY CLINIC
   3. MENTAL HEALTH CLINIC
   4. SURGERY CLINIC
   5. WOMEN'S HEALTH CLINIC
   6. URGENT CARE
   ```
3. Enter the following field-by-field:

| Prompt | Value to Enter |
|--------|----------------|
| Clinic Name | `PRIMARY CARE CLINIC A` |
| Appointment Date | `04/15/2026` |
| Time (HH:mm) | `10:30` |
| Duration (minutes) | `30` (or press Enter for default) |
| Purpose (optional) | `Hypertension follow-up, 3-month check` |
| Type (REGULAR, WALKIN, TELEPHONE) | `REGULAR` (or press Enter for default) |

4. At the confirmation prompt `Schedule this appointment?`, type: `Y`.

### Expected Result

- The terminal displays: `Appointment scheduled: [appointment-ID]`
- Returns to the Appointments menu.
- Verify by listing upcoming appointments (option 1) -- the new appointment appears.

---

## Scenario 4: Schedule a Walk-In Appointment

### Steps

1. At the Appointments menu, type: `3`.
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Clinic Name | `URGENT CARE` |
| Appointment Date | `T` (Today) |
| Time (HH:mm) | `14:00` |
| Duration (minutes) | `20` |
| Purpose (optional) | `Acute URI symptoms` |
| Type | `WALKIN` |

3. Confirm: `Y`

### Expected Result

- Appointment scheduled with Type = WALKIN for today.

---

## Scenario 5: Schedule a Telephone Appointment

### Steps

1. At the Appointments menu, type: `3`.
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Clinic Name | `PRIMARY CARE CLINIC A` |
| Appointment Date | `T+7` (7 days from today) |
| Time (HH:mm) | `09:00` (or press Enter for default) |
| Duration (minutes) | `15` |
| Purpose (optional) | `Lab results review, telephone follow-up` |
| Type | `TELEPHONE` |

3. Confirm: `Y`

### Expected Result

- Appointment scheduled with Type = TELEPHONE.

---

## Scenario 6: Schedule Appointment -- Minimal Fields

### Steps

1. At the Appointments menu, type: `3`.
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Clinic Name | `CARDIOLOGY CLINIC` |
| Appointment Date | `04/20/2026` |
| Time (HH:mm) | (press Enter for default: 09:00) |
| Duration (minutes) | (press Enter for default: 30) |
| Purpose (optional) | (press Enter to skip) |
| Type | (press Enter for default: REGULAR) |

3. Confirm: `Y`

### Expected Result

- Appointment scheduled with defaults: Time 09:00, Duration 30 min, Type REGULAR, no purpose.

---

## Scenario 7: Cancel Scheduling an Appointment

### Steps

1. At the Appointments menu, type: `3`.
2. Fill in fields with test data.
3. At the confirmation prompt `Schedule this appointment?`, type: `N`.

### Expected Result

- The appointment is NOT scheduled.
- Returns to the Appointments menu.

---

## Scenario 8: Check In a Patient (Happy Path)

### Steps

1. At the Appointments menu, type: `4` (Check In).
2. A numbered list of upcoming appointments appears.
3. Select the appointment by number.

### Expected Result

- The terminal displays: `Patient checked in.`
- The appointment status changes from SCHEDULED to CHECKED IN.
- Verify by listing upcoming appointments -- the status column shows CHECKED IN.

---

## Scenario 9: Check In -- No Upcoming Appointments

### Steps

1. Ensure no upcoming appointments exist for this patient.
2. At the Appointments menu, type: `4`.

### Expected Result

- Empty list or message indicating no upcoming appointments.
- Returns to the Appointments menu.

---

## Scenario 10: Cancel an Appointment (Happy Path)

### Steps

1. At the Appointments menu, type: `5` (Cancel Appointment).
2. A numbered list of upcoming appointments appears.
3. Select the appointment to cancel.
4. At the confirmation prompt `Cancel this appointment?`, type: `Y`.

### Expected Result

- The terminal displays: `Appointment cancelled.`
- The appointment status changes to CANCELLED.
- It no longer appears in upcoming appointments.

---

## Scenario 11: Decline Cancelling an Appointment

### Steps

1. At the Appointments menu, type: `5`.
2. Select an appointment.
3. At the confirmation prompt `Cancel this appointment?`, type: `N`.

### Expected Result

- The appointment remains as-is.
- Returns to the Appointments menu.

---

## Scenario 12: Return to Main Menu

### Steps

1. At the Appointments menu, type `Q` or `^` to return.

### Expected Result

- Returns to the Main Menu with patient context preserved.
