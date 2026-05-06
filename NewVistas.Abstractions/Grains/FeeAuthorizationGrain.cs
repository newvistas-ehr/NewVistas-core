// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class FeeAuthorizationGrain : Grain, IFeeAuthorizationGrain
{
    private readonly IPersistentState<FeeAuthorizationState> _state;

    public FeeAuthorizationGrain(
        [PersistentState("feeAuthorizationState", "feeAuthorizationStore")]
        IPersistentState<FeeAuthorizationState> state)
    {
        _state = state;
    }

    public Task<FeeAuthorizationState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string patientId,
        string vendorId,
        string vendorName,
        FeeServiceType serviceType,
        DateTime authorizationDate,
        DateTime effectiveDate,
        DateTime? expirationDate,
        decimal authorizedAmount,
        string authorizedByUserId,
        string authorizedByUserName,
        string serviceDescription,
        int? maxVisits,
        string? diagnosisCode,
        string? authorizationNumber,
        string? notes)
    {
        _state.State.AuthorizationId      = this.GetPrimaryKeyString();
        _state.State.PatientId            = patientId;
        _state.State.VendorId             = vendorId;
        _state.State.VendorName           = vendorName;
        _state.State.ServiceType          = serviceType;
        _state.State.AuthorizationDate    = authorizationDate;
        _state.State.EffectiveDate        = effectiveDate;
        _state.State.ExpirationDate       = expirationDate;
        _state.State.AuthorizedAmount     = authorizedAmount;
        _state.State.SpentAmount          = 0m;
        _state.State.RemainingAmount      = authorizedAmount;
        _state.State.AuthorizedByUserId   = authorizedByUserId;
        _state.State.AuthorizedByUserName = authorizedByUserName;
        _state.State.ServiceDescription   = serviceDescription;
        _state.State.MaxVisits            = maxVisits;
        _state.State.VisitsUsed           = 0;
        _state.State.DiagnosisCode        = diagnosisCode;
        _state.State.AuthorizationNumber  = authorizationNumber;
        _state.State.Notes                = notes;
        _state.State.Status               = FeeAuthorizationStatus.Active;
        _state.State.CreatedDate          = DateTime.UtcNow;
        _state.State.LastModifiedDate     = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SuspendAsync(string reason, string userId)
    {
        _state.State.Status           = FeeAuthorizationStatus.Suspended;
        _state.State.Notes            = string.IsNullOrEmpty(_state.State.Notes)
            ? $"Suspended by {userId}: {reason}"
            : _state.State.Notes + $"\nSuspended by {userId}: {reason}";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelAsync(string reason, string userId)
    {
        _state.State.Status           = FeeAuthorizationStatus.Cancelled;
        _state.State.Notes            = string.IsNullOrEmpty(_state.State.Notes)
            ? $"Cancelled by {userId}: {reason}"
            : _state.State.Notes + $"\nCancelled by {userId}: {reason}";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordInvoicePaymentAsync(string invoiceId, decimal paidAmount)
    {
        _state.State.SpentAmount     += paidAmount;
        _state.State.RemainingAmount  = _state.State.AuthorizedAmount - _state.State.SpentAmount;
        _state.State.VisitsUsed      += 1;

        if (!_state.State.InvoiceIds.Contains(invoiceId))
            _state.State.InvoiceIds.Add(invoiceId);

        if (_state.State.RemainingAmount <= 0m)
            _state.State.Status = FeeAuthorizationStatus.Exhausted;

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
