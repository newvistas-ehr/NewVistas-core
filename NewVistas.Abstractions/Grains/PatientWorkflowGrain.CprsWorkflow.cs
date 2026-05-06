// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public partial class PatientWorkflowGrain
{
    // ─── Order Entry Workflow (ORWDX.m SAVE, ORWDXA.m) ───────────────────
    // Three-tier read pattern (mirrors vitals):
    //   1. Patient grain — embedded RecentOrders cache (last N, zero fan-out)
    //   2. PatientOrderIndexGrain — full history index, supports filter/range queries
    //   3. Individual OrderGrain — full detail record, status mutations

    private IPatientOrderIndexGrain GetOrderIndex()
        => GrainFactory.GetGrain<IPatientOrderIndexGrain>(PatientId);

    /// <summary>
    /// Creates an OrderIndexEntry from the current state of an order grain.
    /// Used for both initial index population and status-change sync.
    /// </summary>
    private static OrderIndexEntry MakeOrderIndexEntry(OrderState state) => new()
    {
        OrderGrainKey = state.OrderId,
        StartDate = state.OrderDateTime,
        OrderType = state.OrderType ?? "",
        Status = state.Status ?? "",
        OrderText = state.OrderableItem ?? "",
        ProviderName = state.ProviderName,
        IsSigned = !string.IsNullOrEmpty(state.ElectronicSignature)
    };

    /// <summary>
    /// Syncs an order's current state to the index grain.
    /// Called after any status change (sign, hold, DC, renew, verify, etc.).
    /// </summary>
    private async Task SyncOrderToIndexAsync(string orderId)
    {
        OrderState state = await GrainFactory.GetGrain<IOrderGrain>(orderId).GetOrderAsync();
        await GetOrderIndex().AddOrUpdateOrderAsync(MakeOrderIndexEntry(state));
    }

    public async Task<string> PlaceOrderAsync(
        string orderType, string orderText, string? orderableItemId,
        string providerId, string providerName,
        string? locationId, string? locationName,
        string urgency, string? instructions, string? indication)
    {
        var orderId = $"ORDER-{Guid.NewGuid()}";
        var orderGrain = GrainFactory.GetGrain<IOrderGrain>(orderId);
        var now = DateTime.UtcNow;

        // ORWDX SAVE: create the order
        await orderGrain.CreateOrderAsync(
            PatientId, orderType, orderText, orderableItemId,
            providerId, providerName, now, locationId, locationName,
            urgency, instructions, indication, "NEW", providerId);

        // Link to patient (legacy — kept for backward compatibility)
        await GetPatientGrain().AddOrderIdAsync(orderId);

        // Add to the full-history order index
        OrderState state = await orderGrain.GetOrderAsync();
        await GetOrderIndex().AddOrUpdateOrderAsync(MakeOrderIndexEntry(state));

        // Add to the embedded recent orders cache on the patient grain
        int displayCount = await GetSiteParams().GetOrdersDisplayCountAsync();
        var summary = new OrderSummary
        {
            OrderId = orderId,
            OrderText = orderText,
            OrderType = orderType,
            Status = state.Status ?? "Pending",
            StartDate = now,
            ProviderName = providerName
        };
        await GetPatientGrain().AddRecentOrderAsync(summary, displayCount);

        return orderId;
    }

    public async Task SignOrderAsync(string orderId, string electronicSignature)
    {
        // ORWDXA with ACTION="ES"
        var orderGrain = GrainFactory.GetGrain<IOrderGrain>(orderId);
        var now = DateTime.UtcNow;

        await orderGrain.SignOrderAsync(electronicSignature, now);
        await orderGrain.ReleaseOrderAsync(now);

        // Sync index with new status (Active + signed)
        await SyncOrderToIndexAsync(orderId);
    }

    public async Task DiscontinueOrderAsync(string orderId, string reason)
    {
        // ORWDXA DC
        var orderGrain = GrainFactory.GetGrain<IOrderGrain>(orderId);
        await orderGrain.DiscontinueOrderAsync(DateTime.UtcNow, reason, null);

        // Sync index with new status (Discontinued)
        await SyncOrderToIndexAsync(orderId);
    }

    public async Task HoldOrderAsync(string orderId)
    {
        // ORWDXA HOLD — note: per ORDER STATUS file (#100.01) entry 3,
        // not all packages support hold (Lab cannot, Pharmacy can)
        var orderGrain = GrainFactory.GetGrain<IOrderGrain>(orderId);
        await orderGrain.HoldOrderAsync();

        // Sync index with new status (Hold)
        await SyncOrderToIndexAsync(orderId);
    }

    public async Task ReleaseOrderAsync(string orderId)
    {
        // ORWDXA UNHOLD
        var orderGrain = GrainFactory.GetGrain<IOrderGrain>(orderId);
        await orderGrain.ReleaseOrderAsync(DateTime.UtcNow);

        // Sync index with new status (Active)
        await SyncOrderToIndexAsync(orderId);
    }

    public async Task<List<OrderSummary>> GetOrdersByFilterAsync(int filter)
    {
        // ORWORR AGET filter codes from XGET:
        //   1=All, 2=Current, 3=Discontinued, 4=Completed/Expired,
        //   5=Expiring, 7=Pending, 11=Unsigned
        // Now reads from the order index — zero fan-out to individual order grains
        List<OrderIndexEntry> entries = await GetOrderIndex().GetEntriesByFilterAsync(filter);

        return entries
            .Select(e => new OrderSummary
            {
                OrderId = e.OrderGrainKey,
                OrderText = e.OrderText,
                OrderType = e.OrderType,
                Status = e.Status,
                StartDate = e.StartDate,
                ProviderName = e.ProviderName
            })
            .ToList();
    }

    public async Task<List<OrderSummary>> GetRecentOrdersAsync()
    {
        // Hot path: return the embedded recent orders cache — zero fan-out
        return await GetPatientGrain().GetRecentOrdersAsync();
    }

    public async Task<List<OrderSummary>> GetOrderHistoryAsync(DateTime? from, DateTime? to, int maxCount)
    {
        // Cold path: query the index with optional date range
        var orderIndex = GetOrderIndex();

        List<OrderIndexEntry> entries;
        if (from.HasValue && to.HasValue)
            entries = await orderIndex.GetEntriesByDateRangeAsync(from.Value, to.Value);
        else if (to.HasValue)
            entries = await orderIndex.GetEntriesBeforeDateAsync(to.Value, maxCount);
        else
            entries = await orderIndex.GetAllEntriesAsync();

        return entries.Take(maxCount)
            .Select(e => new OrderSummary
            {
                OrderId = e.OrderGrainKey,
                OrderText = e.OrderText,
                OrderType = e.OrderType,
                Status = e.Status,
                StartDate = e.StartDate,
                ProviderName = e.ProviderName
            })
            .ToList();
    }

    public Task<OrderState> GetOrderDetailAsync(string orderId) =>
        GrainFactory.GetGrain<IOrderGrain>(orderId).GetOrderAsync();

    public async Task RenewOrderAsync(string orderId, string renewedByProviderId, DateTime? newStopDateTime)
    {
        var orderGrain = GrainFactory.GetGrain<IOrderGrain>(orderId);
        await orderGrain.RenewOrderAsync(renewedByProviderId, newStopDateTime);

        // Sync index with renewed status
        await SyncOrderToIndexAsync(orderId);
    }

    public async Task VerifyOrderAsync(string orderId, string nurseId)
    {
        var orderGrain = GrainFactory.GetGrain<IOrderGrain>(orderId);
        await orderGrain.VerifyOrderAsync(nurseId, DateTime.UtcNow);

        // Sync index (nurse verification may change signature status)
        await SyncOrderToIndexAsync(orderId);
    }

    public Task<List<OrderCheckResult>> CheckOrderAsync(
        string orderType, string orderText, string? orderableItemId)
    {
        var checker = GrainFactory.GetGrain<IOrderCheckGrain>("ORDER-CHECK");
        return checker.CheckOrderAsync(PatientId, orderType, orderText, orderableItemId);
    }

    public async Task<List<string>> ExecuteOrderSetAsync(
        string orderSetId,
        string providerId, string providerName,
        string? locationId, string? locationName,
        List<string>? selectedTemplateIds)
    {
        var orderSetGrain = GrainFactory.GetGrain<IOrderSetGrain>(orderSetId);
        List<OrderTemplate> templates = await orderSetGrain.GetTemplatesAsync();

        if (selectedTemplateIds != null && selectedTemplateIds.Count > 0)
            templates = templates.Where(t => selectedTemplateIds.Contains(t.TemplateId)).ToList();
        else
            templates = templates.Where(t => t.IsDefaultSelected).ToList();

        var createdIds = new List<string>();
        foreach (OrderTemplate tmpl in templates)
        {
            string orderId = await PlaceOrderAsync(
                tmpl.OrderType, tmpl.OrderableItem, tmpl.OrderableItemId,
                providerId, providerName,
                locationId, locationName,
                tmpl.Urgency, tmpl.Instructions, null);
            createdIds.Add(orderId);
        }

        return createdIds;
    }

    // ─── Problem List Workflow (GMPLSAVE.m EN, GMPLEDIT.m) ───────────────

    public async Task<string> AddProblemAsync(
        string diagnosis, string? diagnosisCode, string? condition,
        string? priority, DateTime? dateOfOnset,
        string? providerId, string? providerName,
        string? clinicId, string? clinicName,
        bool isServiceConnected, string? comments)
    {
        var problemId = $"PROB-{Guid.NewGuid()}";
        var now = DateTime.UtcNow;

        // GMPLSAVE: save with full audit trail, set status to ACTIVE
        var entry = new ProblemEntry
        {
            ProblemId = problemId,
            Diagnosis = diagnosis,
            DiagnosisCode = diagnosisCode,
            Status = "ACTIVE",
            Condition = condition,
            Priority = priority,
            DateOfOnset = dateOfOnset,
            DateRecorded = now,
            RecordingProviderId = providerId,
            RecordingProviderName = providerName,
            ResponsibleProviderId = providerId,  // responsible provider = entering provider initially
            ResponsibleProviderName = providerName,
            ClinicId = clinicId,
            ClinicName = clinicName,
            IsServiceConnected = isServiceConnected,
            Comments = comments,
            CreatedDate = now,
            LastModifiedDate = now
        };

        await GetPatientGrain().AddProblemAsync(entry);

        return problemId;
    }

    public async Task<List<ProblemSummary>> GetActiveProblemsAsync()
    {
        List<ProblemEntry> entries = await GetPatientGrain().GetProblemsAsync();

        return entries
            .Where(e => e.Status == "ACTIVE")
            .Select(e => new ProblemSummary
            {
                ProblemId = e.ProblemId,
                Diagnosis = e.Diagnosis,
                DiagnosisCode = e.DiagnosisCode,
                Status = e.Status,
                DateOfOnset = e.DateOfOnset,
                Condition = e.Condition,
                IsServiceConnected = e.IsServiceConnected
            })
            .ToList();
    }

    public async Task<List<ProblemSummary>> GetAllProblemsAsync()
    {
        List<ProblemEntry> entries = await GetPatientGrain().GetProblemsAsync();

        return entries
            .Select(e => new ProblemSummary
            {
                ProblemId = e.ProblemId,
                Diagnosis = e.Diagnosis,
                DiagnosisCode = e.DiagnosisCode,
                Status = e.Status,
                DateOfOnset = e.DateOfOnset,
                Condition = e.Condition,
                IsServiceConnected = e.IsServiceConnected
            })
            .ToList();
    }

    public Task InactivateProblemAsync(string problemId, DateTime dateResolved)
    {
        // GMPLSAVE status change: ACTIVE → INACTIVE.
        // Causal — emits a ProblemInactivatedV1 into the patient's clinical
        // event stream as part of the same WriteStateAsync.
        return GetPatientGrain().InactivateProblemAsync(problemId, dateResolved);
    }

    // ─── Appointment/Check-In Workflow (SDAM2.m, SDAMEVT.m) ──────────────

    private IPatientScheduleIndexGrain GetScheduleIndex() =>
        GrainFactory.GetGrain<IPatientScheduleIndexGrain>($"SD-SCHED:{PatientId}");

    private IScheduleIndexGrain GetClinicScheduleIndex(string clinicId) =>
        GrainFactory.GetGrain<IScheduleIndexGrain>($"CLINIC-SCHED:{clinicId}");

    public async Task<string> ScheduleAppointmentAsync(
        string clinicId, string clinicName, DateTime appointmentDateTime,
        int durationMinutes, string? providerId, string? providerName,
        string? purpose, string? appointmentType, bool allowDoubleBook = false)
    {
        // Eligibility gate — verify patient enrollment before booking
        PatientEligibilityResult eligibility = await CheckPatientEligibilityForSchedulingAsync();
        if (!eligibility.IsEligible)
            throw new InvalidOperationException(
                $"Patient is not eligible for scheduling: {string.Join(" ", eligibility.Reasons)}");

        // Provider availability gate — only when PROVIDER_AVAILABILITY feature is enabled
        // (Enhancement: VistA is clinic-centric; this adds provider-level availability checks)
        if (!string.IsNullOrEmpty(providerId)
            && await GetSiteParams().IsFeatureEnabledAsync(ProviderAvailabilityFeature))
        {
            IProviderAvailabilityGrain provAvail = GrainFactory.GetGrain<IProviderAvailabilityGrain>($"PROV-AVAIL:{providerId}");
            ProviderAvailabilityState provState = await provAvail.GetAvailabilityAsync();

            if (provState.Status != "ACTIVE")
                throw new InvalidOperationException(
                    $"Provider is currently {provState.Status}" +
                    (provState.StatusReason != null ? $": {provState.StatusReason}" : "") +
                    ". Cannot schedule appointments.");

            List<AvailabilityWindow> windows = await provAvail.GetEffectiveAvailabilityAsync(clinicId, appointmentDateTime);
            if (windows.Count > 0 && !allowDoubleBook)
            {
                bool withinWindow = windows.Any(w =>
                    appointmentDateTime >= w.StartTime &&
                    appointmentDateTime.AddMinutes(durationMinutes) <= w.EndTime);

                if (!withinWindow)
                    throw new InvalidOperationException(
                        $"Requested time {appointmentDateTime:HH:mm} is outside provider's " +
                        $"availability at this clinic. Use Double Book override to proceed.");
            }
        }

        if (!allowDoubleBook)
        {
            ClinicState clinic = await GrainFactory.GetGrain<IClinicGrain>(clinicId).GetClinicAsync();
            if (!clinic.AllowOverbooking)
            {
                var clinicIdx = GetClinicScheduleIndex(clinicId);
                int dailyCount = await clinicIdx.GetCountByDateAsync(appointmentDateTime);
                if (dailyCount >= clinic.MaxPatientsPerDay)
                    throw new InvalidOperationException(
                        $"Clinic '{clinicName}' is fully booked on {appointmentDateTime:MM/dd/yyyy} " +
                        $"({dailyCount}/{clinic.MaxPatientsPerDay} appointments). " +
                        $"Enable Double Book override to proceed.");
                if (await clinicIdx.HasOverlapAsync(appointmentDateTime, durationMinutes))
                    throw new InvalidOperationException(
                        $"A conflicting appointment already exists at {appointmentDateTime:HH:mm} in '{clinicName}'. " +
                        $"Enable Double Book override to proceed.");
            }
        }

        var appointmentId = $"APPT-{Guid.NewGuid()}";
        var apptGrain = GrainFactory.GetGrain<IAppointmentGrain>(appointmentId);

        // SDAMEVT MAKE event #1
        await apptGrain.ScheduleAppointmentAsync(
            PatientId, clinicId, clinicName, appointmentDateTime,
            durationMinutes, providerId, providerName,
            purpose, appointmentType, null, allowDoubleBook);

        await GetPatientGrain().AddAppointmentIdAsync(appointmentId);

        // Register slot in the clinic schedule index for conflict detection
        await GetClinicScheduleIndex(clinicId).AddOrUpdateAsync(new ClinicScheduleEntry
        {
            AppointmentId = appointmentId,
            PatientId = PatientId,
            AppointmentDateTime = appointmentDateTime,
            DurationMinutes = durationMinutes,
            Status = "Scheduled",
            IsDoubleBook = allowDoubleBook
        });

        // Write schedule index entry for fast retrieval
        await GetScheduleIndex().AddOrUpdateAsync(new AppointmentEntry
        {
            AppointmentId = appointmentId,
            ClinicId = clinicId,
            ClinicName = clinicName,
            AppointmentDateTime = appointmentDateTime,
            DurationMinutes = durationMinutes,
            Status = "Scheduled",
            ProviderId = providerId,
            ProviderName = providerName,
            Purpose = purpose,
            AppointmentType = appointmentType,
            CreatedDate = DateTime.UtcNow
        });

        // PCMM: Auto-add provider to care team on appointment scheduling
        if (!string.IsNullOrEmpty(providerId))
        {
            await SyncProviderOnScheduleAsync(providerId, providerName ?? string.Empty,
                appointmentId, clinicId, clinicName, appointmentDateTime,
                durationMinutes, purpose, appointmentType);
        }

        return appointmentId;
    }

    public async Task CheckInAsync(string appointmentId, DateTime? checkInTime)
    {
        // SDAM2 ONE: check in with event capture
        // SDAMEVT BEFORE/AFTER pattern: capture state before, apply change, capture after
        var apptGrain = GrainFactory.GetGrain<IAppointmentGrain>(appointmentId);
        var ciTime = checkInTime ?? DateTime.UtcNow;

        // SDAM2: "if appt d/t is less than NOW then check-in"
        await apptGrain.CheckInAsync(ciTime);

        // Sync schedule index status
        AppointmentState state = await apptGrain.GetAppointmentAsync();
        await SyncScheduleIndexAsync(state);

        // Sync provider schedule index
        if (!string.IsNullOrEmpty(state.ProviderId))
            await GetProviderScheduleIndex(state.ProviderId).UpdateStatusAsync(appointmentId, state.Status);
    }

    public async Task CheckOutAsync(string appointmentId, DateTime? checkOutTime)
    {
        var apptGrain = GrainFactory.GetGrain<IAppointmentGrain>(appointmentId);
        var coTime = checkOutTime ?? DateTime.UtcNow;

        await apptGrain.CheckOutAsync(coTime);
        await apptGrain.CompleteAppointmentAsync();

        // Sync schedule index status
        AppointmentState state = await apptGrain.GetAppointmentAsync();
        await SyncScheduleIndexAsync(state);

        // Sync provider: update schedule status and LastSeen on care team + patient index
        if (!string.IsNullOrEmpty(state.ProviderId))
        {
            await GetProviderScheduleIndex(state.ProviderId).UpdateStatusAsync(appointmentId, state.Status);
            await GetCareTeamGrain().UpdateMemberLastSeenAsync(state.ProviderId, coTime);
            await GetProviderPatientIndex(state.ProviderId).UpdateLastSeenAsync(PatientId, coTime);
        }
    }

    public async Task CancelAppointmentAsync(string appointmentId)
    {
        // SDAMEVT CANCEL event #2
        var apptGrain = GrainFactory.GetGrain<IAppointmentGrain>(appointmentId);
        await apptGrain.CancelAppointmentAsync("Cancelled", null);

        // Sync schedule index status
        AppointmentState state = await apptGrain.GetAppointmentAsync();
        await SyncScheduleIndexAsync(state);

        // Sync provider schedule (care team membership NOT removed on cancellation)
        if (!string.IsNullOrEmpty(state.ProviderId))
            await GetProviderScheduleIndex(state.ProviderId).UpdateStatusAsync(appointmentId, state.Status);
    }

    public async Task NoShowAppointmentAsync(string appointmentId)
    {
        // SDAMEVT NOSHOW event #3
        var apptGrain = GrainFactory.GetGrain<IAppointmentGrain>(appointmentId);
        await apptGrain.MarkAsNoShowAsync();

        // Sync schedule index status
        AppointmentState state = await apptGrain.GetAppointmentAsync();
        await SyncScheduleIndexAsync(state);

        // Sync provider schedule (care team membership NOT removed on no-show)
        if (!string.IsNullOrEmpty(state.ProviderId))
            await GetProviderScheduleIndex(state.ProviderId).UpdateStatusAsync(appointmentId, state.Status);
    }

    public async Task<List<VisitSummary>> GetUpcomingAppointmentsAsync()
    {
        List<AppointmentEntry> entries = await GetScheduleIndex().GetUpcomingAsync(20);
        return entries
            .Select(e => new VisitSummary
            {
                AppointmentId = e.AppointmentId,
                ClinicName = e.ClinicName,
                AppointmentDateTime = e.AppointmentDateTime,
                Status = e.Status,
                ProviderName = e.ProviderName
            })
            .ToList();
    }

    public Task<AppointmentState> GetAppointmentAsync(string appointmentId) =>
        GrainFactory.GetGrain<IAppointmentGrain>(appointmentId).GetAppointmentAsync();

    public async Task RescheduleAppointmentAsync(string appointmentId, DateTime newDateTime, string? reason, string? modifiedBy)
    {
        // SDAMEVT CHANGE event — update appointment date/time
        var apptGrain = GrainFactory.GetGrain<IAppointmentGrain>(appointmentId);
        await apptGrain.UpdateAppointmentAsync(newDateTime, null, null, null, reason, null, modifiedBy);

        // Sync schedule index
        AppointmentState state = await apptGrain.GetAppointmentAsync();
        await SyncScheduleIndexAsync(state);

        // Sync provider schedule and extend care team expiration
        if (!string.IsNullOrEmpty(state.ProviderId))
        {
            PatientState patient = await GetPatientGrain().GetPatientAsync();
            await GetProviderScheduleIndex(state.ProviderId).AddOrUpdateAsync(new ProviderScheduleEntry
            {
                AppointmentId = state.AppointmentId,
                PatientId = PatientId,
                PatientName = patient.Name,
                AppointmentDateTime = newDateTime,
                ClinicId = state.ClinicId,
                ClinicName = state.ClinicName,
                DurationMinutes = state.DurationMinutes,
                Status = state.Status,
                Purpose = state.Purpose,
                AppointmentType = state.AppointmentType
            });

            // Extend care team expiration to 90 days from new date
            await GetCareTeamGrain().AddMemberAsync(state.ProviderId, state.ProviderName ?? state.ProviderId,
                "SPECIALIST", null, "APPOINTMENT", newDateTime.AddDays(90));

            // Update next appointment date on provider's patient index
            await GetProviderPatientIndex(state.ProviderId).UpdateNextAppointmentAsync(PatientId, newDateTime);
        }
    }

    public Task<List<AppointmentEntry>> GetAllAppointmentsAsync(int max = 50) =>
        GetScheduleIndex().GetAppointmentsAsync(max);

    public Task<List<ClinicEntry>> GetClinicListAsync() =>
        GrainFactory.GetGrain<IClinicIndexGrain>("SD-CLINIC-INDEX").GetAllClinicsAsync();

    public async Task<ClinicDailyCapacity> GetClinicDailyCapacityAsync(string clinicId, DateTime date)
    {
        IClinicGrain clinicGrain = GrainFactory.GetGrain<IClinicGrain>(clinicId);
        ClinicState clinic = await clinicGrain.GetClinicAsync();

        int bookedCount = await GetClinicScheduleIndex(clinicId).GetCountByDateAsync(date);
        int remaining = Math.Max(0, clinic.MaxPatientsPerDay - bookedCount);

        List<AvailableSlot> slots = await GetClinicScheduleIndex(clinicId)
            .GetAvailableSlotsAsync(date, 8, 17, clinic.AppointmentLength);

        return new ClinicDailyCapacity
        {
            ClinicId = clinicId,
            ClinicName = clinic.Name,
            Date = date.Date,
            MaxPatientsPerDay = clinic.MaxPatientsPerDay,
            BookedCount = bookedCount,
            RemainingSlots = remaining,
            AllowOverbooking = clinic.AllowOverbooking,
            IsAtCapacity = !clinic.AllowOverbooking && bookedCount >= clinic.MaxPatientsPerDay,
            AppointmentLength = clinic.AppointmentLength,
            AvailableSlots = slots,
        };
    }

    public async Task<List<AvailableSlot>> GetAvailableSlotsAsync(string clinicId, DateTime date)
    {
        IClinicGrain clinicGrain = GrainFactory.GetGrain<IClinicGrain>(clinicId);
        ClinicState clinic = await clinicGrain.GetClinicAsync();

        // If PROVIDER_AVAILABILITY enabled and clinic has a primary provider, use provider patterns
        if (!string.IsNullOrEmpty(clinic.PrimaryProviderId)
            && await GetSiteParams().IsFeatureEnabledAsync(ProviderAvailabilityFeature))
        {
            List<AvailableSlot> providerSlots = await GetProviderAvailableSlotsAsync(clinicId, date, clinic.PrimaryProviderId);
            if (providerSlots.Count > 0)
                return providerSlots;
        }

        // VistA default: clinic-wide 8-17 grid (SDBUILD.m pattern)
        return await GetClinicScheduleIndex(clinicId)
            .GetAvailableSlotsAsync(date, 8, 17, clinic.AppointmentLength);
    }

    // ─── Provider Availability (Enhancement — Site Flavor gated) ────────────
    // VistA is clinic-centric (File #44.005 patterns on the clinic, not provider).
    // These methods add provider-level availability when PROVIDER_AVAILABILITY is enabled.

    public async Task<List<AvailableSlot>> GetProviderAvailableSlotsAsync(
        string clinicId, DateTime date, string? providerId)
    {
        IClinicGrain clinicGrain = GrainFactory.GetGrain<IClinicGrain>(clinicId);
        ClinicState clinic = await clinicGrain.GetClinicAsync();

        if (string.IsNullOrEmpty(providerId))
            return await GetClinicScheduleIndex(clinicId)
                .GetAvailableSlotsAsync(date, 8, 17, clinic.AppointmentLength);

        IProviderAvailabilityGrain availGrain = GrainFactory.GetGrain<IProviderAvailabilityGrain>($"PROV-AVAIL:{providerId}");
        ProviderAvailabilityState availState = await availGrain.GetAvailabilityAsync();

        if (availState.Status != "ACTIVE")
            return new List<AvailableSlot>();

        List<AvailabilityWindow> windows = await availGrain.GetEffectiveAvailabilityAsync(clinicId, date);
        if (windows.Count == 0)
            return new List<AvailableSlot>();

        ClinicSchedulingTierConfig? tierConfig = await availGrain.GetClinicSchedulingTiersAsync(clinicId);
        int slotDuration = windows.FirstOrDefault()?.AppointmentLengthOverride ?? clinic.AppointmentLength;

        return await GetClinicScheduleIndex(clinicId)
            .GetAvailableSlotsAsync(date, slotDuration, windows, tierConfig);
    }

    public async Task<ClinicDailyCapacity> GetProviderClinicDailyCapacityAsync(
        string clinicId, DateTime date, string? providerId)
    {
        IClinicGrain clinicGrain = GrainFactory.GetGrain<IClinicGrain>(clinicId);
        ClinicState clinic = await clinicGrain.GetClinicAsync();

        int bookedCount = await GetClinicScheduleIndex(clinicId).GetCountByDateAsync(date);
        int remaining = Math.Max(0, clinic.MaxPatientsPerDay - bookedCount);

        List<AvailableSlot> slots = await GetProviderAvailableSlotsAsync(clinicId, date, providerId);

        return new ClinicDailyCapacity
        {
            ClinicId = clinicId,
            ClinicName = clinic.Name,
            Date = date.Date,
            MaxPatientsPerDay = clinic.MaxPatientsPerDay,
            BookedCount = bookedCount,
            RemainingSlots = remaining,
            AllowOverbooking = clinic.AllowOverbooking,
            IsAtCapacity = !clinic.AllowOverbooking && bookedCount >= clinic.MaxPatientsPerDay,
            AppointmentLength = clinic.AppointmentLength,
            AvailableSlots = slots,
        };
    }

    public async Task<List<AvailableSlot>> GetPatientSchedulableSlotsAsync(
        string clinicId, DateTime date, string? providerId)
    {
        List<AvailableSlot> allSlots = await GetProviderAvailableSlotsAsync(clinicId, date, providerId);
        return allSlots
            .Where(s => s.IsAvailable && s.SchedulingTier == "PATIENT")
            .ToList();
    }

    // ─── Cancellation with reason (supports batch operations) ───────────

    public async Task CancelAppointmentWithReasonAsync(string appointmentId, string reason, string cancelledBy)
    {
        var apptGrain = GrainFactory.GetGrain<IAppointmentGrain>(appointmentId);
        await apptGrain.CancelAppointmentAsync(reason, cancelledBy);

        AppointmentState state = await apptGrain.GetAppointmentAsync();
        await SyncScheduleIndexAsync(state);

        if (!string.IsNullOrEmpty(state.ProviderId))
            await GetProviderScheduleIndex(state.ProviderId).UpdateStatusAsync(appointmentId, state.Status);

        // Trigger waitlist auto-offer for the opened slot
        await TryAutoOfferWaitListSlotAsync(state.ClinicId, state.AppointmentDateTime);
    }

    public async Task ReassignAppointmentProviderAsync(
        string appointmentId, string newProviderId, string newProviderName, string? reason)
    {
        var apptGrain = GrainFactory.GetGrain<IAppointmentGrain>(appointmentId);
        AppointmentState state = await apptGrain.GetAppointmentAsync();

        string? oldProviderId = state.ProviderId;

        // Update the appointment with new provider
        await apptGrain.UpdateAppointmentAsync(null, null, newProviderId, newProviderName, reason, null, "SYSTEM");

        // Re-read state
        state = await apptGrain.GetAppointmentAsync();
        await SyncScheduleIndexAsync(state);

        // Remove from old provider's schedule
        if (!string.IsNullOrEmpty(oldProviderId))
            await GetProviderScheduleIndex(oldProviderId).RemoveAsync(appointmentId);

        // Add to new provider's schedule
        PatientState patient = await GetPatientGrain().GetPatientAsync();
        await GetProviderScheduleIndex(newProviderId).AddOrUpdateAsync(new ProviderScheduleEntry
        {
            AppointmentId = state.AppointmentId,
            PatientId = PatientId,
            PatientName = patient.Name,
            AppointmentDateTime = state.AppointmentDateTime,
            ClinicId = state.ClinicId,
            ClinicName = state.ClinicName,
            DurationMinutes = state.DurationMinutes,
            Status = state.Status,
            Purpose = state.Purpose,
            AppointmentType = state.AppointmentType
        });

        // Add new provider to care team with 90-day expiration
        await GetCareTeamGrain().AddMemberAsync(newProviderId, newProviderName,
            "SPECIALIST", null, "APPOINTMENT", state.AppointmentDateTime.AddDays(90));

        await GetProviderPatientIndex(newProviderId).AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = PatientId,
            PatientName = patient.Name,
            DateOfBirth = patient.DateOfBirth,
            Relationship = "SPECIALIST"
        });
        await GetProviderPatientIndex(newProviderId).UpdateNextAppointmentAsync(PatientId, state.AppointmentDateTime);
    }

    /// <summary>
    /// Checks the waitlist for a matching entry when a slot opens up and auto-offers it.
    /// Only fires when the APPOINTMENT_WAITLIST feature is enabled (RPMS-pattern auto-rebooking).
    /// VistA's EWL (File #409.3) does not auto-offer — this is an RPMS-inspired enhancement.
    /// </summary>
    private async Task TryAutoOfferWaitListSlotAsync(string clinicId, DateTime slotDateTime)
    {
        try
        {
            // Only auto-offer if waitlist feature is enabled
            bool waitlistEnabled = await GetSiteParams().IsFeatureEnabledAsync(AppointmentWaitListFeature);
            if (!waitlistEnabled)
                return;

            IAppointmentWaitListIndexGrain waitListIndex = GrainFactory.GetGrain<IAppointmentWaitListIndexGrain>("SD-WL-IDX");
            AppointmentWaitListIndexEntry? match = await waitListIndex.FindBestMatchForSlotAsync(clinicId, slotDateTime);

            if (match != null)
            {
                IPatientWorkflowGrain patientWorkflow = GrainFactory.GetGrain<IPatientWorkflowGrain>(match.PatientId);
                await patientWorkflow.OfferWaitListSlotAsync(
                    match.EntryId, $"AUTO-{Guid.NewGuid()}", slotDateTime, "SYSTEM-AUTO");
            }
        }
        catch
        {
            // Waitlist auto-offer is best-effort; don't fail the cancellation
        }
    }

    // (AppointmentWaitListFeature constant is in PatientWorkflowGrain.WaitList.cs)

    // ─── Patient Eligibility Verification (DG eligibility — DGENELA.m) ──────

    public async Task<PatientEligibilityResult> CheckPatientEligibilityForSchedulingAsync()
    {
        PatientEligibilityResult result = new() { IsEligible = true };

        // Get enrollment data
        PatientEnrollmentState enrollment = await Enrollment().GetAsync();
        PatientState patient = await GetPatientAsync();

        result.EnrollmentStatus = enrollment.EnrollmentStatus.ToString();
        result.PriorityGroup = enrollment.PriorityGroup;
        result.CopayExempt = enrollment.CopayExempt;
        result.CopayExemptionReason = enrollment.CopayExemptionReason;
        result.MeansTestRequired = enrollment.MeansTestRequired;
        result.ServiceConnectedPercentage = patient.ServiceConnectedPercentage;
        result.PrimaryEligibilityCode = patient.PrimaryEligibilityCode;
        result.EnrollmentEffectiveDate = enrollment.EffectiveDate;
        result.EnrollmentTerminationDate = enrollment.TerminationDate;

        // If enrollment grain has never been explicitly set (no status change recorded),
        // treat as eligible (new patient without enrollment record yet).
        // This allows scheduling for patients who haven't gone through enrollment.
        if (!enrollment.LastStatusChangeDate.HasValue && string.IsNullOrEmpty(enrollment.PatientId))
            return result;

        // Check enrollment status — must be Verified or LimitedBenefits
        if (enrollment.EnrollmentStatus is not (
            GrainStates.EnrollmentStatus.Verified or
            GrainStates.EnrollmentStatus.LimitedBenefits))
        {
            result.IsEligible = false;
            result.Reasons.Add($"Enrollment status is '{enrollment.EnrollmentStatus}'. Must be Verified or LimitedBenefits.");
        }

        // Check for termination
        if (enrollment.TerminationDate.HasValue && enrollment.TerminationDate.Value <= DateTime.UtcNow)
        {
            result.IsTerminated = true;
            result.IsEligible = false;
            result.Reasons.Add($"Enrollment was terminated on {enrollment.TerminationDate.Value:d}.");
        }

        // Check means test completion if required
        if (enrollment.MeansTestRequired && !enrollment.MeansTestExempt && !enrollment.CopayExempt)
        {
            MeansTestEntry? latestMt = patient.MeansTests.Count > 0
                ? patient.MeansTests[^1]
                : null;

            if (latestMt is null)
            {
                result.MeansTestCompleted = false;
                result.IsEligible = false;
                result.Reasons.Add("Means test is required but has not been completed.");
            }
            else
            {
                result.MeansTestCompleted = true;
            }
        }
        else
        {
            result.MeansTestCompleted = true; // Not required or exempt
        }

        return result;
    }

    // ─── Appointment Letter Generation (SD appointment letters) ────────────

    public async Task<AppointmentLetterContent> GenerateAppointmentLetterAsync(
        string appointmentId, string letterType)
    {
        AppointmentState appt = await GrainFactory.GetGrain<IAppointmentGrain>(appointmentId)
            .GetAppointmentAsync();
        PatientState patient = await GetPatientAsync();

        // Get clinic details for location/phone
        ClinicState clinic = await GrainFactory.GetGrain<IClinicGrain>(appt.ClinicId).GetClinicAsync();

        return new AppointmentLetterContent
        {
            AppointmentId = appointmentId,
            PatientName = patient.Name,
            PatientId = PatientId,
            StreetAddress1 = patient.StreetAddress1,
            StreetAddress2 = patient.StreetAddress2,
            City = patient.City,
            State = patient.State,
            ZipCode = patient.ZipCode,
            PhoneNumber = patient.PhoneNumberResidence,
            ClinicName = appt.ClinicName,
            ClinicPhone = clinic.PhoneNumber,
            ClinicLocation = clinic.PhysicalLocation,
            AppointmentDateTime = appt.AppointmentDateTime,
            DurationMinutes = appt.DurationMinutes,
            ProviderName = appt.ProviderName,
            Purpose = appt.Purpose,
            AppointmentType = appt.AppointmentType,
            LetterType = letterType,
            GeneratedDate = DateTime.UtcNow,
            Instructions = letterType switch
            {
                "REMINDER" => "Please arrive 15 minutes early. Bring your insurance card and a list of current medications.",
                "CANCELLATION" => "Your appointment has been cancelled. Please contact the clinic to reschedule if needed.",
                "PROVIDER_CHANGE" => "Your appointment provider has been changed. The date and time remain the same. Please contact the clinic if you have questions.",
                _ => "Your appointment has been confirmed. Please call the clinic if you need to reschedule.",
            },
        };
    }

    // ─── Reminder Batch Processing (SD reminder processing) ─────────────────

    public async Task<ReminderBatchResult> ProcessReminderBatchAsync(int daysAhead)
    {
        ReminderBatchResult result = new();

        List<AppointmentEntry> upcoming = await GetScheduleIndex().GetUpcomingAsync(100);
        DateTime cutoffDate = DateTime.UtcNow.AddDays(daysAhead);

        foreach (AppointmentEntry entry in upcoming)
        {
            result.TotalEvaluated++;

            // Only process appointments within the reminder window
            if (entry.AppointmentDateTime > cutoffDate)
            {
                result.Skipped++;
                result.Entries.Add(new ReminderBatchEntry
                {
                    AppointmentId = entry.AppointmentId, PatientId = PatientId,
                    ClinicName = entry.ClinicName, AppointmentDateTime = entry.AppointmentDateTime,
                    Status = "SKIPPED", Reason = "Beyond reminder window"
                });
                continue;
            }

            if (entry.Status is not ("Scheduled"))
            {
                result.Skipped++;
                result.Entries.Add(new ReminderBatchEntry
                {
                    AppointmentId = entry.AppointmentId, PatientId = PatientId,
                    ClinicName = entry.ClinicName, AppointmentDateTime = entry.AppointmentDateTime,
                    Status = "SKIPPED", Reason = $"Status is {entry.Status}"
                });
                continue;
            }

            // Check if already sent
            IAppointmentGrain apptGrain = GrainFactory.GetGrain<IAppointmentGrain>(entry.AppointmentId);
            AppointmentState apptState = await apptGrain.GetAppointmentAsync();

            if (apptState.ReminderSent)
            {
                result.AlreadySent++;
                result.Entries.Add(new ReminderBatchEntry
                {
                    AppointmentId = entry.AppointmentId, PatientId = PatientId,
                    ClinicName = entry.ClinicName, AppointmentDateTime = entry.AppointmentDateTime,
                    Status = "ALREADY_SENT", ReminderSent = true
                });
                continue;
            }

            // Mark reminder as sent
            await apptGrain.MarkReminderSentAsync();
            result.RemindersSent++;
            result.Entries.Add(new ReminderBatchEntry
            {
                AppointmentId = entry.AppointmentId, PatientId = PatientId,
                ClinicName = entry.ClinicName, AppointmentDateTime = entry.AppointmentDateTime,
                Status = "SENT", ReminderSent = true
            });
        }

        return result;
    }

    public async Task<List<AppointmentEntry>> GetAppointmentsNeedingRemindersAsync(int daysAhead)
    {
        List<AppointmentEntry> upcoming = await GetScheduleIndex().GetUpcomingAsync(100);
        DateTime cutoffDate = DateTime.UtcNow.AddDays(daysAhead);

        List<AppointmentEntry> needReminders = new();

        foreach (AppointmentEntry entry in upcoming)
        {
            if (entry.AppointmentDateTime > cutoffDate || entry.Status != "Scheduled")
                continue;

            IAppointmentGrain apptGrain = GrainFactory.GetGrain<IAppointmentGrain>(entry.AppointmentId);
            AppointmentState apptState = await apptGrain.GetAppointmentAsync();

            if (!apptState.ReminderSent)
                needReminders.Add(entry);
        }

        return needReminders;
    }

    private async Task SyncScheduleIndexAsync(AppointmentState state)
    {
        await GetScheduleIndex().AddOrUpdateAsync(new AppointmentEntry
        {
            AppointmentId = state.AppointmentId,
            ClinicId = state.ClinicId,
            ClinicName = state.ClinicName,
            AppointmentDateTime = state.AppointmentDateTime,
            DurationMinutes = state.DurationMinutes,
            Status = state.Status,
            ProviderId = state.ProviderId,
            ProviderName = state.ProviderName,
            Purpose = state.Purpose,
            AppointmentType = state.AppointmentType,
            CreatedDate = state.CreatedDate
        });
        await GetClinicScheduleIndex(state.ClinicId).UpdateStatusAsync(state.AppointmentId, state.Status);
    }

    /// <summary>
    /// PCMM sync helper — called when an appointment is scheduled with a provider.
    /// Adds provider to care team, syncs provider's patient index, and adds to provider schedule.
    /// </summary>
    private async Task SyncProviderOnScheduleAsync(string providerId, string providerName,
        string appointmentId, string clinicId, string clinicName, DateTime appointmentDateTime,
        int durationMinutes, string? purpose, string? appointmentType)
    {
        // Add to care team with 90-day expiration from appointment date
        await GetCareTeamGrain().AddMemberAsync(providerId, providerName,
            "SPECIALIST", null, "APPOINTMENT", appointmentDateTime.AddDays(90));

        // Add patient to provider's patient index
        PatientState patient = await GetPatientGrain().GetPatientAsync();
        string? ssnLast4 = patient.SocialSecurityNumber?.Length >= 4
            ? patient.SocialSecurityNumber[^4..] : null;

        await GetProviderPatientIndex(providerId).AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = PatientId,
            PatientName = patient.Name,
            DateOfBirth = patient.DateOfBirth,
            SsnLast4 = ssnLast4,
            Relationship = "SPECIALIST",
            NextAppointmentDate = appointmentDateTime,
            IsActive = true,
            AssignmentDate = DateTime.UtcNow
        });

        // Add to provider's schedule index
        await GetProviderScheduleIndex(providerId).AddOrUpdateAsync(new ProviderScheduleEntry
        {
            AppointmentId = appointmentId,
            PatientId = PatientId,
            PatientName = patient.Name,
            AppointmentDateTime = appointmentDateTime,
            ClinicId = clinicId,
            ClinicName = clinicName,
            DurationMinutes = durationMinutes,
            Status = "Scheduled",
            Purpose = purpose,
            AppointmentType = appointmentType
        });

        // Grant access to sensitive records
        await GetPatientAccessControlGrain().AddAuthorizedProviderAsync(providerId);
    }

    // ─── Vitals Workflow (GMRVED*.m, GMRVFILE.m) ─────────────────────────
    // Three-tier read pattern:
    //   1. Patient grain — embedded RecentVitals cache (last N, zero fan-out)
    //   2. PatientVitalIndexGrain — full history index, supports range queries
    //   3. Individual VitalGrain — full detail record, write-once

    private const string DefaultSiteKey = "SITE:DEFAULT";

    // Enhancement feature flags — these features go beyond core VistA/RPMS scheduling
    private const string ProviderAvailabilityFeature = "PROVIDER_AVAILABILITY";
    private const string ProviderUnavailabilityBatchFeature = "PROVIDER_UNAVAILABILITY_BATCH";
    private const string PatientSelfSchedulingFeature = "PATIENT_SELF_SCHEDULING";

    /// <summary>
    /// Generates a composite vital grain key: VITAL:{patientId}:{yyyyMMddHHmmss}:{vitalType}
    /// Sortable by date, contains patient + type metadata in the key itself.
    /// </summary>
    private static string MakeVitalKey(string patientId, DateTime dateTimeTaken, string vitalType)
        => $"VITAL:{patientId}:{dateTimeTaken:yyyyMMddHHmmss}:{vitalType}";

    private IPatientVitalIndexGrain GetVitalIndex()
        => GrainFactory.GetGrain<IPatientVitalIndexGrain>(PatientId);

    private ISiteParametersGrain GetSiteParams()
        => GrainFactory.GetGrain<ISiteParametersGrain>(DefaultSiteKey);

    public async Task RecordVitalsAsync(
        string? locationId, string? locationName,
        string? enteredById, string? enteredByName,
        DateTime dateTimeTaken,
        Dictionary<string, string> vitals,
        Dictionary<string, List<string>>? qualifiers)
    {
        var patientGrain = GetPatientGrain();
        int displayCount = await GetSiteParams().GetVitalsDisplayCountAsync();

        // GMRVED: enter each vital as a separate measurement (like file 120.5 entries)
        // Fan-out: record all vitals concurrently
        var recordTasks = vitals.Select(vital =>
        {
            string vitalKey = MakeVitalKey(PatientId, dateTimeTaken, vital.Key);
            var vitalGrain = GrainFactory.GetGrain<IVitalGrain>(vitalKey);
            var vitalQualifiers = qualifiers != null && qualifiers.TryGetValue(vital.Key, out var q)
                ? q : null;

            return (VitalKey: vitalKey, VitalType: vital.Key, Value: vital.Value,
                Task: vitalGrain.RecordVitalAsync(
                    PatientId, vital.Key, vital.Value, null,
                    dateTimeTaken, locationId, locationName,
                    enteredById, enteredByName, vitalQualifiers, null));
        }).ToList();

        await Task.WhenAll(recordTasks.Select(r => r.Task));

        // Update index and patient cache (sequential — single-writer grains)
        var vitalIndex = GetVitalIndex();
        foreach (var r in recordTasks)
        {
            // Add to the full-history index
            await vitalIndex.AddVitalKeyAsync(r.VitalKey, dateTimeTaken, r.VitalType);

            // Add to the embedded recent vitals cache on the patient grain
            var summary = new VitalSummary
            {
                VitalId = r.VitalKey,
                VitalType = r.VitalType,
                Value = r.Value,
                DateTimeTaken = dateTimeTaken
            };
            await patientGrain.AddRecentVitalAsync(summary, displayCount);
        }
    }

    public async Task<List<VitalSummary>> GetLatestVitalsAsync()
    {
        // Hot path: return the embedded recent vitals cache — zero fan-out
        var patientGrain = GetPatientGrain();
        List<VitalSummary> recent = await patientGrain.GetRecentVitalsAsync();

        // Group by type, take latest per type (like CPRS cover sheet VITL section)
        return recent
            .GroupBy(v => v.VitalType)
            .Select(g => g.OrderByDescending(v => v.DateTimeTaken).First())
            .ToList();
    }

    public async Task<List<VitalSummary>> GetVitalHistoryAsync(DateTime? from, DateTime? to, int maxCount)
    {
        // Cold path: query the index, then fan-out only for the requested slice
        var vitalIndex = GetVitalIndex();

        List<VitalIndexEntry> entries;
        if (from.HasValue && to.HasValue)
            entries = await vitalIndex.GetKeysByDateRangeAsync(from.Value, to.Value);
        else if (to.HasValue)
            entries = await vitalIndex.GetKeysBeforeDateAsync(to.Value, maxCount);
        else
            entries = await vitalIndex.GetAllKeysAsync();

        // Limit the fan-out
        var slice = entries.Take(maxCount).ToList();

        // Fan-out to get full vital details
        var tasks = slice.Select(e =>
            GrainFactory.GetGrain<IVitalGrain>(e.VitalGrainKey).GetVitalAsync()).ToList();
        var states = await Task.WhenAll(tasks);

        return states
            .Where(s => !s.IsEnteredInError)
            .Select(s => new VitalSummary
            {
                VitalId = s.VitalId,
                VitalType = s.VitalType ?? "",
                Value = s.Value ?? "",
                Units = s.Units,
                DateTimeTaken = s.DateTimeTaken,
                AbnormalFlag = s.AbnormalFlag
            })
            .OrderByDescending(v => v.DateTimeTaken)
            .ToList();
    }

    public async Task<List<VitalSummary>> GetVitalHistoryByTypeAsync(
        string vitalType, DateTime from, DateTime to)
    {
        var vitalIndex = GetVitalIndex();
        List<VitalIndexEntry> entries = await vitalIndex.GetKeysByTypeAndDateRangeAsync(vitalType, from, to);

        var tasks = entries.Select(e =>
            GrainFactory.GetGrain<IVitalGrain>(e.VitalGrainKey).GetVitalAsync()).ToList();
        var states = await Task.WhenAll(tasks);

        return states
            .Where(s => !s.IsEnteredInError)
            .Select(s => new VitalSummary
            {
                VitalId = s.VitalId,
                VitalType = s.VitalType ?? "",
                Value = s.Value ?? "",
                Units = s.Units,
                DateTimeTaken = s.DateTimeTaken,
                AbnormalFlag = s.AbnormalFlag
            })
            .OrderByDescending(v => v.DateTimeTaken)
            .ToList();
    }

    // ─── Medication Workflow ─────────────────────────────────────────────

    public async Task<List<MedicationSummary>> GetActiveMedicationsAsync()
    {
        var patientGrain = GetPatientGrain();
        var pharmacyIds = await patientGrain.GetPharmacyIdsAsync();

        // Fan-out: fire all grain calls concurrently
        var tasks = pharmacyIds.Select(id => GrainFactory.GetGrain<IPharmacyGrain>(id).GetPrescriptionAsync()).ToList();
        var states = await Task.WhenAll(tasks);

        return states
            .Where(state => state.Status is "ACTIVE" or "HOLD")
            .Select(state => new MedicationSummary
            {
                PrescriptionId = state.PrescriptionId,
                DrugName = state.DrugName ?? "",
                Sig = state.Sig,
                Status = state.Status ?? "",
                FillDate = state.FillDate,
                RefillsRemaining = state.RefillsRemaining
            })
            .ToList();
    }

    // ─── Allergy Workflow (CWAD "A" flag) ────────────────────────────────

    public async Task<string> RecordAllergyAsync(
        string allergen, string allergenType, string? reactantId,
        string? observedHistorical, List<string>? reactions,
        string? severity, string? originatorId, string? originatorName,
        string? comments)
    {
        var allergyId = $"ALLERGY-{Guid.NewGuid()}";

        var entry = new AllergyEntry
        {
            AllergyId = allergyId,
            Allergen = allergen,
            AllergenType = allergenType,
            AllergenId = reactantId,
            ReactionType = "ALLERGY",
            Reactions = reactions ?? new List<string>(),
            Severity = severity,
            ReactionDateTime = DateTime.UtcNow,
            ObservedHistorical = observedHistorical,
            OriginatorId = originatorId,
            OriginatorName = originatorName,
            Comments = comments,
            OriginationDateTime = DateTime.UtcNow,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };

        await GetPatientGrain().AddAllergyAsync(entry);

        return allergyId;
    }

    public async Task<List<AllergySummary>> GetAllergiesAsync()
    {
        List<AllergyEntry> entries = await GetPatientGrain().GetAllergiesAsync();

        return entries
            .Select(e => new AllergySummary
            {
                AllergyId = e.AllergyId,
                Allergen = e.Allergen,
                Severity = e.Severity,
                Reactions = e.Reactions ?? [],
                AllergenType = e.AllergenType,
                ObservedHistorical = e.ObservedHistorical
            })
            .ToList();
    }
}
