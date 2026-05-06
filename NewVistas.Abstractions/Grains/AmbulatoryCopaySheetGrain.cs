// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class AmbulatoryCopaySheetGrain : Grain, IAmbulatoryCopaySheetGrain
{
    private readonly IPersistentState<AmbulatoryCopaySheetState> _state;

    public AmbulatoryCopaySheetGrain(
        [PersistentState("ambulatoryCopaySheetState", "ambulatoryCopaySheetStore")]
        IPersistentState<AmbulatoryCopaySheetState> state)
    {
        _state = state;
    }

    public Task<AmbulatoryCopaySheetState> GetAsync() => Task.FromResult(_state.State);

    public async Task<string> CreateAsync(
        string patientId,
        string? encounterId,
        DateTime visitDate,
        string? clinicId,
        string? clinicName)
    {
        string sheetId = this.GetPrimaryKeyString().Replace("IB-SHEET:", string.Empty);

        _state.State.SheetId       = sheetId;
        _state.State.PatientId     = patientId;
        _state.State.EncounterId   = encounterId;
        _state.State.VisitDate     = visitDate;
        _state.State.ClinicId      = clinicId;
        _state.State.ClinicName    = clinicName;
        _state.State.IsComplete    = false;
        _state.State.CreatedDate   = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
        return sheetId;
    }

    public async Task CheckItemAsync(
        string itemCode,
        string itemDescription,
        string checkedByUserId,
        string checkedByUserName)
    {
        List<CopayChecklistItem> items = _state.State.ChecklistItems;
        int idx = items.FindIndex(i => i.ItemCode == itemCode);

        CopayChecklistItem checked_item = new()
        {
            ItemCode          = itemCode,
            ItemDescription   = itemDescription,
            IsChecked         = true,
            CheckedByUserId   = checkedByUserId,
            CheckedByUserName = checkedByUserName,
            CheckedDateTime   = DateTime.UtcNow,
        };

        if (idx >= 0)
            items[idx] = checked_item;
        else
            items.Add(checked_item);

        _state.State.TotalBillableItems = items.Count(i => i.IsChecked);
        _state.State.LastModifiedDate   = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteAsync(string completedByUserId, string completedByUserName)
    {
        _state.State.IsComplete          = true;
        _state.State.CompletedByUserId   = completedByUserId;
        _state.State.CompletedByUserName = completedByUserName;
        _state.State.CompletedDateTime   = DateTime.UtcNow;
        _state.State.LastModifiedDate    = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task LinkBillingActionAsync(string billingActionId)
    {
        if (!_state.State.BillingActionIds.Contains(billingActionId))
        {
            _state.State.BillingActionIds.Add(billingActionId);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }
}
