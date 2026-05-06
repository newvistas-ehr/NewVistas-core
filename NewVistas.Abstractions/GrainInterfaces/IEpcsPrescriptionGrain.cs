// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// EPCS E-Prescription Grain — 21 CFR Part 1311.
/// Key: "EPCS-RX:{guid}"
///
/// Models a single DEA-compliant e-prescription for controlled substances
/// with digital signature, 2FA verification, and audit trail.
/// Additive feature grain per Site Flavor Architecture (Option 4).
/// </summary>
public interface IEpcsPrescriptionGrain : IGrainWithStringKey
{
    Task<GrainStates.EpcsPrescriptionState> GetAsync();

    Task CreateAsync(
        string patientId,
        string? prescriptionId,
        GrainStates.EpcsScriptTransactionType transactionType,
        string drugName, string? ndc, string deaSchedule,
        decimal quantity, int daysSupply, int refillsAuthorized,
        string? sig, string? diagnosisCode,
        string? prescriberNpi, string? prescriberDea, string? prescriberName,
        string? prescriberCredentialId,
        GrainStates.EpcsPharmacyDestination? destinationPharmacy);

    /// <summary>Records the digital signature with 2FA verification — 21 CFR Part 1311.120.</summary>
    Task SignAsync(GrainStates.EpcsSignatureRecord signature);

    /// <summary>Records successful transmission to Surescripts/pharmacy network.</summary>
    Task MarkTransmittedAsync(string? transmissionMessageId);

    /// <summary>Records acknowledgment from receiving pharmacy.</summary>
    Task MarkAcknowledgedAsync();

    /// <summary>Records a transmission error.</summary>
    Task MarkErrorAsync(string errorMessage);

    /// <summary>Cancels the e-prescription.</summary>
    Task CancelAsync(string userId, string? reason);

    /// <summary>Adds an audit trail entry — 21 CFR Part 1311.150.</summary>
    Task AddAuditEntryAsync(string action, string userId, string? details);
}
