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
/// Per-patient log of drug safety advisories received. Keyed by patient id.
/// The authoritative record of what each patient was told and when.
/// </summary>
public class PatientSafetyAdvisoryGrain : Grain, IPatientSafetyAdvisoryGrain
{
    private readonly IPersistentState<PatientSafetyAdvisoryState> _state;

    public PatientSafetyAdvisoryGrain(
        [PersistentState("patientSafetyAdvisoryState", "patientSafetyAdvisoryStore")]
        IPersistentState<PatientSafetyAdvisoryState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
            _state.State.PatientId = this.GetPrimaryKeyString();

        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<string> RecordReceiptAsync(
        string advisoryId,
        string advisoryTitle,
        string messageSent,
        string providerId,
        string providerName,
        AdvisoryChannel channel)
    {
        // Idempotent per advisory: a patient receives a given advisory at most once.
        PatientAdvisoryReceipt? existing = _state.State.Receipts
            .FirstOrDefault(r => r.AdvisoryId == advisoryId);
        if (existing is not null)
            return existing.ReceiptId;

        PatientAdvisoryReceipt receipt = new()
        {
            ReceiptId = $"PADV-{Guid.NewGuid()}",
            AdvisoryId = advisoryId,
            AdvisoryTitle = advisoryTitle,
            MessageSent = messageSent,
            SentByProviderId = providerId,
            SentByProviderName = providerName,
            Channel = channel,
            SentDate = DateTime.UtcNow,
            Status = AdvisoryReceiptStatus.Sent,
        };

        _state.State.Receipts.Add(receipt);
        await _state.WriteStateAsync();
        return receipt.ReceiptId;
    }

    public Task<List<PatientAdvisoryReceipt>> GetReceiptsAsync() =>
        Task.FromResult(_state.State.Receipts);

    public Task<bool> HasReceivedAsync(string advisoryId) =>
        Task.FromResult(_state.State.Receipts.Any(r => r.AdvisoryId == advisoryId));

    public async Task AcknowledgeAsync(string advisoryId, DateTime acknowledgedDate)
    {
        PatientAdvisoryReceipt? receipt = _state.State.Receipts
            .FirstOrDefault(r => r.AdvisoryId == advisoryId);
        if (receipt is null)
            return;

        receipt.Status = AdvisoryReceiptStatus.Acknowledged;
        receipt.AcknowledgedDate = acknowledgedDate;
        await _state.WriteStateAsync();
    }
}
