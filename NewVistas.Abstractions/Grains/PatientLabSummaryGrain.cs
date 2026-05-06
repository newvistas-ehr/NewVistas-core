// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Answers "what are the patient's current labs?" with a single grain read.
/// Push-updated on the write path. For a typical patient, CurrentResults holds
/// 50-200 distinct test types at ~200 bytes each — well under 40KB.
/// </summary>
public class PatientLabSummaryGrain : Grain, IPatientLabSummary
{
    private readonly IPersistentState<PatientLabSummaryState> _state;

    public PatientLabSummaryGrain(
        [PersistentState("patientLabSummaryState", "labSummaryStore")]
        IPersistentState<PatientLabSummaryState> state)
    {
        _state = state;
    }

    public async Task RecordNewResult(LabResultEvent labResult)
    {
        var code = labResult.LoincCode;

        if (_state.State.CurrentResults.TryGetValue(code, out var existing))
        {
            // Only replace if the new result is actually newer (handles out-of-order arrival)
            if (labResult.ResultDate <= existing.ResultDate)
                return;

            // Shift trend: prepend new value, cap at 3
            existing.RecentTrend.Insert(0, new LabTrendPoint
            {
                Value = labResult.Value,
                ResultDate = labResult.ResultDate,
                AbnormalFlag = labResult.AbnormalFlag
            });
            if (existing.RecentTrend.Count > 3)
                existing.RecentTrend.RemoveAt(existing.RecentTrend.Count - 1);

            // Update current values
            existing.Value = labResult.Value;
            existing.Units = labResult.Units;
            existing.ReferenceRange = labResult.ReferenceRange;
            existing.AbnormalFlag = labResult.AbnormalFlag;
            existing.ResultDate = labResult.ResultDate;
            existing.FacilityCode = labResult.FacilityCode;
        }
        else
        {
            // First result for this test type
            _state.State.CurrentResults[code] = new LabTestSummaryEntry
            {
                LoincCode = code,
                TestName = labResult.TestName,
                Value = labResult.Value,
                Units = labResult.Units,
                ReferenceRange = labResult.ReferenceRange,
                AbnormalFlag = labResult.AbnormalFlag,
                ResultDate = labResult.ResultDate,
                FacilityCode = labResult.FacilityCode,
                RecentTrend =
                [
                    new LabTrendPoint
                    {
                        Value = labResult.Value,
                        ResultDate = labResult.ResultDate,
                        AbnormalFlag = labResult.AbnormalFlag
                    }
                ]
            };
        }

        _state.State.LastUpdated = DateTimeOffset.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyDictionary<string, LabTestSummaryEntry>> GetCurrentLabs()
    {
        return Task.FromResult<IReadOnlyDictionary<string, LabTestSummaryEntry>>(
            _state.State.CurrentResults);
    }

    public Task<LabTestSummaryEntry?> GetCurrentResult(string loincCode)
    {
        _state.State.CurrentResults.TryGetValue(loincCode, out var entry);
        return Task.FromResult(entry);
    }

    public Task<IReadOnlyList<LabTestSummaryEntry>> GetAbnormalResults()
    {
        var abnormal = _state.State.CurrentResults.Values
            .Where(e => e.AbnormalFlag != LabAbnormalFlag.Normal)
            .ToList();
        return Task.FromResult<IReadOnlyList<LabTestSummaryEntry>>(abnormal);
    }
}
