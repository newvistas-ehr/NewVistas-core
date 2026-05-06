// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PharmacyPosClaimGrain : Grain, IPharmacyPosClaimGrain
{
    private readonly IPersistentState<PharmacyPosClaimState> _state;

    public PharmacyPosClaimGrain(
        [PersistentState("posClaimState", "posClaimStore")]
        IPersistentState<PharmacyPosClaimState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ClaimId))
        {
            _state.State.ClaimId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<PharmacyPosClaimState> GetAsync() => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string patientId,
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
        _state.State.PatientId = patientId;
        _state.State.PrescriptionId = prescriptionId;
        _state.State.TransactionType = transactionType;
        _state.State.Status = PosClaimStatus.Pending;
        _state.State.Bin = bin;
        _state.State.Pcn = pcn;
        _state.State.NcpdpVersion = ncpdpVersion;
        _state.State.GroupNumber = groupNumber;
        _state.State.CardholderId = cardholderId;
        _state.State.RelationshipCode = relationshipCode;
        _state.State.InsurerId = insurerId;
        _state.State.InsurerName = insurerName;
        _state.State.Ndc = ndc;
        _state.State.DrugName = drugName;
        _state.State.QuantityDispensed = quantityDispensed;
        _state.State.DaysSupply = daysSupply;
        _state.State.DateOfService = dateOfService;
        _state.State.IngredientCostSubmitted = ingredientCostSubmitted;
        _state.State.DispensingFeeSubmitted = dispensingFeeSubmitted;
        _state.State.UsualAndCustomary = usualAndCustomary;
        _state.State.GrossAmountDue = (ingredientCostSubmitted ?? 0) + (dispensingFeeSubmitted ?? 0);
        _state.State.PharmacyNcpdpId = pharmacyNcpdpId;
        _state.State.PharmacistName = pharmacistName;
        _state.State.PrescriberNpi = prescriberNpi;
        _state.State.PrescriberName = prescriberName;
        _state.State.OriginalClaimId = originalClaimId;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AdjudicateAsync(
        PosClaimStatus status,
        decimal? insurancePaidAmount,
        decimal? patientResponsibility,
        decimal? copayAmount,
        decimal? coinsuranceAmount,
        decimal? deductibleAmount,
        string? authorizationNumber,
        List<PosRejection>? rejections,
        List<DurMessage>? durMessages)
    {
        _state.State.Status = status;
        _state.State.InsurancePaidAmount = insurancePaidAmount;
        _state.State.PatientResponsibility = patientResponsibility;
        _state.State.CopayAmount = copayAmount;
        _state.State.CoinsuranceAmount = coinsuranceAmount;
        _state.State.DeductibleAmount = deductibleAmount;
        _state.State.AuthorizationNumber = authorizationNumber;
        _state.State.Rejections = rejections ?? new();
        _state.State.DurMessages = durMessages ?? new();
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReverseAsync()
    {
        _state.State.Status = PosClaimStatus.Reversed;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelAsync()
    {
        _state.State.Status = PosClaimStatus.Cancelled;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
