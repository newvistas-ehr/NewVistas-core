// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class IBillingActionGrain : Grain, IIBillingActionGrain
{
    private readonly IPersistentState<IBillingActionState> _state;

    public IBillingActionGrain(
        [PersistentState("ibBillingActionState", "ibBillingActionStore")]
        IPersistentState<IBillingActionState> state)
    {
        _state = state;
    }

    public Task<IBillingActionState> GetAsync() => Task.FromResult(_state.State);

    public async Task<string> CreateAsync(
        string patientId,
        string actionTypeCode,
        string actionTypeDescription,
        IBActionCategory actionCategory,
        decimal? chargeAmount,
        DateTime serviceDate,
        string enteredByUserId,
        string enteredByUserName,
        string? encounterId,
        string? diagnosisCode,
        string? procedureCode,
        string? locationId,
        string? orderId,
        string? prescriptionId,
        string? notes)
    {
        string actionId = this.GetPrimaryKeyString()
            .Replace("IB-ACTION:", string.Empty);

        _state.State.BillingActionId    = actionId;
        _state.State.PatientId          = patientId;
        _state.State.ActionTypeCode     = actionTypeCode;
        _state.State.ActionTypeDescription = actionTypeDescription;
        _state.State.ActionCategory     = actionCategory;
        _state.State.ChargeAmount       = chargeAmount;
        _state.State.ServiceDate        = serviceDate;
        _state.State.DateEntered        = DateTime.UtcNow;
        _state.State.EnteredByUserId    = enteredByUserId;
        _state.State.EnteredByUserName  = enteredByUserName;
        _state.State.EncounterId        = encounterId;
        _state.State.DiagnosisCode      = diagnosisCode;
        _state.State.ProcedureCode      = procedureCode;
        _state.State.LocationId         = locationId;
        _state.State.OrderId            = orderId;
        _state.State.PrescriptionId     = prescriptionId;
        _state.State.Notes              = notes;
        _state.State.Status             = IBillingActionStatus.Incomplete;
        _state.State.IsExempt           = false;
        _state.State.CreatedDate        = DateTime.UtcNow;
        _state.State.LastModifiedDate   = DateTime.UtcNow;

        await _state.WriteStateAsync();
        return actionId;
    }

    public async Task CancelAsync(
        string removeReasonCode,
        string removeReasonDescription,
        string removedByUserId)
    {
        _state.State.Status                  = IBillingActionStatus.Cancelled;
        _state.State.RemoveReasonCode        = removeReasonCode;
        _state.State.RemoveReasonDescription = removeReasonDescription;
        _state.State.RemoveDateTime          = DateTime.UtcNow;
        _state.State.RemovedByUserId         = removedByUserId;
        _state.State.LastModifiedDate        = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(IBillingActionStatus newStatus)
    {
        _state.State.Status           = newStatus;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetExemptAsync(string exemptionReason)
    {
        _state.State.IsExempt         = true;
        _state.State.ExemptionReason  = exemptionReason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
