// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Code-set migration sweep. See <see cref="IDxStewardshipSweepGrain"/> for the contract; the
/// per-patient semantics (idempotency, supersession, episode outcome pinned to Recoded) live
/// in <c>PatientWorkflowGrain.RecodeProblemCodeAsync</c> so a single patient can be fixed the
/// same way a population is.
/// </summary>
public class DxStewardshipSweepGrain : Grain, IDxStewardshipSweepGrain
{
    public const string Key = "DX-STEWARDSHIP-SWEEP";
    private const int MaxRunHistory = 50;

    private readonly IPersistentState<DxStewardshipSweepState> _state;

    public DxStewardshipSweepGrain(
        [PersistentState("dxStewardshipSweepState", "dxStewardshipSweepStore")]
        IPersistentState<DxStewardshipSweepState> state)
    {
        _state = state;
    }

    public async Task<BulkRecodeRun> BulkRecodeAsync(BulkRecodeCommand command)
    {
        await ValidateAsync(command);
        List<string> patientIds = await GrainFactory
            .GetGrain<IPatientIndexGrain>("PATIENT-INDEX")
            .GetAllPatientIdsAsync(command.MaxPatients);
        return await RunAsync(command, patientIds, targeted: false);
    }

    public async Task<BulkRecodeRun> BulkRecodePatientsAsync(BulkRecodeCommand command, List<string> patientIds)
    {
        await ValidateAsync(command);
        return await RunAsync(command, patientIds ?? new(), targeted: true);
    }

    public Task<List<BulkRecodeRun>> GetRecentRunsAsync() =>
        Task.FromResult(_state.State.Runs.OrderByDescending(r => r.StartedAt).ToList());

    private async Task ValidateAsync(BulkRecodeCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.FromCode) || string.IsNullOrWhiteSpace(command.ToCode))
            throw new ArgumentException("Both the retired code and the replacement code are required.");
        if (DiagnosisCodeRelation.Normalize(command.FromCode) == DiagnosisCodeRelation.Normalize(command.ToCode))
            throw new ArgumentException("The retired and replacement codes are the same code.");
        if (string.IsNullOrWhiteSpace(command.ToDisplay))
            throw new ArgumentException("The replacement code's display text is required.");
        if (string.IsNullOrWhiteSpace(command.Narrative))
            throw new ArgumentException(
                "A narrative is required — every touched chart cites why the code set changed.");

        // When the site's index is loaded, an unknown replacement code is a typo, and a typo
        // applied at population scale is not a recoverable mistake.
        IIcd10IndexGrain index = GrainFactory.GetGrain<IIcd10IndexGrain>("ICD10-INDEX");
        Icd10IndexStatus status = await index.GetStatusAsync();
        if (status.IsLoaded && await index.GetCodeAsync(command.ToCode) is null)
            throw new ArgumentException(
                $"Replacement code '{command.ToCode}' is not in the loaded ICD-10 index.");
    }

    private async Task<BulkRecodeRun> RunAsync(BulkRecodeCommand command, List<string> patientIds, bool targeted)
    {
        var run = new BulkRecodeRun
        {
            FromCode = command.FromCode,
            ToCode = command.ToCode,
            StartedAt = DateTime.UtcNow,
            RunBy = command.RunBy,
            TargetedMode = targeted,
            PatientsScreened = patientIds.Count,
        };

        foreach (string patientId in patientIds.Distinct())
        {
            try
            {
                ProblemRecodeResult result = await GrainFactory
                    .GetGrain<IPatientWorkflowGrain>(patientId)
                    .RecodeProblemCodeAsync(command);
                switch (result.Outcome)
                {
                    case ProblemRecodeOutcome.Recoded:
                        run.RecodedCount++;
                        run.EpisodesClosed += result.EpisodesClosed;
                        break;
                    case ProblemRecodeOutcome.AlreadyCoded: run.AlreadyCodedCount++; break;
                    default: run.NoMatchCount++; break;
                }
            }
            catch
            {
                // One patient's failure must not abort a population migration; the count
                // surfaces it so the run report is honest about partial coverage.
                run.FailureCount++;
            }
        }

        _state.State.Runs.Add(run);
        if (_state.State.Runs.Count > MaxRunHistory)
            _state.State.Runs.RemoveRange(0, _state.State.Runs.Count - MaxRunHistory);
        await _state.WriteStateAsync();
        return run;
    }
}
