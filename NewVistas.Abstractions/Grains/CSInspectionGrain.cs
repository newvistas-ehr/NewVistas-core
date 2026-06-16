// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class CSInspectionGrain : Grain, ICSInspectionGrain
{
    private readonly IPersistentState<CSInspectionState> _state;

    public CSInspectionGrain(
        [PersistentState("csInspectionState", "csInspectionStore")] IPersistentState<CSInspectionState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.InspectionId))
            _state.State.InspectionId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<CSInspectionState> GetInspectionAsync() => Task.FromResult(_state.State);

    public async Task CreateInspectionAsync(
        string locationId,
        string locationName,
        CSInspectionType inspectionType,
        DateTime inspectionDateTime,
        string inspectorId,
        string inspectorName,
        string witnessId,
        string witnessName,
        string? secondWitnessId,
        string? secondWitnessName,
        string? notes)
    {
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.InspectionType = inspectionType;
        _state.State.InspectionDateTime = inspectionDateTime;
        _state.State.InspectorId = inspectorId;
        _state.State.InspectorName = inspectorName;
        _state.State.WitnessId = witnessId;
        _state.State.WitnessName = witnessName;
        _state.State.SecondWitnessId = secondWitnessId;
        _state.State.SecondWitnessName = secondWitnessName;
        _state.State.Notes = notes;
        _state.State.OverallResult = CSInspectionResult.Passed;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddDrugCountAsync(CSInspectionCount count)
    {
        // Calculate discrepancy: physical minus system (negative = shortage)
        count.Discrepancy = count.PhysicalCount - count.SystemCount;
        _state.State.DrugCounts.Add(count);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task FinalizeInspectionAsync(
        CSInspectionResult result,
        bool discrepanciesReported,
        string? reportedToId,
        string? reportedToName,
        string? investigationNotes)
    {
        _state.State.OverallResult = result;
        _state.State.DiscrepanciesReported = discrepanciesReported;
        _state.State.ReportedToId = reportedToId;
        _state.State.ReportedToName = reportedToName;
        _state.State.InvestigationNotes = investigationNotes;

        if (discrepanciesReported)
            _state.State.ReportedDateTime = DateTime.UtcNow;

        // Count drugs with non-zero discrepancies
        _state.State.TotalDiscrepancies = _state.State.DrugCounts
            .Count(c => c.Discrepancy != 0);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
