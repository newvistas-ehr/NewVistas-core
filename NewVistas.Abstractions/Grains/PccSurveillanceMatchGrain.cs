// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PccSurveillanceMatchGrain : Grain, IPccSurveillanceMatchGrain
{
    private readonly IPersistentState<PccSurveillanceMatchState> _state;

    public PccSurveillanceMatchGrain(
        [PersistentState("pccSurvMatchState", "pccSurvMatchStore")]
        IPersistentState<PccSurveillanceMatchState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.MatchId))
        {
            _state.State.MatchId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<PccSurveillanceMatchState> GetAsync() => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string patientId, string? patientName,
        string configId, string conditionName,
        PccEncounterClassification classification,
        DateTime encounterDate, PccVisitType visitType,
        string? chiefComplaint, string? facilityName,
        DateTime? dischargeDate, string? providerName,
        List<string>? matchingDiagnoses,
        List<string>? matchingProcedures,
        List<string>? matchingLabResults,
        List<string>? matchingMedications,
        PccComorbidityFlags? comorbidities,
        PccEncounterVitals? vitals)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.ConfigId = configId;
        _state.State.ConditionName = conditionName;
        _state.State.Status = PccSurveillanceMatchStatus.Detected;
        _state.State.Classification = classification;
        _state.State.EncounterDate = encounterDate;
        _state.State.VisitType = visitType;
        _state.State.ChiefComplaint = chiefComplaint;
        _state.State.FacilityName = facilityName;
        _state.State.DischargeDate = dischargeDate;
        _state.State.ProviderName = providerName;
        _state.State.MatchingDiagnoses = matchingDiagnoses ?? new();
        _state.State.MatchingProcedures = matchingProcedures ?? new();
        _state.State.MatchingLabResults = matchingLabResults ?? new();
        _state.State.MatchingMedications = matchingMedications ?? new();
        _state.State.Comorbidities = comorbidities;
        _state.State.Vitals = vitals;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(PccSurveillanceMatchStatus status)
    {
        _state.State.Status = status;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkExportedAsync(string exportReference)
    {
        _state.State.Status = PccSurveillanceMatchStatus.Exported;
        _state.State.ExportedDate = DateTime.UtcNow;
        _state.State.ExportReference = exportReference;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
