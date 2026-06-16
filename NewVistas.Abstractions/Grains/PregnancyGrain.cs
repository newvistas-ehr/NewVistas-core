// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PregnancyGrain : Grain, IPregnancyGrain
{
    private readonly IPersistentState<PregnancyState> _state;

    public PregnancyGrain(
        [PersistentState("pregnancyState", "pregnancyStore")]
        IPersistentState<PregnancyState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PregnancyId))
        {
            _state.State.PregnancyId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<PregnancyState> GetAsync() => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string patientId,
        DateTime? lastMenstrualPeriod,
        DateTime? eddByLmp,
        DateTime? eddByUltrasound,
        DateTime definitiveEdd,
        int gravida,
        int para,
        int abortions,
        int living,
        PregnancyRiskLevel riskLevel,
        List<string>? riskFactors,
        string? providerId,
        string? providerName,
        string? locationId,
        string? locationName,
        string? notes)
    {
        _state.State.PatientId = patientId;
        _state.State.Status = PregnancyStatus.Active;
        _state.State.LastMenstrualPeriod = lastMenstrualPeriod;
        _state.State.EddByLmp = eddByLmp;
        _state.State.EddByUltrasound = eddByUltrasound;
        _state.State.DefinitiveEdd = definitiveEdd;
        _state.State.Gravida = gravida;
        _state.State.Para = para;
        _state.State.Abortions = abortions;
        _state.State.Living = living;
        _state.State.RiskLevel = riskLevel;
        _state.State.RiskFactors = riskFactors ?? new();
        _state.State.ProviderId = providerId;
        _state.State.ProviderName = providerName;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.Notes = notes;
        _state.State.Outcome = PregnancyOutcome.Ongoing;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateRiskAsync(PregnancyRiskLevel riskLevel, List<string> riskFactors)
    {
        _state.State.RiskLevel = riskLevel;
        _state.State.RiskFactors = riskFactors;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddProblemAsync(PrenatalProblemEntry problem)
    {
        if (!_state.State.Problems.Any(p => p.ProblemId == problem.ProblemId))
            _state.State.Problems.Add(problem);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ResolveProblemAsync(string problemId)
    {
        PrenatalProblemEntry? problem = _state.State.Problems.FirstOrDefault(p => p.ProblemId == problemId);
        if (problem != null)
            problem.IsActive = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordDeliveryAsync(DeliveryInfo delivery, PregnancyOutcome outcome)
    {
        _state.State.Delivery = delivery;
        _state.State.Outcome = outcome;
        _state.State.Status = PregnancyStatus.Delivered;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordPostpartumAsync(PostpartumInfo postpartum)
    {
        _state.State.Postpartum = postpartum;
        _state.State.Status = PregnancyStatus.Postpartum;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(PregnancyStatus status)
    {
        _state.State.Status = status;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateEddAsync(DateTime? eddByUltrasound, DateTime definitiveEdd)
    {
        _state.State.EddByUltrasound = eddByUltrasound;
        _state.State.DefinitiveEdd = definitiveEdd;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
