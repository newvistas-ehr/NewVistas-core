// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PrenatalVisitGrain : Grain, IPrenatalVisitGrain
{
    private readonly IPersistentState<PrenatalVisitState> _state;

    public PrenatalVisitGrain(
        [PersistentState("prenatalVisitState", "prenatalVisitStore")]
        IPersistentState<PrenatalVisitState> state)
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

    public Task<PrenatalVisitState> GetAsync() => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string pregnancyId,
        string patientId,
        DateTime visitDate,
        int gestationalAgeWeeks,
        int gestationalAgeDays,
        decimal? weight,
        int? bloodPressureSystolic,
        int? bloodPressureDiastolic,
        decimal? fundalHeightCm,
        int? fetalHeartRate,
        FetalPresentation fetalPresentation,
        bool? fetalMovement,
        string? urineProtein,
        string? urineGlucose,
        string? edema,
        decimal? cervicalDilationCm,
        int? cervicalEffacementPercent,
        int? fetalStation,
        string? providerId,
        string? providerName,
        string? notes,
        DateTime? nextVisitDate)
    {
        _state.State.PregnancyId = pregnancyId;
        _state.State.PatientId = patientId;
        _state.State.VisitDate = visitDate;
        _state.State.GestationalAgeWeeks = gestationalAgeWeeks;
        _state.State.GestationalAgeDays = gestationalAgeDays;
        _state.State.Weight = weight;
        _state.State.BloodPressureSystolic = bloodPressureSystolic;
        _state.State.BloodPressureDiastolic = bloodPressureDiastolic;
        _state.State.FundalHeightCm = fundalHeightCm;
        _state.State.FetalHeartRate = fetalHeartRate;
        _state.State.FetalPresentation = fetalPresentation;
        _state.State.FetalMovement = fetalMovement;
        _state.State.UrineProtein = urineProtein;
        _state.State.UrineGlucose = urineGlucose;
        _state.State.Edema = edema;
        _state.State.CervicalDilationCm = cervicalDilationCm;
        _state.State.CervicalEffacementPercent = cervicalEffacementPercent;
        _state.State.FetalStation = fetalStation;
        _state.State.ProviderId = providerId;
        _state.State.ProviderName = providerName;
        _state.State.Notes = notes;
        _state.State.NextVisitDate = nextVisitDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateNotesAsync(string? notes)
    {
        _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
