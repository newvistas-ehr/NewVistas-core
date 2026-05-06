// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Stores detailed lab results for a 6-month time partition.
/// A typical 6-month batch for an active patient has 20-50 results.
/// At ~500 bytes per result that's ~25KB — trivial grain state size.
///
/// Grain Key: PatientLabBatch/{patientIcn}/{year}-{half}
/// </summary>
public class PatientLabBatchGrain : Grain, IPatientLabBatch
{
    private readonly IPersistentState<PatientLabBatchState> _state;

    public PatientLabBatchGrain(
        [PersistentState("patientLabBatchState", "labBatchStore")]
        IPersistentState<PatientLabBatchState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientIcn))
        {
            // Parse key: PatientLabBatch/{patientIcn}/{year}-{half}
            var key = this.GetPrimaryKeyString();
            var parts = key.Split('/');
            if (parts.Length >= 3)
            {
                _state.State.PatientIcn = parts[1];
                var timeParts = parts[2].Split('-');
                if (timeParts.Length == 2)
                {
                    _state.State.Year = int.TryParse(timeParts[0], out var year) ? year : 0;
                    _state.State.Half = timeParts[1] == "H2" ? LabHalfYear.H2 : LabHalfYear.H1;
                }
            }
        }

        return base.OnActivateAsync(cancellationToken);
    }

    public async Task AddResult(LabResultDetail result)
    {
        // Insert in date-descending order
        var index = _state.State.Results
            .FindIndex(r => r.ResultDate <= result.ResultDate);

        if (index < 0)
            _state.State.Results.Add(result);
        else
            _state.State.Results.Insert(index, result);

        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<LabResultDetail>> GetAllResults()
    {
        return Task.FromResult<IReadOnlyList<LabResultDetail>>(_state.State.Results);
    }

    public Task<IReadOnlyList<LabResultDetail>> GetResultsByType(string loincCode)
    {
        var filtered = _state.State.Results
            .Where(r => r.LoincCode == loincCode)
            .ToList();
        return Task.FromResult<IReadOnlyList<LabResultDetail>>(filtered);
    }

    public Task<IReadOnlyList<LabResultDetail>> GetResultsByDateRange(
        DateTimeOffset from, DateTimeOffset to)
    {
        var filtered = _state.State.Results
            .Where(r => r.ResultDate >= from && r.ResultDate <= to)
            .ToList();
        return Task.FromResult<IReadOnlyList<LabResultDetail>>(filtered);
    }

    public Task<IReadOnlyList<LabResultDetail>> GetResultsByPanel(string panelName)
    {
        var filtered = _state.State.Results
            .Where(r => string.Equals(r.PanelName, panelName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult<IReadOnlyList<LabResultDetail>>(filtered);
    }
}
