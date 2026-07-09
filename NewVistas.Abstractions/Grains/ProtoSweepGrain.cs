// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Screening-sweep coordinator (grain key <c>PROTO-SWEEP</c>). Explicit EPI action — walks the
/// active patient population (paged) or an explicit list, invokes the per-patient screening worker,
/// and records each result on the proto grain. Keeps a bounded run history.
/// </summary>
public class ProtoSweepGrain : Grain, IProtoSweepGrain
{
    private const int MaxRunHistory = 50;
    private readonly IPersistentState<ProtoSweepState> _state;

    public ProtoSweepGrain(
        [PersistentState("protoSweepState", "protoSweepStore")]
        IPersistentState<ProtoSweepState> state)
    {
        _state = state;
    }

    public async Task<ProtoSweepRun> SweepProtoAsync(string protoConditionId, int? maxPatients, string runBy)
    {
        List<string> patientIds = await GrainFactory
            .GetGrain<IPatientIndexGrain>("PATIENT-INDEX")
            .GetAllPatientIdsAsync(maxPatients);
        return await RunAsync(protoConditionId, patientIds, runBy, targeted: false);
    }

    public Task<ProtoSweepRun> SweepPatientsAsync(string protoConditionId, List<string> patientIds, string runBy) =>
        RunAsync(protoConditionId, patientIds ?? new(), runBy, targeted: true);

    public Task<List<ProtoSweepRun>> GetRecentRunsAsync() =>
        Task.FromResult(_state.State.Runs.OrderByDescending(r => r.StartedAt).ToList());

    private async Task<ProtoSweepRun> RunAsync(string protoConditionId, List<string> patientIds, string runBy, bool targeted)
    {
        var run = new ProtoSweepRun
        {
            ProtoConditionId = protoConditionId,
            StartedAt = DateTime.UtcNow,
            TargetedMode = targeted,
            RunBy = runBy,
            PatientsScreened = patientIds.Count
        };

        int matched = 0;
        foreach (string patientId in patientIds)
        {
            try
            {
                ProtoMatchResult result = await GrainFactory
                    .GetGrain<IProtoConditionScreeningGrain>($"PROTO-SCREEN:{patientId}")
                    .EvaluateAndRecordAsync(protoConditionId);
                if (result.Matches) matched++;
            }
            catch
            {
                // A single patient's read/eval failure must not abort the sweep.
            }
        }
        run.MatchedCount = matched;

        _state.State.Runs.Add(run);
        if (_state.State.Runs.Count > MaxRunHistory)
            _state.State.Runs.RemoveRange(0, _state.State.Runs.Count - MaxRunHistory);
        await _state.WriteStateAsync();

        return run;
    }
}
