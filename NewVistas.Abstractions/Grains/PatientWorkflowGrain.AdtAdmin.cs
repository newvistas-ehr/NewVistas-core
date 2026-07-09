// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public partial class PatientWorkflowGrain
{
    // ─── Means Test (File #408.31) ───────────────────────────────────────

    public async Task<string> RecordMeansTestAsync(
        string testType, DateTime dateOfTest,
        decimal? annualIncome, decimal? netWorth, int? numberOfDependents,
        string? eligibilityStatus, string? priorityGroup,
        string? completedById, string? completedByName, string? comments)
    {
        var mtId = $"MT-{Guid.NewGuid()}";
        var entry = new MeansTestEntry
        {
            MeansTestId = mtId,
            TestType = testType,
            DateOfTest = dateOfTest,
            AnnualIncome = annualIncome,
            NetWorth = netWorth,
            NumberOfDependents = numberOfDependents,
            EligibilityStatus = eligibilityStatus,
            PriorityGroup = priorityGroup,
            CompletedById = completedById,
            CompletedByName = completedByName,
            Comments = comments,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };
        await GetPatientGrain().AddMeansTestAsync(entry);
        return mtId;
    }

    public async Task<List<MeansTestSummary>> GetMeansTestsAsync()
    {
        var entries = await GetPatientGrain().GetMeansTestsAsync();
        return entries.OrderByDescending(s => s.DateOfTest)
            .Select(s => new MeansTestSummary
            {
                MeansTestId = s.MeansTestId, TestType = s.TestType,
                DateOfTest = s.DateOfTest, Status = s.Status,
                EligibilityStatus = s.EligibilityStatus, PriorityGroup = s.PriorityGroup
            }).ToList();
    }

    // ─── Service Connected Conditions (File #2.04) ───────────────────────

    public async Task<string> RecordServiceConnectedConditionAsync(
        string condition, string? diagnosisCode, int? disabilityPercentage,
        bool isServiceConnected, DateTime? effectiveDate,
        string? extremityAffected, string? comments)
    {
        var scId = $"SC-{Guid.NewGuid()}";
        var entry = new ScConditionEntry
        {
            ConditionId = scId,
            Condition = condition,
            DiagnosisCode = diagnosisCode,
            DisabilityPercentage = disabilityPercentage,
            IsServiceConnected = isServiceConnected,
            EffectiveDate = effectiveDate,
            ExtremityAffected = extremityAffected,
            Comments = comments,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };
        await GetPatientGrain().AddScConditionAsync(entry);
        return scId;
    }

    public async Task<List<ServiceConnectedSummary>> GetServiceConnectedConditionsAsync()
    {
        var entries = await GetPatientGrain().GetScConditionsAsync();
        return entries
            .Select(s => new ServiceConnectedSummary
            {
                ConditionId = s.ConditionId, Condition = s.Condition,
                DiagnosisCode = s.DiagnosisCode, DisabilityPercentage = s.DisabilityPercentage,
                IsServiceConnected = s.IsServiceConnected, Status = s.Status
            }).ToList();
    }

    // ─── ADT — Admit/Discharge/Transfer (File #405) ─────────────────────

    /// <summary>Grain for the unit that owns rooms/beds/census — the single source of bed truth.</summary>
    private IInpatientUnitGrain Unit(string institutionId, string unitId)
        => GrainFactory.GetGrain<IInpatientUnitGrain>($"UNIT:{institutionId}:{unitId}");

    public async Task<string> RecordAdmissionAsync(
        DateTime movementDateTime, string institutionId, string unitId, string? bedId,
        string? treatingSpecialtyName,
        string? attendingPhysicianId, string? attendingPhysicianName,
        string? admissionDiagnosis, string? comments)
    {
        if (string.IsNullOrWhiteSpace(institutionId) || string.IsNullOrWhiteSpace(unitId))
            throw new InvalidOperationException("institutionId and unitId are required for an admission.");

        IInpatientUnitGrain unit = Unit(institutionId, unitId);
        InpatientUnitState unitState = await unit.GetAsync();
        if (string.IsNullOrEmpty(unitState.Name) || !unitState.IsActive)
            throw new InvalidOperationException($"Unknown or inactive unit '{unitId}' at institution '{institutionId}'.");

        PatientState patientState = await GetPatientGrain().GetPatientAsync();

        // Generate the movement id FIRST so the unit records it (idempotency key), and
        // occupy the bed BEFORE writing ADT — a rejected placement is a clean failure
        // with no movement record.
        var adtId = $"ADT-{Guid.NewGuid()}";
        await unit.AdmitPatientAsync(new UnitAdmissionRequest
        {
            PatientId = PatientId,
            PatientName = patientState.Name,
            MovementId = adtId,
            BedId = bedId,
            AdmitDate = movementDateTime,
            TreatingSpecialty = treatingSpecialtyName,
            AttendingPhysicianId = attendingPhysicianId,
            AttendingPhysicianName = attendingPhysicianName
        });

        var grain = GrainFactory.GetGrain<IAdtGrain>(adtId);
        try
        {
            await grain.RecordAdmissionAsync(PatientId, movementDateTime, unitId, unitState.Name,
                bedId, null, treatingSpecialtyName ?? unitState.DefaultTreatingSpecialty,
                attendingPhysicianId, attendingPhysicianName,
                null, admissionDiagnosis, comments, institutionId);
        }
        catch
        {
            // Compensate: the bed was occupied but the movement never recorded.
            await unit.ReleasePatientAsync(PatientId, adtId);
            throw;
        }
        await AppendCappedIdAsync(PatientHistoryDomains.Adt, adtId, DateTime.UtcNow);

        // ADR-002 Phase 4b: admitting the patient establishes the attending physician's treatment
        // relationship (tied to this admission episode) — the inpatient attending gets frictionless
        // access without a hand-curated authorized list.
        if (!string.IsNullOrWhiteSpace(attendingPhysicianId))
            await Pac().EstablishRelationshipAsync(attendingPhysicianId, TreatmentRelationshipReason.Admission, adtId, null);

        // The patient's current-admission pointer (File #2 movement fields).
        await GetPatientGrain().UpdateCurrentAdmissionAsync(adtId, bedId, unitState.Name);

        return adtId;
    }

    public async Task RecordDischargeAsync(string movementId, DateTime dischargeDateTime,
        string? dischargeDiagnosis, string? disposition, string? comments)
    {
        IAdtGrain grain = GrainFactory.GetGrain<IAdtGrain>(movementId);
        AdtState st = await grain.GetMovementAsync();

        // Release the bed (→ Dirty for EVS turnover) or remove the boarder — idempotent
        // no-op when the patient isn't on the unit.
        if (!string.IsNullOrEmpty(st.InstitutionId) && !string.IsNullOrEmpty(st.WardLocationId))
            await Unit(st.InstitutionId, st.WardLocationId).ReleasePatientAsync(PatientId, movementId);

        await grain.RecordDischargeAsync(dischargeDateTime, dischargeDiagnosis, disposition, comments);
        await GetPatientGrain().UpdateCurrentAdmissionAsync(null, null, null);
    }

    public async Task<string> RecordTransferAsync(
        string currentMovementId, DateTime transferDateTime,
        string toInstitutionId, string toUnitId, string? toBedId,
        string? toSpecialtyId, string? toSpecialtyName,
        string? attendingPhysicianId, string? attendingPhysicianName, string? comments,
        bool overrideReservation = false)
    {
        if (string.IsNullOrWhiteSpace(toInstitutionId) || string.IsNullOrWhiteSpace(toUnitId))
            throw new InvalidOperationException("toInstitutionId and toUnitId are required for a transfer.");

        IAdtGrain sourceGrain = GrainFactory.GetGrain<IAdtGrain>(currentMovementId);
        AdtState sourceState = await sourceGrain.GetMovementAsync();

        IInpatientUnitGrain toUnit = Unit(toInstitutionId, toUnitId);
        InpatientUnitState toUnitState = await toUnit.GetAsync();
        if (string.IsNullOrEmpty(toUnitState.Name) || !toUnitState.IsActive)
            throw new InvalidOperationException($"Unknown or inactive unit '{toUnitId}' at institution '{toInstitutionId}'.");

        string transferId = $"ADT-{Guid.NewGuid()}";

        bool sameUnit = sourceState.InstitutionId == toInstitutionId
                        && sourceState.WardLocationId == toUnitId;
        if (sameUnit && !string.IsNullOrWhiteSpace(toBedId))
        {
            // Intra-unit bed swap — one atomic grain call; the old bed goes to Dirty.
            await toUnit.MoveOccupantAsync(PatientId, toBedId, transferId, overrideReservation);
        }
        else if (!sameUnit)
        {
            // Occupy the target first; release the source AFTER the ADT write below —
            // a crash in between self-heals because ReleasePatientAsync is idempotent.
            PatientState patientState = await GetPatientGrain().GetPatientAsync();
            await toUnit.AdmitPatientAsync(new UnitAdmissionRequest
            {
                PatientId = PatientId,
                PatientName = patientState.Name,
                MovementId = transferId,
                BedId = toBedId,
                AdmitDate = sourceState.AdmissionDateTime ?? sourceState.MovementDateTime,
                TreatingSpecialty = toSpecialtyName,
                AttendingPhysicianId = attendingPhysicianId,
                AttendingPhysicianName = attendingPhysicianName,
                OverrideReservation = overrideReservation
            });
        }

        IAdtGrain transferGrain = GrainFactory.GetGrain<IAdtGrain>(transferId);
        try
        {
            await transferGrain.RecordAsTransferAsync(
                PatientId,
                sourceState.AdmissionDateTime ?? sourceState.MovementDateTime,
                transferDateTime,
                toUnitId, toUnitState.Name, toBedId,
                toSpecialtyId, toSpecialtyName,
                attendingPhysicianId, attendingPhysicianName, comments, toInstitutionId);
        }
        catch when (!sameUnit)
        {
            await toUnit.ReleasePatientAsync(PatientId, transferId);
            throw;
        }
        await AppendCappedIdAsync(PatientHistoryDomains.Adt, transferId, DateTime.UtcNow);

        if (!sameUnit && !string.IsNullOrEmpty(sourceState.InstitutionId) && !string.IsNullOrEmpty(sourceState.WardLocationId))
            await Unit(sourceState.InstitutionId, sourceState.WardLocationId)
                .ReleasePatientAsync(PatientId, currentMovementId);

        // ADR-002 Phase 4b: a transfer may hand the patient to a new attending — establish their
        // treatment relationship too (the receiving service becomes authorized on arrival).
        if (!string.IsNullOrWhiteSpace(attendingPhysicianId))
            await Pac().EstablishRelationshipAsync(attendingPhysicianId, TreatmentRelationshipReason.Admission, transferId, null);

        await GetPatientGrain().UpdateCurrentAdmissionAsync(transferId, toBedId, toUnitState.Name);

        return transferId;
    }

    public async Task<List<AdtSummary>> GetAdtMovementsAsync()
    {
        var ids = await GetPatientGrain().GetAdtIdsAsync();
        var tasks = ids.Select(id => GrainFactory.GetGrain<IAdtGrain>(id).GetMovementAsync()).ToList();
        var states = await Task.WhenAll(tasks);
        return states.OrderByDescending(s => s.MovementDateTime)
            .Select(s => new AdtSummary
            {
                MovementId = s.MovementId, MovementType = s.TransactionType,
                MovementDateTime = s.MovementDateTime, WardLocationName = s.WardLocationName,
                RoomBed = s.RoomBed, AttendingPhysicianName = s.AttendingPhysicianName,
                Status = s.TransactionType switch { "DISCHARGE" => "DISCHARGED", "TRANSFER" => "TRANSFERRED", _ => "ADMITTED" }
            }).ToList();
    }

    /// <summary>
    /// Paged full ADT movement history (newest first); default reads return only the recent window.
    /// </summary>
    public async Task<List<AdtSummary>> GetAdtHistoryAsync(int offset, int maxResults)
    {
        var ids = await GetHistoryPageIdsAsync(PatientHistoryDomains.Adt, offset, maxResults);
        var tasks = ids.Select(id => GrainFactory.GetGrain<IAdtGrain>(id).GetMovementAsync()).ToList();
        var states = await Task.WhenAll(tasks);
        return states
            .Select(s => new AdtSummary
            {
                MovementId = s.MovementId, MovementType = s.TransactionType,
                MovementDateTime = s.MovementDateTime, WardLocationName = s.WardLocationName,
                RoomBed = s.RoomBed, AttendingPhysicianName = s.AttendingPhysicianName,
                Status = s.TransactionType switch { "DISCHARGE" => "DISCHARGED", "TRANSFER" => "TRANSFERRED", _ => "ADMITTED" }
            }).ToList();
    }

    public Task<List<UnitCensusEntry>> GetUnitCensusAsync(string institutionId, string unitId)
        => Unit(institutionId, unitId).GetCensusAsync();

    public async Task<List<UnitCapacitySummary>> GetUnitDirectoryAsync(string institutionId)
    {
        IBedCapacityGrain capacity = GrainFactory.GetGrain<IBedCapacityGrain>($"BED-CAPACITY:{institutionId}");
        return await capacity.GetUnitsAsync();
    }

    // ─── Private Helpers ─────────────────────────────────────────────────

    private static PatientIndexEntry BuildIndexEntry(PatientState state)
    {
        string ssnLast4 = string.Empty;
        if (!string.IsNullOrEmpty(state.SocialSecurityNumber))
        {
            string ssn = state.SocialSecurityNumber.Replace("-", string.Empty);
            if (ssn.Length >= 4 && ssn.All(char.IsDigit))
                ssnLast4 = ssn[^4..];
        }

        return new PatientIndexEntry
        {
            PatientId   = state.PatientId,
            Name        = state.Name,
            DateOfBirth = state.DateOfBirth,
            Sex         = state.Sex,
            SsnLast4    = ssnLast4,
            Dfn         = state.Dfn,
            Icn         = state.Icn,
            IsActive    = state.IsActive
        };
    }

    private static PatientDemographicsSummary BuildDemographicsSummary(PatientState state)
    {
        int? age = null;
        if (state.DateOfBirth.HasValue)
        {
            age = (int)((DateTime.UtcNow - state.DateOfBirth.Value).TotalDays / 365.25);
        }

        return new PatientDemographicsSummary
        {
            Name = state.Name ?? "",
            Sex = state.Sex ?? "",
            DateOfBirth = state.DateOfBirth,
            Ssn = state.SocialSecurityNumber,
            Age = age,
            LocationName = state.CurrentMovement,
            RoomBed = state.RoomBed,
            IsAdmitted = !string.IsNullOrEmpty(state.CurrentAdmission),
            IsVeteran = string.Equals(state.Veteran, "Y", StringComparison.OrdinalIgnoreCase),
            IsServiceConnected = state.ServiceConnectedPercentage > 0,
            ServiceConnectedPercent = state.ServiceConnectedPercentage
        };
    }

    /// <summary>
    /// Builds CWAD flags by examining patient data.
    /// Mirrors $$CWAD^ORQPT2 which checks for Crises/Warnings/Allergies/Directives.
    /// Now checks TIU documents for crisis notes and advance directives.
    /// </summary>
    private async Task<CwadFlags> BuildCwadFlagsAsync(PatientState state)
    {
        var allergies = state.Allergies ?? [];

        // COMPLETE document set: state.TiuDocumentIds is a capped recent
        // window — an old advance directive or crisis note must never fall
        // off the CWAD flags.
        var documentIds = await GetCompleteIdsAsync(PatientHistoryDomains.Tiu);

        var hasCrisis = false;
        var hasDirectives = false;

        if (documentIds.Count > 0)
        {
            var tasks = documentIds.Select(id => GrainFactory.GetGrain<ITiuDocumentGrain>(id).GetDocumentAsync()).ToList();
            var docs = await Task.WhenAll(tasks);

            hasCrisis = docs.Any(d =>
                d.DocumentType.Contains("CRISIS", StringComparison.OrdinalIgnoreCase) &&
                d.Status != "RETRACTED");
            hasDirectives = docs.Any(d =>
                d.DocumentType.Contains("ADVANCE DIRECTIVE", StringComparison.OrdinalIgnoreCase) &&
                d.Status != "RETRACTED");
        }

        return new CwadFlags
        {
            HasCrisisNotes = hasCrisis,
            HasWarnings = false,
            HasAllergies = allergies.Count > 0,
            HasAdvanceDirectives = hasDirectives
        };
    }

    private async Task<List<ReminderSummary>> LoadRemindersAsync(IPatientGrain patientGrain)
    {
        // COMPLETE reminder set: the DUE filter is an active-set query — a
        // due reminder older than the capped recent window must still fire.
        var reminderIds = await GetCompleteIdsAsync(PatientHistoryDomains.Reminder);

        // Fan-out: fire all grain calls concurrently
        var tasks = reminderIds.Select(id => GrainFactory.GetGrain<IClinicalReminderGrain>(id).GetReminderAsync()).ToList();
        var states = await Task.WhenAll(tasks);

        return states
            .Where(state => state.Status == "DUE")
            .Select(state => new ReminderSummary
            {
                ReminderId = state.ReminderId,
                ReminderName = state.ReminderName ?? "",
                Status = state.Status ?? "",
                DueDate = state.NextDueDate
            })
            .ToList();
    }

    private async Task<List<LabResultSummary>> LoadRecentLabsAsync(IPatientGrain patientGrain)
    {
        var labIds = await patientGrain.GetLabTestIdsAsync();

        // Fan-out: fire all grain calls concurrently
        var tasks = labIds.Select(id => GrainFactory.GetGrain<ILabTestGrain>(id).GetLabTestAsync()).ToList();
        var states = await Task.WhenAll(tasks);

        return states
            .Select(state => new LabResultSummary
            {
                LabTestId = state.LabTestId,
                TestName = state.TestName ?? "",
                ResultValue = state.ResultValue,
                Units = state.ResultUnit,
                Flag = state.AbnormalFlag,
                Status = state.Status ?? "",
                CollectionDate = state.CollectionDateTime
            })
            .OrderByDescending(l => l.CollectionDate)
            .ToList();
    }

    private async Task<List<ProblemSummary>> GetProblemsAsync(bool activeOnly)
    {
        List<ProblemEntry> entries = await GetPatientGrain().GetProblemsAsync();

        return entries
            .Where(e => !activeOnly || e.Status == "ACTIVE")
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

    // ─── PCE — Patient Care Encounters (File #9000010) ───────────────────

    private IVisitGrain GetVisitGrain(string visitId) =>
        GrainFactory.GetGrain<IVisitGrain>($"PCE-VISIT:{visitId}");

    private IPatientVisitIndexGrain GetVisitIndexGrain() =>
        GrainFactory.GetGrain<IPatientVisitIndexGrain>($"PCE-VISITS:{PatientId}");

    private static PceVisitEntry BuildVisitEntry(string visitId, VisitState s) => new()
    {
        VisitId = visitId,
        VisitDateTime = s.VisitDateTime,
        ServiceCategory = s.ServiceCategory,
        LocationName = s.LocationName,
        PrimaryProviderName = s.PrimaryProviderName,
        Status = s.Status,
        DiagnosisCount = s.Diagnoses.Count,
        ProcedureCount = s.Procedures.Count
    };

    public async Task<string> CreateEncounterAsync(
        DateTime visitDateTime,
        string serviceCategory,
        string? locationId,
        string? locationName,
        string? visitType,
        string? stopCode,
        string? primaryProviderId,
        string? primaryProviderName,
        string? linkedAppointmentId,
        string? comments)
    {
        string visitId = Guid.NewGuid().ToString("N");
        IVisitGrain visitGrain = GetVisitGrain(visitId);
        await visitGrain.CreateVisitAsync(PatientId, visitDateTime, serviceCategory,
            locationId, locationName, visitType, stopCode, null,
            primaryProviderId, primaryProviderName, linkedAppointmentId, comments);
        VisitState state = await visitGrain.GetVisitAsync();
        await GetVisitIndexGrain().AddOrUpdateVisitAsync(BuildVisitEntry(visitId, state));
        return visitId;
    }

    public async Task<VisitState> GetEncounterAsync(string visitId) =>
        await GetVisitGrain(visitId).GetVisitAsync();

    public async Task<List<PceVisitEntry>> GetEncounterListAsync(int maxResults) =>
        await GetVisitIndexGrain().GetVisitsAsync(maxResults);

    public async Task CheckOutEncounterAsync(string visitId, DateTime checkOutDateTime)
    {
        IVisitGrain visitGrain = GetVisitGrain(visitId);
        await visitGrain.CheckOutAsync(checkOutDateTime);
        VisitState state = await visitGrain.GetVisitAsync();
        await GetVisitIndexGrain().AddOrUpdateVisitAsync(BuildVisitEntry(visitId, state));
    }

    public async Task AddEncounterDiagnosisAsync(
        string visitId,
        string icd10Code,
        string description,
        bool isPrimary,
        string? providerId,
        string? providerName)
    {
        IVisitGrain visitGrain = GetVisitGrain(visitId);
        await visitGrain.AddEncounterDiagnosisAsync(icd10Code, description, isPrimary, providerId, providerName);
        VisitState state = await visitGrain.GetVisitAsync();
        await GetVisitIndexGrain().AddOrUpdateVisitAsync(BuildVisitEntry(visitId, state));
    }

    public async Task AddEncounterProcedureAsync(
        string visitId,
        string cptCode,
        string description,
        int quantity,
        string? modifiers,
        string? providerId,
        string? providerName)
    {
        IVisitGrain visitGrain = GetVisitGrain(visitId);
        await visitGrain.AddProcedureAsync(cptCode, description, quantity, modifiers, providerId, providerName);
        VisitState state = await visitGrain.GetVisitAsync();
        await GetVisitIndexGrain().AddOrUpdateVisitAsync(BuildVisitEntry(visitId, state));
    }

    public async Task CancelEncounterAsync(string visitId, string? reason)
    {
        IVisitGrain visitGrain = GetVisitGrain(visitId);
        await visitGrain.CancelVisitAsync(reason);
        VisitState state = await visitGrain.GetVisitAsync();
        await GetVisitIndexGrain().AddOrUpdateVisitAsync(BuildVisitEntry(visitId, state));
    }

    /// <summary>
    /// Matches order state against filter codes from ORWORR XGET.
    /// Filter codes from ORDER STATUS file (#100.01):
    ///   1=DISCONTINUED, 2=COMPLETE, 3=HOLD, 4=FLAGGED, 5=PENDING,
    ///   6=ACTIVE, 7=EXPIRED, 8=SCHEDULED, 9=PARTIAL RESULTS,
    ///   10=DELAYED, 11=UNRELEASED, 12=DC/EDIT, 13=CANCELLED, 15=LAPSED
    /// ORWORR AGET filter codes (different!):
    ///   1=All, 2=Current, 3=Discontinued, 4=Completed/Expired,
    ///   5=Expiring, 7=Pending, 11=Unsigned
    /// </summary>
    private static bool MatchesOrderFilter(OrderState state, int filter)
    {
        return filter switch
        {
            1 => true, // All
            2 => state.Status is "Pending" or "Active" or "Hold", // Current
            3 => state.Status == "Discontinued", // Discontinued
            4 => state.Status is "Completed" or "Expired", // Completed/Expired
            5 => state.Status == "Active", // Expiring (active orders nearing stop date)
            7 => state.Status == "Pending", // Pending
            11 => string.IsNullOrEmpty(state.ElectronicSignature), // Unsigned
            _ => true
        };
    }

    // ─── Audit Trail (VistA AUDIT file #1.1, XUSEC routines) ────────────

    public async Task<string> LogAuditEventAsync(
        string domain,
        string action,
        string entityType,
        string entityId,
        string? userId,
        string? userName,
        string? locationId,
        string? locationName,
        string? details,
        string? oldValue = null,
        string? newValue = null)
    {
        var eventId = $"AUDIT-{Guid.NewGuid()}";
        var eventGrain = GrainFactory.GetGrain<IAuditEventGrain>(eventId);

        // §170.315(d)(2) hash chain: get previous event hash from patient index
        var indexGrain = GrainFactory.GetGrain<IPatientAuditIndexGrain>(PatientId);
        List<AuditEventSummary> recent = await indexGrain.GetRecentEventsAsync(1);
        string previousHash = recent.Count > 0 && !string.IsNullOrEmpty(recent[0].EventHash)
            ? recent[0].EventHash
            : IAuditEventGrain.GenesisHash;

        await eventGrain.RecordAsync(
            PatientId, domain, action, entityType, entityId,
            userId, userName, locationId, locationName,
            details, oldValue, newValue, previousHash);

        // Read back the computed event hash for the index
        AuditEventState recorded = await eventGrain.GetEventAsync();

        var summary = new AuditEventSummary
        {
            EventId = eventId,
            Domain = domain,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            UserName = userName,
            LocationName = locationName,
            Details = details,
            Timestamp = recorded.Timestamp,
            EventHash = recorded.EventHash
        };

        await indexGrain.AddEventAsync(summary);

        return eventId;
    }

    public async Task<List<AuditEventSummary>> GetRecentAuditEventsAsync(int maxResults = 100)
    {
        var indexGrain = GrainFactory.GetGrain<IPatientAuditIndexGrain>(PatientId);
        return await indexGrain.GetRecentEventsAsync(maxResults);
    }

    public async Task<List<AuditEventSummary>> GetAuditEventsAsync(
        string? domain,
        DateTime? from,
        DateTime? to,
        int maxResults = 200)
    {
        var indexGrain = GrainFactory.GetGrain<IPatientAuditIndexGrain>(PatientId);
        return await indexGrain.GetEventsAsync(domain, from, to, maxResults);
    }

    public async Task<List<AuditEventSummary>> GetAuditEventsByEntityAsync(
        string entityType,
        string entityId)
    {
        var indexGrain = GrainFactory.GetGrain<IPatientAuditIndexGrain>(PatientId);
        return await indexGrain.GetEventsByEntityAsync(entityType, entityId);
    }

    public async Task<AuditEventState> GetAuditEventAsync(string eventId)
    {
        var eventGrain = GrainFactory.GetGrain<IAuditEventGrain>(eventId);
        return await eventGrain.GetEventAsync();
    }

    // ── Notifications / Alerts ────────────────────────────────────────────────

    public async Task<string> CreateAlertAsync(
        int notificationType,
        string notificationTypeText,
        string recipientId,
        string recipientName,
        string? sendingPackage,
        string? messageText,
        string? followUpAction,
        bool isCritical,
        string? xqaData)
    {
        string alertId = $"ALERT-{PatientId}-{Guid.NewGuid():N}";
        var alertGrain = GrainFactory.GetGrain<INotificationGrain>(alertId);
        await alertGrain.CreateNotificationAsync(
            PatientId, notificationType, notificationTypeText,
            recipientId, recipientName, sendingPackage,
            messageText, followUpAction, isCritical, xqaData);
        return alertId;
    }

    public async Task<NotificationState> GetAlertAsync(string alertId)
    {
        var alertGrain = GrainFactory.GetGrain<INotificationGrain>(alertId);
        return await alertGrain.GetNotificationAsync();
    }

    public async Task ProcessAlertAsync(string alertId, DateTime processedDateTime, string processedByUserId)
    {
        var alertGrain = GrainFactory.GetGrain<INotificationGrain>(alertId);
        await alertGrain.ProcessAlertAsync(processedDateTime, processedByUserId);
    }

    public async Task DeleteAlertAsync(string alertId, string deletedByUserId)
    {
        var alertGrain = GrainFactory.GetGrain<INotificationGrain>(alertId);
        await alertGrain.DeleteAlertAsync(deletedByUserId);
    }

    public async Task ForwardAlertAsync(
        string alertId,
        string toRecipientId,
        string toRecipientName,
        string forwardType,
        string? comment,
        string forwardedByUserId)
    {
        var alertGrain = GrainFactory.GetGrain<INotificationGrain>(alertId);
        await alertGrain.ForwardAlertAsync(toRecipientId, toRecipientName, forwardType, comment, forwardedByUserId);
    }

    // ─── SC Condition Depth (File #2.04 Phase 5) ────────────────────────────

    public async Task SetServiceConnectedPercentageAsync(string conditionId, int percentage)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetScConditionAsync(conditionId);
        if (entry != null)
        {
            entry.ServiceConnectedPercentage = percentage;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateScConditionAsync(entry);
        }
    }

    public async Task RecordScRatingDecisionAsync(string conditionId, DateTime decisionDate, string? decisionId)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetScConditionAsync(conditionId);
        if (entry != null)
        {
            entry.RatingDecisionDate = decisionDate;
            entry.RatingDecisionId = decisionId;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateScConditionAsync(entry);
        }
    }

    public async Task AddRatedDisabilityAsync(string conditionId, string conditionName, int ratingPercentage, DateTime effectiveDate, string? diagnosticCode, bool isStatic)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetScConditionAsync(conditionId);
        if (entry != null)
        {
            entry.RatedDisabilities.Add(new RatedDisability
            {
                ConditionName = conditionName,
                RatingPercentage = ratingPercentage,
                EffectiveDate = effectiveDate,
                DiagnosticCode = diagnosticCode,
                IsStatic = isStatic
            });
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateScConditionAsync(entry);
        }
    }

    public async Task CalculateScCombinedRatingAsync(string conditionId)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetScConditionAsync(conditionId);
        if (entry != null)
        {
            // VA combined rating formula: sort descending, iterative combination
            var ratings = entry.RatedDisabilities
                .OrderByDescending(r => r.RatingPercentage)
                .Select(r => r.RatingPercentage)
                .ToList();

            if (ratings.Count > 0)
            {
                double remaining = 100.0;
                foreach (int rating in ratings)
                {
                    remaining -= remaining * rating / 100.0;
                }
                // Round to nearest 10
                int combined = (int)(Math.Round((100.0 - remaining) / 10.0) * 10);
                entry.CombinedRating = Math.Min(combined, 100);
            }
            else
            {
                entry.CombinedRating = 0;
            }

            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateScConditionAsync(entry);
        }
    }

    public async Task SetScAppealStatusAsync(string conditionId, string status, DateTime? appealFiledDate)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetScConditionAsync(conditionId);
        if (entry != null)
        {
            entry.AppealStatus = status;
            entry.AppealFiledDate = appealFiledDate;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateScConditionAsync(entry);
        }
    }

    public async Task RecordScExamAsync(string conditionId, DateTime examDate, string? examiningFacility, DateTime? nextExamDueDate)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetScConditionAsync(conditionId);
        if (entry != null)
        {
            entry.LastExamDate = examDate;
            entry.ExaminingFacility = examiningFacility;
            entry.NextExamDueDate = nextExamDueDate;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateScConditionAsync(entry);
        }
    }

    public async Task SetScPermanentAndTotalAsync(string conditionId, bool isPermanentAndTotal)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetScConditionAsync(conditionId);
        if (entry != null)
        {
            entry.IsPermanentAndTotal = isPermanentAndTotal;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateScConditionAsync(entry);
        }
    }

    public async Task AddScConditionNoteAsync(string conditionId, string authorName, string noteText)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetScConditionAsync(conditionId);
        if (entry != null)
        {
            entry.Notes.Add(new ScConditionNote
            {
                NoteDate = DateTime.UtcNow,
                AuthorName = authorName,
                NoteText = noteText
            });
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateScConditionAsync(entry);
        }
    }

    // ─── Means Test Depth (File #408.31 Phase 5) ────────────────────────────

    public async Task RecordMeansTestIncomeAsync(string meansTestId, decimal veteranGrossIncome, decimal? spouseGrossIncome, decimal? dependentIncome)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetMeansTestAsync(meansTestId);
        if (entry != null)
        {
            entry.VeteranGrossIncome = veteranGrossIncome;
            entry.SpouseGrossIncome = spouseGrossIncome;
            entry.DependentIncome = dependentIncome;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateMeansTestAsync(entry);
        }
    }

    public async Task RecordMeansTestAssetsAsync(string meansTestId, decimal? totalNetWorth, decimal? propertyValue, decimal? otherAssets)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetMeansTestAsync(meansTestId);
        if (entry != null)
        {
            entry.TotalNetWorth = totalNetWorth;
            entry.PropertyValue = propertyValue;
            entry.OtherAssets = otherAssets;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateMeansTestAsync(entry);
        }
    }

    public async Task RecordMeansTestExpensesAsync(string meansTestId, decimal deductibleExpenses)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetMeansTestAsync(meansTestId);
        if (entry != null)
        {
            entry.DeductibleExpenses = deductibleExpenses;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateMeansTestAsync(entry);
        }
    }

    public async Task CalculateMeansTestAdjustedIncomeAsync(string meansTestId)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetMeansTestAsync(meansTestId);
        if (entry != null)
        {
            decimal veteranIncome = entry.VeteranGrossIncome ?? 0m;
            decimal spouseIncome = entry.SpouseGrossIncome ?? 0m;
            decimal dependentIncome = entry.DependentIncome ?? 0m;
            decimal deductible = entry.DeductibleExpenses ?? 0m;
            entry.AdjustedIncome = veteranIncome + spouseIncome + dependentIncome - deductible;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateMeansTestAsync(entry);
        }
    }

    public async Task SetMeansTestGmtThresholdAsync(string meansTestId, decimal gmtThreshold)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetMeansTestAsync(meansTestId);
        if (entry != null)
        {
            entry.GmtThreshold = gmtThreshold;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateMeansTestAsync(entry);
        }
    }

    public async Task DetermineMeansTestHardshipAsync(string meansTestId, string determination)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetMeansTestAsync(meansTestId);
        if (entry != null)
        {
            entry.HardshipDetermination = determination;
            entry.HardshipDecisionDate = DateTime.UtcNow;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateMeansTestAsync(entry);
        }
    }

    public async Task SetMeansTestCopayResultAsync(string meansTestId, string result)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetMeansTestAsync(meansTestId);
        if (entry != null)
        {
            entry.CopayTestResult = result;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateMeansTestAsync(entry);
        }
    }

    public async Task AddMeansTestDependentAsync(string meansTestId, string name, string relationship, decimal income, decimal netWorth, DateTime? dateOfBirth)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetMeansTestAsync(meansTestId);
        if (entry != null)
        {
            entry.Dependents.Add(new MeansTestDependent
            {
                Name = name,
                Relationship = relationship,
                Income = income,
                NetWorth = netWorth,
                DateOfBirth = dateOfBirth
            });
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateMeansTestAsync(entry);
        }
    }

    public async Task<List<MeansTestDependent>> GetMeansTestDependentsAsync(string meansTestId)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetMeansTestAsync(meansTestId);
        return entry?.Dependents ?? new List<MeansTestDependent>();
    }

    public async Task RemoveScRatedDisabilityAsync(string conditionId, string conditionName)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetScConditionAsync(conditionId);
        if (entry != null)
        {
            entry.RatedDisabilities.RemoveAll(d => d.ConditionName == conditionName);
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateScConditionAsync(entry);
        }
    }

    public async Task SetScSpecialMonthlyCompensationAsync(string conditionId, string? smcLevel)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetScConditionAsync(conditionId);
        if (entry != null)
        {
            entry.SpecialMonthlyCompensation = smcLevel;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateScConditionAsync(entry);
        }
    }

    public async Task<List<RatedDisability>> GetScRatedDisabilitiesAsync(string conditionId)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetScConditionAsync(conditionId);
        return entry?.RatedDisabilities ?? new List<RatedDisability>();
    }

    public async Task<List<ProstheticsMaintenanceRecord>> GetProstheticsMaintenanceHistoryAsync(string prostheticsId)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetProstheticsItemAsync(prostheticsId);
        return entry?.MaintenanceHistory ?? new List<ProstheticsMaintenanceRecord>();
    }

    public async Task<List<DietModificationEntry>> GetDietModificationHistoryAsync(string dietOrderId)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetDietOrderAsync(dietOrderId);
        return entry?.ModificationHistory ?? new List<DietModificationEntry>();
    }

    public async Task<List<ImmunizationComment>> GetImmunizationCommentsAsync(string immunizationId)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetImmunizationAsync(immunizationId);
        return entry?.ImmunizationComments ?? new List<ImmunizationComment>();
    }

    // ─── Bed Availability Query — DGPM bed control ──────────────────────────

    public async Task<List<InpatientBed>> FindAvailableBedsAsync(
        string institutionId, string? unitId, BedType? bedType)
    {
        // A specific unit → one unit-grain read. Otherwise consult the capacity
        // directory and fan out only to units that actually have placeable beds.
        List<string> unitIds;
        if (!string.IsNullOrEmpty(unitId))
        {
            unitIds = new List<string> { unitId };
        }
        else
        {
            IBedCapacityGrain capacity = GrainFactory.GetGrain<IBedCapacityGrain>($"BED-CAPACITY:{institutionId}");
            List<UnitCapacitySummary> units = await capacity.GetUnitsAsync();
            unitIds = units.Where(u => u.Available > 0).Select(u => u.UnitId).ToList();
        }

        var reads = unitIds.Select(id => Unit(institutionId, id).GetAsync()).ToList();
        InpatientUnitState[] states = await Task.WhenAll(reads);
        return states
            .SelectMany(s => s.Beds)
            .Where(b => b.State == BedLifecycleState.Available)
            .Where(b => bedType is null || b.BedType == bedType)
            .OrderBy(b => b.BedId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<(int Total, int Available, int Occupied)> GetBedCountsAsync(string institutionId)
    {
        IBedCapacityGrain capacity = GrainFactory.GetGrain<IBedCapacityGrain>($"BED-CAPACITY:{institutionId}");
        (int total, int available, int occupied, _, _, _) = await capacity.GetInstitutionTotalsAsync();
        return (total, available, occupied);
    }

    // ─── Advance Directives — DG advance directives (CWAD 'D' flag) ─────────

    private IAdvanceDirectiveGrain AdvDir()
        => GrainFactory.GetGrain<IAdvanceDirectiveGrain>($"ADV-DIR:{PatientId}");

    public Task<GrainStates.AdvanceDirectiveState> GetAdvanceDirectivesAsync()
        => AdvDir().GetAsync();

    public Task UpdateCodeStatusAsync(GrainStates.CodeStatus codeStatus, string updatedByUserId)
        => AdvDir().UpdateCodeStatusAsync(codeStatus, updatedByUserId);

    public Task SetHealthcareProxyAsync(string proxyName, string? proxyPhone, string? proxyRelationship)
        => AdvDir().SetHealthcareProxyAsync(proxyName, proxyPhone, proxyRelationship);

    public Task AddAdvanceDirectiveDocumentAsync(GrainStates.AdvanceDirectiveType directiveType,
        DateTime documentDate, string? documentSource, DateTime? expirationDate, string? notes)
        => AdvDir().AddDocumentAsync(directiveType, documentDate, documentSource, expirationDate, notes);

    // ─── Identity Verification — DG identity verification ────────────────────

    private IIdentityVerificationGrain Identity()
        => GrainFactory.GetGrain<IIdentityVerificationGrain>($"IDENTITY:{PatientId}");

    public Task<GrainStates.IdentityVerificationState> GetIdentityVerificationAsync()
        => Identity().GetAsync();

    public Task<string> RecordIdentityVerificationAsync(
        GrainStates.IdentityDocumentType documentType, string? documentNumber,
        string? issuingAuthority, DateTime? expirationDate,
        GrainStates.IdentityVerificationResult result,
        bool photoOnFile, string? photoReference, string? discrepancyNotes,
        string verifiedByUserId, string verifiedByUserName, string? notes)
        => Identity().RecordVerificationAsync(documentType, documentNumber,
            issuingAuthority, expirationDate, result,
            photoOnFile, photoReference, discrepancyNotes,
            verifiedByUserId, verifiedByUserName, notes);

    public Task UpdatePatientPhotoAsync(string photoReference)
        => Identity().UpdatePhotoAsync(photoReference);

    // ─── Insurance at Registration ───────────────────────────────────────────

    public Task<List<GrainStates.PersonalPolicyIndexEntry>> GetInsuranceAtRegistrationAsync()
        => PolicyIndex().GetAllAsync();
}
