// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class EpcsPrescriptionGrain : Grain, IEpcsPrescriptionGrain
{
    private readonly IPersistentState<EpcsPrescriptionState> _state;

    public EpcsPrescriptionGrain(
        [PersistentState("epcsPrescriptionState", "epcsRxStore")]
        IPersistentState<EpcsPrescriptionState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.EpcsId))
        {
            _state.State.EpcsId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<EpcsPrescriptionState> GetAsync() => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string patientId,
        string? prescriptionId,
        EpcsScriptTransactionType transactionType,
        string drugName, string? ndc, string deaSchedule,
        decimal quantity, int daysSupply, int refillsAuthorized,
        string? sig, string? diagnosisCode,
        string? prescriberNpi, string? prescriberDea, string? prescriberName,
        string? prescriberCredentialId,
        EpcsPharmacyDestination? destinationPharmacy)
    {
        _state.State.PatientId = patientId;
        _state.State.PrescriptionId = prescriptionId;
        _state.State.TransactionType = transactionType;
        _state.State.Status = EpcsTransmissionStatus.Draft;
        _state.State.DrugName = drugName;
        _state.State.Ndc = ndc;
        _state.State.DeaSchedule = deaSchedule;
        _state.State.Quantity = quantity;
        _state.State.DaysSupply = daysSupply;
        _state.State.RefillsAuthorized = refillsAuthorized;
        _state.State.Sig = sig;
        _state.State.DiagnosisCode = diagnosisCode;
        _state.State.PrescriberNpi = prescriberNpi;
        _state.State.PrescriberDea = prescriberDea;
        _state.State.PrescriberName = prescriberName;
        _state.State.PrescriberCredentialId = prescriberCredentialId;
        _state.State.DestinationPharmacy = destinationPharmacy;
        _state.State.AuditTrail.Add(new EpcsAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = "CREATED",
            UserId = prescriberNpi ?? "SYSTEM",
            Details = $"E-prescription created for {drugName} (Schedule {deaSchedule})",
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SignAsync(EpcsSignatureRecord signature)
    {
        _state.State.Signature = signature;
        _state.State.Status = EpcsTransmissionStatus.Signed;
        _state.State.AuditTrail.Add(new EpcsAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = "SIGNED",
            UserId = _state.State.PrescriberNpi ?? "SYSTEM",
            Details = $"Digitally signed with 2FA ({signature.TwoFactorMethod}), cert {signature.CertificateThumbprint[..8]}...",
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkTransmittedAsync(string? transmissionMessageId)
    {
        _state.State.Status = EpcsTransmissionStatus.Transmitted;
        _state.State.TransmissionMessageId = transmissionMessageId;
        _state.State.TransmittedDate = DateTime.UtcNow;
        _state.State.AuditTrail.Add(new EpcsAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = "TRANSMITTED",
            UserId = "SYSTEM",
            Details = $"Transmitted to {_state.State.DestinationPharmacy?.PharmacyName}, msg={transmissionMessageId}",
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkAcknowledgedAsync()
    {
        _state.State.Status = EpcsTransmissionStatus.Acknowledged;
        _state.State.AcknowledgedDate = DateTime.UtcNow;
        _state.State.AuditTrail.Add(new EpcsAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = "ACKNOWLEDGED",
            UserId = "SYSTEM",
            Details = "Acknowledgment received from pharmacy",
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkErrorAsync(string errorMessage)
    {
        _state.State.Status = EpcsTransmissionStatus.Error;
        _state.State.ErrorMessage = errorMessage;
        _state.State.AuditTrail.Add(new EpcsAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = "ERROR",
            UserId = "SYSTEM",
            Details = errorMessage,
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelAsync(string userId, string? reason)
    {
        _state.State.Status = EpcsTransmissionStatus.Cancelled;
        _state.State.AuditTrail.Add(new EpcsAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = "CANCELLED",
            UserId = userId,
            Details = reason ?? "Prescription cancelled",
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddAuditEntryAsync(string action, string userId, string? details)
    {
        _state.State.AuditTrail.Add(new EpcsAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = action,
            UserId = userId,
            Details = details,
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
