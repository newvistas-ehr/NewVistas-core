# Patient Portal Scheduling -- Human Test Script

## Prerequisites

- **Login:** Patient portal account (Patient ID + password)
- **Feature Flag:** `PATIENT_SELF_SCHEDULING` must be enabled
- **Pre-conditions:**
  1. SiloHost, WebServer, and PatientPortal running.
  2. Patient registered in the portal (see PATIENT_PORTAL_GUIDE.md).
  3. Patient enrollment verified (status: Verified).
  4. At least one clinic configured with `AcceptsPatientSelfSchedule = true`.
  5. Enable features via API:
     ```
     POST /api/site-parameters/features/enable  →  { "featureName": "PATIENT_SELF_SCHEDULING" }
     ```

---

## Part A: Feature Status and Eligibility

### Scenario 1: Check Feature Status

### Steps

1. Call the feature status endpoint:
   ```
   GET /api/my/scheduling/feature-status
   ```
   (Requires patient JWT auth header)

### Expected Result

- Response: `{ "PatientSelfScheduling": true, "ProviderAvailability": false }`
- If `PatientSelfScheduling` is `false`, all scheduling endpoints will return errors.

---

### Scenario 2: Check Scheduling Eligibility

### Steps

1. Call the eligibility endpoint:
   ```
   GET /api/my/scheduling/eligibility
   ```

### Expected Result

- `IsEligible: true` for a patient with Verified enrollment and completed means test.
- Includes `EnrollmentStatus`, `PriorityGroup`, `CopayExempt` details.
- If not eligible, `Reasons` list explains why (e.g., "Enrollment not verified").

---

### Scenario 3: Feature Disabled -- All Endpoints Return Error

### Steps

1. Disable the feature:
   ```
   POST /api/site-parameters/features/disable  →  { "featureName": "PATIENT_SELF_SCHEDULING" }
   ```
2. Try to self-schedule:
   ```
   POST /api/my/scheduling/appointments
   Body: { "clinicId": "SD-CLINIC-001", "appointmentDateTime": "2026-04-20T10:00:00Z", "appointmentType": "REGULAR" }
   ```

### Expected Result

- 400 Bad Request: "Patient self-scheduling is not enabled for this site. Contact your care team to schedule appointments."

---

## Part B: Browse Clinics and Slots

### Scenario 4: View Available Clinics

### Steps

1. Call:
   ```
   GET /api/my/scheduling/clinics
   ```

### Expected Result

- List of clinics where `AcceptsPatientSelfSchedule = true` and `Status = ACTIVE`.
- Each entry shows: ClinicId, Name, AppointmentLength.
- Clinics with `AcceptsPatientSelfSchedule = false` do NOT appear.

---

### Scenario 5: View Available Slots for a Clinic

### Steps

1. Choose a clinic from Scenario 4.
2. Call:
   ```
   GET /api/my/scheduling/clinics/{clinicId}/slots?date=2026-04-20
   ```

### Expected Result

- List of available time slots for the date.
- Each slot shows: StartTime, EndTime, DurationMinutes, IsAvailable.
- Only available (unbooked) slots are returned.

---

## Part C: Self-Schedule an Appointment

### Scenario 6: Schedule REGULAR Appointment (Happy Path)

### Steps

1. Call:
   ```
   POST /api/my/scheduling/appointments
   Body:
   {
     "clinicId": "{clinicId from Scenario 4}",
     "appointmentDateTime": "2026-04-20T10:00:00Z",
     "purpose": "Annual physical exam",
     "appointmentType": "REGULAR"
   }
   ```

### Expected Result

- 201 Created with `{ "appointmentId": "APPT-..." }`.
- Appointment appears in `GET /api/my/scheduling/appointments`.

---

### Scenario 7: Schedule URGENT Type -- Rejected

### Steps

1. Call:
   ```
   POST /api/my/scheduling/appointments
   Body:
   {
     "clinicId": "{clinicId}",
     "appointmentDateTime": "2026-04-20T14:00:00Z",
     "purpose": "Chest pain",
     "appointmentType": "URGENT"
   }
   ```

### Expected Result

- 400 Bad Request: "Appointment type 'URGENT' requires staff scheduling. Please contact your care team."

---

### Scenario 8: Schedule at Non-Self-Schedule Clinic -- Rejected

### Steps

1. Use a clinic ID where `AcceptsPatientSelfSchedule = false`.
2. Attempt to schedule.

### Expected Result

- 400 Bad Request: "This clinic does not accept patient self-scheduling."

---

## Part D: View and Cancel Appointments

### Scenario 9: View Upcoming Appointments

### Steps

1. Call:
   ```
   GET /api/my/scheduling/appointments/upcoming
   ```

### Expected Result

- List of future appointments with status Scheduled or Checked In.
- Sorted by date ascending.
- Includes clinic name, provider name, time, purpose.

---

### Scenario 10: Cancel Appointment -- Outside Notice Window

### Steps

1. Use an appointment scheduled 7+ days in the future.
2. Call:
   ```
   PUT /api/my/scheduling/appointments/{appointmentId}/cancel
   Body: { "reason": "Schedule conflict" }
   ```

### Expected Result

- `IsAllowed: true`, `WasCancelled: true`.
- `IsWithinNoticeWindow: false` (more than 24 hours out).
- No `PolicyMessage`.
- Appointment status changes to Cancelled.

---

### Scenario 11: Cancel Appointment -- Within Notice Window (Late Cancel)

### Steps

1. Schedule an appointment for tomorrow (within 24 hours).
2. Cancel it:
   ```
   PUT /api/my/scheduling/appointments/{appointmentId}/cancel
   Body: { "reason": "Feeling better" }
   ```

### Expected Result

- `IsAllowed: true`, `WasCancelled: true`.
- `IsWithinNoticeWindow: true`.
- `PolicyMessage`: "This cancellation is within the 24-hour notice window. Your care team has been notified of the late cancellation."
- Cancellation reason includes `[LATE CANCEL]` prefix in staff view.

---

### Scenario 12: Reschedule Appointment

### Steps

1. Use a scheduled appointment.
2. Call:
   ```
   PUT /api/my/scheduling/appointments/{appointmentId}/reschedule
   Body: { "newDateTime": "2026-04-22T11:00:00Z", "reason": "Need later time" }
   ```

### Expected Result

- 200 OK.
- Appointment date/time updated.
- Verify via `GET /api/my/scheduling/appointments/{appointmentId}`.

---

## Part E: Waitlist (Requires APPOINTMENT_WAITLIST enabled)

### Scenario 13: Join Waitlist

### Steps

1. Enable waitlist:
   ```
   POST /api/site-parameters/features/enable  →  { "featureName": "APPOINTMENT_WAITLIST" }
   ```
2. Join waitlist:
   ```
   POST /api/my/scheduling/waitlist
   Body:
   {
     "clinicId": "{clinicId}",
     "desiredAppointmentType": "FOLLOW-UP",
     "desiredDateRangeStart": "2026-05-01T00:00:00Z",
     "desiredDateRangeEnd": "2026-06-30T00:00:00Z",
     "comments": "Need follow-up for blood pressure"
   }
   ```

### Expected Result

- 201 Created with wait list entry details.
- Priority is always ROUTINE (patients cannot set URGENT/STAT).
- Status is WAITING.

---

### Scenario 14: View My Waitlist Entries

### Steps

1. Call:
   ```
   GET /api/my/scheduling/waitlist
   ```

### Expected Result

- List of patient's waitlist entries with status, clinic, date range.

---

### Scenario 15: Accept Offered Slot

### Steps

1. (Staff must first offer a slot to the patient via the staff waitlist UI.)
2. Check waitlist entry — status should be "OFFERED" with an offered date/time.
3. Accept:
   ```
   PUT /api/my/scheduling/waitlist/{entryId}/accept
   ```

### Expected Result

- Status changes to BOOKED.
- An appointment is created at the offered date/time.

---

## Part F: Verification Checklist

- [ ] Feature status endpoint correctly reports enabled/disabled state
- [ ] All endpoints reject when PATIENT_SELF_SCHEDULING is disabled
- [ ] Only self-schedule clinics appear in clinic list
- [ ] Available slots endpoint returns open times
- [ ] REGULAR and FOLLOW-UP appointment types accepted
- [ ] URGENT and CONSULT types rejected with helpful error
- [ ] Cancellation outside notice window shows no warning
- [ ] Cancellation within notice window shows late-cancel warning
- [ ] Late-cancel reason includes [LATE CANCEL] prefix in staff view
- [ ] Reschedule updates appointment date/time
- [ ] Waitlist join sets priority to ROUTINE
- [ ] Waitlist accept creates appointment
- [ ] Patient cannot see/modify other patients' appointments
