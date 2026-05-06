// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Pharmacy Point of Sale — Site Flavor Architecture (Option 4: Composition).
/// Checks the PHARMACY_POS feature flag before delegating to
/// the optional POS grains (RPMS ABSP — File #9002313).
/// If the feature is not enabled, the POS grains are never activated
/// and consume no resources.
/// </summary>
public partial class PatientWorkflowGrain
{
    private const string PharmacyPosFeature = "PHARMACY_POS";

    private IPharmacyPosClaimGrain PosClaim(string claimId)
        => GrainFactory.GetGrain<IPharmacyPosClaimGrain>(claimId);

    private IPharmacyPosClaimIndexGrain PosClaimIndex()
        => GrainFactory.GetGrain<IPharmacyPosClaimIndexGrain>($"POS-CLAIM-IDX:{PatientId}");

    private async Task EnsurePharmacyPosEnabledAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(PharmacyPosFeature);
        if (!enabled)
            throw new InvalidOperationException(
                "Pharmacy Point of Sale is not enabled for this site. " +
                "Enable the PHARMACY_POS feature in Site Parameters.");
    }

    public async Task<string> SubmitPosClaimAsync(
        string? prescriptionId,
        NcpdpTransactionType transactionType,
        string bin, string pcn, string ncpdpVersion,
        string? groupNumber, string? cardholderId, string? relationshipCode,
        string? insurerId, string? insurerName,
        string? ndc, string? drugName, decimal? quantityDispensed, int? daysSupply,
        DateTime? dateOfService,
        decimal? ingredientCostSubmitted, decimal? dispensingFeeSubmitted,
        decimal? usualAndCustomary,
        string? pharmacyNcpdpId, string? pharmacistName,
        string? prescriberNpi, string? prescriberName,
        string? originalClaimId)
    {
        await EnsurePharmacyPosEnabledAsync();

        string claimId = $"POS-CLAIM:{Guid.NewGuid()}";
        await PosClaim(claimId).CreateAsync(
            PatientId, prescriptionId, transactionType,
            bin, pcn, ncpdpVersion,
            groupNumber, cardholderId, relationshipCode,
            insurerId, insurerName,
            ndc, drugName, quantityDispensed, daysSupply,
            dateOfService,
            ingredientCostSubmitted, dispensingFeeSubmitted, usualAndCustomary,
            pharmacyNcpdpId, pharmacistName,
            prescriberNpi, prescriberName,
            originalClaimId);

        await PosClaimIndex().AddEntryAsync(new PosClaimIndexEntry
        {
            ClaimId               = claimId,
            PatientId             = PatientId,
            TransactionType       = transactionType,
            Status                = PosClaimStatus.Pending,
            DrugName              = drugName,
            DateOfService         = dateOfService,
            InsurerName           = insurerName,
        });
        return claimId;
    }

    public async Task AdjudicatePosClaimAsync(string claimId,
        PosClaimStatus status,
        decimal? insurancePaidAmount, decimal? patientResponsibility,
        decimal? copayAmount, decimal? coinsuranceAmount, decimal? deductibleAmount,
        string? authorizationNumber,
        List<PosRejection>? rejections,
        List<DurMessage>? durMessages)
    {
        await EnsurePharmacyPosEnabledAsync();
        await PosClaim(claimId).AdjudicateAsync(
            status, insurancePaidAmount, patientResponsibility,
            copayAmount, coinsuranceAmount, deductibleAmount,
            authorizationNumber, rejections, durMessages);
        await PosClaimIndex().UpdateEntryStatusAsync(
            claimId, status, insurancePaidAmount, patientResponsibility);
    }

    public async Task ReversePosClaimAsync(string claimId)
    {
        await EnsurePharmacyPosEnabledAsync();
        await PosClaim(claimId).ReverseAsync();
        await PosClaimIndex().UpdateEntryStatusAsync(
            claimId, PosClaimStatus.Reversed, null, null);
    }

    public async Task<PharmacyPosClaimState> GetPosClaimAsync(string claimId)
    {
        await EnsurePharmacyPosEnabledAsync();
        return await PosClaim(claimId).GetAsync();
    }

    public async Task<List<PosClaimIndexEntry>> GetPosClaimsAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(PharmacyPosFeature);
        if (!enabled) return [];
        return await PosClaimIndex().GetAllAsync();
    }

    public async Task<List<PosClaimIndexEntry>> GetPosClaimsByStatusAsync(PosClaimStatus status)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(PharmacyPosFeature);
        if (!enabled) return [];
        return await PosClaimIndex().GetByStatusAsync(status);
    }
}
