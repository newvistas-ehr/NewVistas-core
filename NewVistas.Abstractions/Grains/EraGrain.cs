// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class EraGrain : Grain, IEraGrain
{
    private readonly IPersistentState<EraState> _state;

    public EraGrain(
        [PersistentState("eraState", "eraStore")]
        IPersistentState<EraState> state)
    {
        _state = state;
    }

    public Task<EraState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task RecordAsync(
        string payerId,
        string payerName,
        string? checkNumber,
        string? paymentMethod,
        DateTime paymentDate,
        decimal totalPaymentAmount,
        string? transactionSetId,
        List<EraClaimPayment> claimPayments,
        string? notes)
    {
        DateTime now = DateTime.UtcNow;

        _state.State.EraId               = this.GetPrimaryKeyString();
        _state.State.PayerId             = payerId;
        _state.State.PayerName           = payerName;
        _state.State.CheckNumber         = checkNumber;
        _state.State.PaymentMethod       = paymentMethod;
        _state.State.PaymentDate         = paymentDate;
        _state.State.TotalPaymentAmount  = totalPaymentAmount;
        _state.State.TransactionSetId    = transactionSetId;
        _state.State.ClaimPayments       = claimPayments;
        _state.State.Status              = EraStatus.Received;
        _state.State.Notes               = notes;
        _state.State.CreatedDate         = now;
        _state.State.LastModifiedDate    = now;

        await _state.WriteStateAsync();
    }

    public async Task ProcessAsync()
    {
        try
        {
            foreach (EraClaimPayment cp in _state.State.ClaimPayments)
            {
                IEdiClaimGrain claim = GrainFactory.GetGrain<IEdiClaimGrain>($"EDI-CLAIM:{cp.ClaimId}");
                await claim.RecordEraPaymentAsync(
                    _state.State.EraId,
                    cp.PaidAmount,
                    cp.AllowedAmount,
                    cp.AdjustmentAmount,
                    cp.DenialReasonCode,
                    cp.DenialReasonDescription);
            }

            _state.State.Status        = EraStatus.Posted;
            _state.State.ProcessedDate = DateTime.UtcNow;
            _state.State.ErrorMessage  = null;
        }
        catch (Exception ex)
        {
            _state.State.Status       = EraStatus.Error;
            _state.State.ErrorMessage = ex.Message;
        }

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
