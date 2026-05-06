// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Visit Grain implementation based on VistA PCE VISIT file (#9000010).
///
/// Each encounter instance is an independent grain keyed by visit ID.
/// Supports the full PCE encounter lifecycle: open → check-out / cancel.
/// </summary>
public class VisitGrain : Grain, IVisitGrain
{
    private readonly IPersistentState<VisitState> _state;

    public VisitGrain(
        [PersistentState("visitState", "visitStore")] IPersistentState<VisitState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.VisitId))
        {
            _state.State.VisitId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<VisitState> GetVisitAsync() => Task.FromResult(_state.State);

    public async Task CreateVisitAsync(
        string patientId,
        DateTime visitDateTime,
        string serviceCategory,
        string? locationId,
        string? locationName,
        string? visitType,
        string? stopCode,
        string? secondaryStopCode,
        string? primaryProviderId,
        string? primaryProviderName,
        string? linkedAppointmentId,
        string? comments)
    {
        _state.State.PatientId = patientId;
        _state.State.VisitDateTime = visitDateTime;
        _state.State.ServiceCategory = serviceCategory;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.VisitType = visitType;
        _state.State.StopCode = stopCode;
        _state.State.SecondaryStopCode = secondaryStopCode;
        _state.State.PrimaryProviderId = primaryProviderId;
        _state.State.PrimaryProviderName = primaryProviderName;
        _state.State.LinkedAppointmentId = linkedAppointmentId;
        _state.State.Comments = comments;
        _state.State.Status = "OPEN";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        // Auto-add primary provider to providers list
        if (!string.IsNullOrEmpty(primaryProviderId))
        {
            _state.State.Providers.Add(new PceEncounterProvider
            {
                ProviderId = primaryProviderId,
                ProviderName = primaryProviderName ?? string.Empty,
                Role = "PRIMARY",
                IsPrimary = true
            });
        }

        await _state.WriteStateAsync();
    }

    public async Task CheckOutAsync(DateTime checkOutDateTime)
    {
        _state.State.CheckOutDateTime = checkOutDateTime;
        _state.State.Status = "CHECKED OUT";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddEncounterDiagnosisAsync(
        string icd10Code,
        string description,
        bool isPrimary,
        string? providerId,
        string? providerName)
    {
        // If setting a new primary, demote any existing primary
        if (isPrimary)
        {
            foreach (var dx in _state.State.Diagnoses)
                dx.IsPrimary = false;
        }

        _state.State.Diagnoses.Add(new PceEncounterDiagnosis
        {
            Icd10Code = icd10Code,
            Description = description,
            IsPrimary = isPrimary,
            ProviderId = providerId,
            ProviderName = providerName,
            RecordedDateTime = DateTime.UtcNow
        });

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddProcedureAsync(
        string cptCode,
        string description,
        int quantity,
        string? modifiers,
        string? providerId,
        string? providerName)
    {
        _state.State.Procedures.Add(new PceProcedure
        {
            CptCode = cptCode,
            Description = description,
            Quantity = quantity,
            Modifiers = modifiers,
            ProviderId = providerId,
            ProviderName = providerName,
            RecordedDateTime = DateTime.UtcNow
        });

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddProviderAsync(
        string providerId,
        string providerName,
        string role,
        bool isPrimary)
    {
        // Avoid duplicate providers
        if (_state.State.Providers.Any(p => p.ProviderId == providerId))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
            return;
        }

        if (isPrimary)
        {
            foreach (var p in _state.State.Providers)
                p.IsPrimary = false;

            _state.State.PrimaryProviderId = providerId;
            _state.State.PrimaryProviderName = providerName;
        }

        _state.State.Providers.Add(new PceEncounterProvider
        {
            ProviderId = providerId,
            ProviderName = providerName,
            Role = role,
            IsPrimary = isPrimary
        });

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task LinkNoteAsync(string noteId)
    {
        if (!_state.State.LinkedNoteIds.Contains(noteId))
            _state.State.LinkedNoteIds.Add(noteId);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelVisitAsync(string? reason)
    {
        _state.State.Status = "CANCELLED";
        _state.State.CancellationReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── GAP 7: Billing Treatment Factors ─────────────────────────────────────

    public async Task SetTreatmentFactorsAsync(
        bool serviceConnected,
        bool agentOrange,
        bool ionizingRadiation,
        bool southwestAsia,
        bool mst,
        bool headNeckCancer,
        bool combatVeteran,
        bool shad,
        bool campLejeune)
    {
        _state.State.TfServiceConnected = serviceConnected;
        _state.State.TfAgentOrange = agentOrange;
        _state.State.TfIonizingRadiation = ionizingRadiation;
        _state.State.TfSouthwestAsia = southwestAsia;
        _state.State.TfMst = mst;
        _state.State.TfHeadNeckCancer = headNeckCancer;
        _state.State.TfCombatVeteran = combatVeteran;
        _state.State.TfShad = shad;
        _state.State.TfCampLejeune = campLejeune;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── GAP 9: Additional PCE Items ───────────────────────────────────────────

    public async Task AddHealthFactorAsync(
        string healthFactorName,
        string? healthFactorId,
        string? level,
        string? providerId,
        string? providerName)
    {
        _state.State.HealthFactors.Add(new PceHealthFactor
        {
            HealthFactorName = healthFactorName,
            HealthFactorId = healthFactorId,
            Level = level,
            ProviderId = providerId,
            ProviderName = providerName,
            RecordedDateTime = DateTime.UtcNow
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddImmunizationAsync(
        string immunizationName,
        string? immunizationId,
        string? series,
        string? lotNumber,
        string? route,
        string? site,
        bool isContraindicated,
        string? providerId,
        string? providerName)
    {
        _state.State.Immunizations.Add(new PceImmunization
        {
            ImmunizationName = immunizationName,
            ImmunizationId = immunizationId,
            Series = series,
            LotNumber = lotNumber,
            Route = route,
            Site = site,
            IsContraindicated = isContraindicated,
            ProviderId = providerId,
            ProviderName = providerName,
            RecordedDateTime = DateTime.UtcNow
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddExamAsync(
        string examName,
        string? examId,
        string? result,
        string? providerId,
        string? providerName)
    {
        _state.State.Exams.Add(new PceExam
        {
            ExamName = examName,
            ExamId = examId,
            Result = result,
            ProviderId = providerId,
            ProviderName = providerName,
            RecordedDateTime = DateTime.UtcNow
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddPatientEducationAsync(
        string topicName,
        string? topicId,
        string? levelOfUnderstanding,
        string? providerId,
        string? providerName)
    {
        _state.State.PatientEducation.Add(new PcePatientEducation
        {
            TopicName = topicName,
            TopicId = topicId,
            LevelOfUnderstanding = levelOfUnderstanding,
            ProviderId = providerId,
            ProviderName = providerName,
            RecordedDateTime = DateTime.UtcNow
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
