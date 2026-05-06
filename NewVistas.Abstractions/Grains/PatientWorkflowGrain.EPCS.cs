// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// EPCS (E-Prescribing for Controlled Substances) — Site Flavor Architecture (Option 4: Composition).
/// Checks the EPCS feature flag before delegating to the optional EPCS grains.
/// Implements 21 CFR Part 1311 requirements: digital signature with 2FA,
/// identity proofing, and DEA-compliant audit trail.
/// If the feature is not enabled, the EPCS grains are never activated
/// and consume no resources.
/// </summary>
public partial class PatientWorkflowGrain
{
    private const string EpcsFeature = "EPCS";

    private IEpcsPrescriptionGrain EpcsRx(string epcsId)
        => GrainFactory.GetGrain<IEpcsPrescriptionGrain>(epcsId);

    private IEpcsPrescriptionIndexGrain EpcsRxIndex()
        => GrainFactory.GetGrain<IEpcsPrescriptionIndexGrain>($"EPCS-RX-IDX:{PatientId}");

    private async Task EnsureEpcsEnabledAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(EpcsFeature);
        if (!enabled)
            throw new InvalidOperationException(
                "EPCS (E-Prescribing for Controlled Substances) is not enabled for this site. " +
                "Enable the EPCS feature in Site Parameters.");
    }

    public async Task<string> CreateEpcsPrescriptionAsync(
        string? prescriptionId,
        EpcsScriptTransactionType transactionType,
        string drugName, string? ndc, string deaSchedule,
        decimal quantity, int daysSupply, int refillsAuthorized,
        string? sig, string? diagnosisCode,
        string? prescriberNpi, string? prescriberDea, string? prescriberName,
        string? prescriberCredentialId,
        EpcsPharmacyDestination? destinationPharmacy)
    {
        await EnsureEpcsEnabledAsync();

        string epcsId = $"EPCS-RX:{Guid.NewGuid()}";
        await EpcsRx(epcsId).CreateAsync(
            PatientId, prescriptionId, transactionType,
            drugName, ndc, deaSchedule,
            quantity, daysSupply, refillsAuthorized,
            sig, diagnosisCode,
            prescriberNpi, prescriberDea, prescriberName,
            prescriberCredentialId, destinationPharmacy);

        await EpcsRxIndex().AddEntryAsync(new EpcsPrescriptionIndexEntry
        {
            EpcsId          = epcsId,
            PatientId       = PatientId,
            TransactionType = transactionType,
            Status          = EpcsTransmissionStatus.Draft,
            DrugName        = drugName,
            DeaSchedule     = deaSchedule,
            PrescriberName  = prescriberName,
            CreatedDate     = DateTime.UtcNow,
            IsSigned        = false,
        });
        return epcsId;
    }

    public async Task SignEpcsPrescriptionAsync(string epcsId, EpcsSignatureRecord signature)
    {
        await EnsureEpcsEnabledAsync();
        await EpcsRx(epcsId).SignAsync(signature);
        await EpcsRxIndex().UpdateEntryAsync(epcsId, EpcsTransmissionStatus.Signed, true);
    }

    public async Task TransmitEpcsPrescriptionAsync(string epcsId, string? transmissionMessageId)
    {
        await EnsureEpcsEnabledAsync();
        await EpcsRx(epcsId).MarkTransmittedAsync(transmissionMessageId);
        await EpcsRxIndex().UpdateEntryAsync(epcsId, EpcsTransmissionStatus.Transmitted, true);
    }

    public async Task AcknowledgeEpcsPrescriptionAsync(string epcsId)
    {
        await EnsureEpcsEnabledAsync();
        await EpcsRx(epcsId).MarkAcknowledgedAsync();
        await EpcsRxIndex().UpdateEntryAsync(epcsId, EpcsTransmissionStatus.Acknowledged, true);
    }

    public async Task CancelEpcsPrescriptionAsync(string epcsId, string userId, string? reason)
    {
        await EnsureEpcsEnabledAsync();
        await EpcsRx(epcsId).CancelAsync(userId, reason);
        await EpcsRxIndex().UpdateEntryAsync(epcsId, EpcsTransmissionStatus.Cancelled, false);
    }

    public async Task<EpcsPrescriptionState> GetEpcsPrescriptionAsync(string epcsId)
    {
        await EnsureEpcsEnabledAsync();
        return await EpcsRx(epcsId).GetAsync();
    }

    public async Task<List<EpcsPrescriptionIndexEntry>> GetEpcsPrescriptionsAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(EpcsFeature);
        if (!enabled) return [];
        return await EpcsRxIndex().GetAllAsync();
    }

    public async Task<List<EpcsPrescriptionIndexEntry>> GetEpcsPrescriptionsByStatusAsync(
        EpcsTransmissionStatus status)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(EpcsFeature);
        if (!enabled) return [];
        return await EpcsRxIndex().GetByStatusAsync(status);
    }
}
