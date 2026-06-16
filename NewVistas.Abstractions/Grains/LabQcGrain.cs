// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class LabQcGrain : Grain, ILabQcGrain
{
    private readonly IPersistentState<LabQcState> _state;

    public LabQcGrain(
        [PersistentState("labQcState", "labQcStore")]
        IPersistentState<LabQcState> state)
    { _state = state; }

    public Task<LabQcState> GetAsync() => Task.FromResult(_state.State);

    public async Task<LabQcResult> RecordQcRunAsync(
        string qcLevel, string lotNumber, decimal measuredValue,
        decimal expectedMean, decimal standardDeviation,
        string techId, string techName, string? notes)
    {
        // Parse grain key for instrument/LOINC
        string key = this.GetPrimaryKeyString();
        string[] parts = key.Split(':');
        if (parts.Length >= 2)
        {
            _state.State.InstrumentId = parts[0];
            _state.State.LoincCode = parts[1];
        }

        // Calculate z-score
        decimal zScore = standardDeviation > 0
            ? Math.Abs((measuredValue - expectedMean) / standardDeviation)
            : 0m;

        // Apply Westgard rules
        QcResultStatus status = QcResultStatus.Pass;
        string? westgardViolation = null;

        if (zScore >= 3m)
        {
            status = QcResultStatus.Fail;
            westgardViolation = "1-3s";
        }
        else if (zScore >= 2m)
        {
            // Check 2-2s rule: two consecutive results > 2SD in same direction
            if (_state.State.Results.Count > 0)
            {
                LabQcResult prev = _state.State.Results[0];
                decimal prevZ = prev.StandardDeviation > 0
                    ? (prev.MeasuredValue - prev.ExpectedMean) / prev.StandardDeviation : 0;
                decimal currZ = (measuredValue - expectedMean) / (standardDeviation > 0 ? standardDeviation : 1);
                if (Math.Abs(prevZ) >= 2m && Math.Sign(prevZ) == Math.Sign(currZ))
                {
                    status = QcResultStatus.Fail;
                    westgardViolation = "2-2s";
                }
                else
                {
                    status = QcResultStatus.Warning;
                    westgardViolation = "1-2s";
                }
            }
            else
            {
                status = QcResultStatus.Warning;
                westgardViolation = "1-2s";
            }
        }

        LabQcResult result = new()
        {
            QcResultId        = $"QC-{Guid.NewGuid():N}",
            QcLevel           = qcLevel,
            LotNumber         = lotNumber,
            MeasuredValue     = measuredValue,
            ExpectedMean      = expectedMean,
            StandardDeviation = standardDeviation,
            ZScore            = Math.Round(zScore, 2),
            Status            = status,
            WestgardViolation = westgardViolation,
            RunDateTime       = DateTime.UtcNow,
            TechId            = techId,
            TechName          = techName,
            Notes             = notes,
        };

        _state.State.Results.Insert(0, result);

        // Update QC status
        _state.State.IsCurrentlyPassing = status == QcResultStatus.Pass;
        if (status == QcResultStatus.Pass)
        {
            _state.State.LastPassingQcDate = DateTime.UtcNow;
            _state.State.ConsecutivePassCount++;
            _state.State.IsTestingBlocked = false;
        }
        else if (status == QcResultStatus.Fail)
        {
            _state.State.LastFailureDate = DateTime.UtcNow;
            _state.State.ConsecutivePassCount = 0;
            _state.State.IsTestingBlocked = true;
        }

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return result;
    }

    public async Task RecordCorrectiveActionAsync(string qcResultId, string correctiveAction)
    {
        int idx = _state.State.Results.FindIndex(r => r.QcResultId == qcResultId);
        if (idx >= 0)
        {
            _state.State.Results[idx] = _state.State.Results[idx] with { CorrectiveAction = correctiveAction };
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task UnblockTestingAsync()
    {
        _state.State.IsTestingBlocked = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<LabQcResult>> GetRecentResultsAsync(int count)
        => Task.FromResult(_state.State.Results.Take(count).ToList());

    public Task<bool> IsTestingAllowedAsync()
        => Task.FromResult(!_state.State.IsTestingBlocked);
}
