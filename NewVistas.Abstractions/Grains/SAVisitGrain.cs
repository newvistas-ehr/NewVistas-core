// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class SAVisitGrain : Grain, ISAVisitGrain
{
    private readonly IPersistentState<SAVisitState> _state;

    public SAVisitGrain(
        [PersistentState("saVisitState", "saVisitStore")]
        IPersistentState<SAVisitState> state)
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

    public Task<SAVisitState> GetAsync() => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string episodeId,
        string patientId,
        DateTime visitDate,
        SAVisitType visitType,
        int? durationMinutes,
        string? udsResult,
        List<string>? udsSubstancesDetected,
        int? daysSinceLastUse,
        int? cravingLevel,
        string? providerId, string? providerName,
        string? notes)
    {
        _state.State.EpisodeId = episodeId;
        _state.State.PatientId = patientId;
        _state.State.VisitDate = visitDate;
        _state.State.VisitType = visitType;
        _state.State.DurationMinutes = durationMinutes;
        _state.State.UdsResult = udsResult;
        _state.State.UdsSubstancesDetected = udsSubstancesDetected ?? new();
        _state.State.DaysSinceLastUse = daysSinceLastUse;
        _state.State.CravingLevel = cravingLevel;
        _state.State.ProviderId = providerId;
        _state.State.ProviderName = providerName;
        _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
