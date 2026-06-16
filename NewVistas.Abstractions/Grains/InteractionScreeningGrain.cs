// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Grain implementation for a single drug interaction screening.
/// Records all interaction findings and supports pharmacist override of blocking interactions.
///
/// VistA reference: DRGINT.m integration in PSO fill workflow.
/// Key format: IXSCREEN:{prescriptionId}
/// </summary>
public class InteractionScreeningGrain : Grain, IInteractionScreeningGrain
{
    private readonly IPersistentState<InteractionScreeningState> _state;

    public InteractionScreeningGrain(
        [PersistentState("interactionScreeningState", "interactionScreeningStore")]
        IPersistentState<InteractionScreeningState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ScreeningId))
        {
            _state.State.ScreeningId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task ScreenAsync(
        string prescriptionId,
        string patientId,
        string drugName,
        string? screenedBy,
        List<InteractionFinding> findings)
    {
        _state.State.PrescriptionId = prescriptionId;
        _state.State.PatientId = patientId;
        _state.State.DrugName = drugName;
        _state.State.ScreenedBy = screenedBy;
        _state.State.ScreenedDate = DateTime.UtcNow;
        _state.State.Findings = findings;

        _state.State.TotalInteractionCount = findings.Count;
        _state.State.BlockingCount = findings.Count(f => f.IsBlocking);
        _state.State.OverriddenCount = 0;

        if (_state.State.BlockingCount > 0)
        {
            _state.State.Status = InteractionScreeningStatus.BlockedPendingOverride;
        }
        else
        {
            _state.State.Status = InteractionScreeningStatus.Cleared;
        }

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<InteractionScreeningState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task OverrideInteractionAsync(int findingIndex, string pharmacistId, string reason)
    {
        if (findingIndex < 0 || findingIndex >= _state.State.Findings.Count)
            return;

        InteractionFinding finding = _state.State.Findings[findingIndex];
        if (!finding.IsBlocking || finding.IsOverridden)
            return;

        finding.IsOverridden = true;
        finding.OverriddenBy = pharmacistId;
        finding.OverrideReason = reason;
        finding.OverrideDate = DateTime.UtcNow;

        _state.State.OverriddenCount = _state.State.Findings.Count(f => f.IsOverridden);

        // If all blocking interactions are now overridden, transition status
        int remainingBlocking = _state.State.Findings.Count(f => f.IsBlocking && !f.IsOverridden);
        if (remainingBlocking == 0)
        {
            _state.State.Status = InteractionScreeningStatus.OverriddenByPharmacist;
        }

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddNotesAsync(string notes)
    {
        _state.State.Notes = string.IsNullOrEmpty(_state.State.Notes)
            ? notes
            : $"{_state.State.Notes}\n{notes}";

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
