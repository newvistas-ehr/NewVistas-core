# Scheduling Enhancements -- Blazor Staff Human Test Script

## Prerequisites

- **Login:** DOCTOR1 / Password: `smythVista1`
- **Security Keys:** PROVIDER, ORES, SD SCHEDULING
- **Patient:** Select a patient with demo data loaded.
- **Pre-conditions:**
  1. SiloHost and WebServer running.
  2. Demo scheduling data loaded: `POST /api/scheduling/demo/load?patientId={patientId}`
  3. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Part A: Feature Gate Verification

### Scenario 1: Verify Default VistA Behavior (All Features Disabled)

**Purpose:** Confirm scheduling works in standard VistA mode without enhancements.

### Steps

1. Navigate to **Scheduling** page.
2. Verify the page loads with tabs: My Schedule, Schedule Appointment, Clinics.
3. Enter a patient ID and click **Load Schedule**.
4. Click the **Schedule Appointment** tab.
5. Select a clinic, pick a date/time, and click **Show Available Slots**.
6. Verify the slot grid shows 8:00 AM to 5:00 PM (standard VistA 8-17 grid).
7. Schedule an appointment within the grid.

### Expected Result

- Slot grid runs from 08:00 to 16:30 (18 x 30-minute slots).
- Appointment schedules successfully without any provider availability checks.
- No scheduling tier labels (PATIENT/STAFF) appear on slots.

---

### Scenario 2: Enable PROVIDER_AVAILABILITY Feature

### Steps

1. Enable the feature via API:
   ```
   POST /api/site-parameters/features/enable
   Body: { "featureName": "PROVIDER_AVAILABILITY" }
   ```
2. Refresh the Scheduling page.

### Expected Result

- Feature enabled confirmation (200 OK).
- Scheduling now checks provider availability when a provider is specified.

---

## Part B: Provider Availability Management

### Scenario 3: Set Up Provider Weekly Pattern

### Steps

1. Via API, add a weekly pattern for the provider:
   ```
   POST /api/providers/{providerId}/availability/patterns
   Body:
   {
     "clinicId": "SD-CLINIC-001",
     "clinicName": "PRIMARY CARE",
     "daysOfWeek": [1, 3, 5],
     "startHour": 8,
     "startMinute": 0,
     "endHour": 12,
     "endMinute": 0,
     "isActive": true
   }
   ```
   (DaysOfWeek: 1=Monday, 3=Wednesday, 5=Friday)

2. Query effective availability:
   ```
   GET /api/providers/{providerId}/availability/effective?clinicId=SD-CLINIC-001&date=2026-04-13
   ```
   (Use a Monday date)

### Expected Result

- Pattern created with a generated PatternId.
- Effective availability returns one window: 08:00-12:00 on the Monday.
- Querying for a Tuesday returns an empty list.

---

### Scenario 4: Add a Lunch Time Block

### Steps

1. Add a recurring daily lunch block:
   ```
   POST /api/providers/{providerId}/availability/blocks
   Body:
   {
     "blockType": "LUNCH",
     "startDateTime": "2026-04-13T00:00:00Z",
     "endDateTime": "2026-04-17T23:59:59Z",
     "isRecurringDaily": true,
     "recurringStartHour": 12,
     "recurringStartMinute": 0,
     "recurringEndHour": 13,
     "recurringEndMinute": 0,
     "reason": "Daily lunch break"
   }
   ```

2. Re-query effective availability for the provider on a day with an 8-17 pattern.

### Expected Result

- Block created with a generated BlockId.
- If the provider has an 8:00-17:00 pattern, effective availability shows two windows:
  - 08:00-12:00
  - 13:00-17:00
- The 12:00-13:00 lunch period is removed.

---

### Scenario 5: Schedule Within Provider Availability -- Success

### Steps

1. On the Scheduling page, select a clinic where the provider has availability.
2. Pick a date/time within the provider's availability window (e.g., Monday 10:00 AM).
3. Enter the provider name and click **Schedule**.

### Expected Result

- Appointment scheduled successfully.
- Appears in the schedule table with status "Scheduled".

---

### Scenario 6: Schedule Outside Provider Availability -- Rejection

### Steps

1. On the Scheduling page, select the same clinic.
2. Pick a date/time **outside** the provider's availability (e.g., Monday 3:00 PM if pattern is 8-12).
3. Enter the provider name and click **Schedule**.

### Expected Result

- Error message: "Requested time 15:00 is outside provider's availability at this clinic."
- The conflict warning panel appears with the Double Book Override checkbox.

---

### Scenario 7: Override with Double Book

### Steps

1. After the rejection in Scenario 6, check the **Double Book Override** checkbox.
2. Click **Schedule** again.

### Expected Result

- Appointment scheduled despite being outside availability.
- Confirmation message appears.

---

## Part C: Provider Unavailability Batch

### Scenario 8: Enable PROVIDER_UNAVAILABILITY_BATCH Feature

### Steps

1. Enable the feature:
   ```
   POST /api/site-parameters/features/enable
   Body: { "featureName": "PROVIDER_UNAVAILABILITY_BATCH" }
   ```
2. Verify feature status:
   ```
   GET /api/provider-unavailability/feature-status
   ```

### Expected Result

- `{ "Feature": "PROVIDER_UNAVAILABILITY_BATCH", "Enabled": true }`

---

### Scenario 9: Create Unavailability Event and Preview Affected

### Steps

1. Ensure the provider has 2-3 scheduled appointments in the next few days (from earlier scenarios).
2. Create an unavailability event:
   ```
   POST /api/provider-unavailability
   Body:
   {
     "providerId": "{providerId}",
     "providerName": "Dr. Smith",
     "unavailableFrom": "2026-04-13T00:00:00Z",
     "unavailableTo": "2026-04-18T00:00:00Z",
     "reason": "ILLNESS",
     "notes": "Flu symptoms",
     "initiatedByUserId": "ADMIN-1",
     "initiatedByUserName": "Scheduling Admin"
   }
   ```
3. Preview affected appointments:
   ```
   GET /api/provider-unavailability/{eventId}/preview
   ```

### Expected Result

- Event created with status "Pending".
- Preview shows the affected appointments with patient names, clinics, and times.
- `TotalAffected` matches the number of scheduled appointments in the date range.

---

### Scenario 10: Execute Batch Cancellation

### Steps

1. Execute batch cancellation:
   ```
   POST /api/provider-unavailability/{eventId}/cancel-all
   ```
2. Check event status:
   ```
   GET /api/provider-unavailability/{eventId}
   ```
3. Verify individual appointments are cancelled on the Scheduling page.

### Expected Result

- Result shows `Processed` count matching `TotalAffected`, `Failed: 0`.
- Event status is "Completed".
- All affected appointments show status "Cancelled" on the Scheduling page.
- Provider status is set to "UNAVAILABLE".

---

### Scenario 11: Batch Cancel When Feature Disabled -- Falls Back to VistA

### Steps

1. Disable the feature:
   ```
   POST /api/site-parameters/features/disable
   Body: { "featureName": "PROVIDER_UNAVAILABILITY_BATCH" }
   ```
2. Try to create an unavailability event via API.

### Expected Result

- Error: "Provider batch unavailability is not enabled for this site."
- Staff must cancel appointments individually (standard VistA workflow).

---

## Part D: Verification Checklist

- [ ] Slot grid shows standard 8-17 when PROVIDER_AVAILABILITY is disabled
- [ ] Slot grid respects provider weekly patterns when enabled
- [ ] Time blocks subtract from availability windows (lunch splits a window)
- [ ] Provider-wide blocks affect all clinics; clinic-specific blocks only affect one
- [ ] UNAVAILABLE provider status prevents scheduling (when feature enabled)
- [ ] Double Book Override bypasses availability check
- [ ] Batch cancel cancels all affected appointments
- [ ] Batch cancel sets provider status to UNAVAILABLE
- [ ] Feature gates prevent access when disabled with clear error messages
- [ ] Core VistA scheduling (no provider, clinic-only) works regardless of feature state

---

## Part E: Patient Self-Scheduling Cross-Check

If `PATIENT_SELF_SCHEDULING` is also being tested in this pass, hand off to
[PatientPortal/01-Patient-Scheduling.md](../../PatientPortal/01-Patient-Scheduling.md)
and verify:

- [ ] Appointments scheduled via Patient Portal appear in the staff Schedule view
- [ ] Provider availability windows configured here also limit patient self-scheduling slots
- [ ] Patient-scheduled appointments respect provider lunch/time blocks created in Scenario 4

---

## Cross-References

- Memory: [project_scheduling_vista_rpms.md](../../../../../../C:/Users/James/.claude/projects/c--Users-James-source-repos-dnasmyth-NewVistas/memory/project_scheduling_vista_rpms.md) -- provider availability, batch unavailability, and patient self-scheduling are all enhancements beyond core VistA/RPMS, gated by site feature flags.
- Controllers: [SchedulingController.cs](../../../../../NewVistas.WebServer/Controllers/SchedulingController.cs), [ProviderUnavailabilityController.cs](../../../../../NewVistas.WebServer/Controllers/ProviderUnavailabilityController.cs)
- Grain interfaces: [IProviderAvailabilityGrain.cs](../../../../GrainInterfaces/IProviderAvailabilityGrain.cs), [IProviderUnavailabilityGrain.cs](../../../../GrainInterfaces/IProviderUnavailabilityGrain.cs), [IClinicGrain.cs](../../../../GrainInterfaces/IClinicGrain.cs)
- Functional / unit tests:
  - `SchedulingWorkflowTests.ClinicGrain_Create_PersistsName`
  - `SchedulingWorkflowTests.ClinicIndexGrain_Search_FiltersByName`
  - `SchedulingWorkflowTests.AppointmentGrain_Schedule_PersistsState`
  - `ProviderAvailabilityGrainTests.ProviderAvailability_DefaultState_IsActive`
  - `ProviderAvailabilityGrainTests.ProviderAvailability_UpdateStatus_ReflectsChange`
  - `ProviderAvailabilityGrainTests.ProviderAvailability_InactiveProvider_NoEffectiveAvailability`
  - `ProviderAvailabilityGrainTests.ProviderAvailability_AddWeeklyPattern_Persists`
  - `SchedulingSlotQueryTests.GetAvailableSlots_EmptySchedule_AllSlotsAvailable`
  - `SchedulingSlotQueryTests.GetAvailableSlots_45MinSlots_CorrectCount`
  - `SchedulingSlotQueryTests.GetAvailableSlots_OneAppointment_SlotMarkedBooked`
