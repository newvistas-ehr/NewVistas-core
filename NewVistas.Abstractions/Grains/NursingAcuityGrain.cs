// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Patient classification (acuity) grain — VistA File #212 (NURSING PATIENT).
/// Records per-shift acuity scores and maintains a full classification history.
/// </summary>
public class NursingAcuityGrain : Grain, INursingAcuityGrain
{
    private readonly IPersistentState<NursingAcuityState> _state;

    public NursingAcuityGrain(
        [PersistentState("nursingAcuityState", "nursingAcuityStore")]
        IPersistentState<NursingAcuityState> state)
    {
        _state = state;
    }

    public Task<NursingAcuityState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task RecordAcuityAsync(
        AcuityLevel level,
        int? score,
        string nurseId,
        string nurseName,
        string? shift,
        string? notes)
    {
        _state.State.CurrentAcuityLevel     = level;
        _state.State.CurrentAcuityScore     = score;
        _state.State.CurrentAcuityDateTime  = DateTime.UtcNow;
        _state.State.CurrentAcuityNurseId   = nurseId;
        _state.State.CurrentAcuityNurseName = nurseName;
        _state.State.CurrentShift           = shift;

        _state.State.AcuityHistory.Add(new AcuityRecord
        {
            RecordedDateTime = DateTime.UtcNow,
            Level            = level,
            Score            = score,
            NurseId          = nurseId,
            NurseName        = nurseName,
            Shift            = shift,
            Notes            = notes
        });

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
