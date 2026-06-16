// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Beneficiary Travel Claim Grain — manages a single travel pay claim.
/// Calculates reimbursement per federal mileage rate with deductible.
/// </summary>
public class BeneficiaryTravelClaimGrain : Grain, IBeneficiaryTravelClaimGrain
{
    private readonly IPersistentState<BeneficiaryTravelClaimState> _state;

    public BeneficiaryTravelClaimGrain(
        [PersistentState("btClaim", "btClaimStore")] IPersistentState<BeneficiaryTravelClaimState> state)
    {
        _state = state;
    }

    public Task<BeneficiaryTravelClaimState> GetClaimAsync()
        => Task.FromResult(_state.State);

    public async Task CreateClaimAsync(
        string patientId, string patientName, DateTime travelDate,
        string claimType, decimal oneWayMileage, bool isRoundTrip,
        string transportMode, string? originAddress, string? destinationFacility,
        string? appointmentId, string? eligibilityCode, bool deductibleExempt)
    {
        _state.State.ClaimId = this.GetPrimaryKeyString().Replace("DGBT:", "");
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.TravelDate = travelDate;
        _state.State.ClaimType = claimType;
        _state.State.OneWayMileage = oneWayMileage;
        _state.State.IsRoundTrip = isRoundTrip;
        _state.State.TotalMileage = isRoundTrip ? oneWayMileage * 2 : oneWayMileage;
        _state.State.TransportMode = transportMode;
        _state.State.OriginAddress = originAddress;
        _state.State.DestinationFacility = destinationFacility;
        _state.State.AppointmentId = appointmentId;
        _state.State.EligibilityCode = eligibilityCode;
        _state.State.DeductibleExempt = deductibleExempt;
        _state.State.Status = "PENDING";

        // Calculate reimbursement
        CalculateReimbursement();

        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ApproveAsync(string authorizedBy)
    {
        _state.State.Status = "APPROVED";
        _state.State.AuthorizedBy = authorizedBy;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DenyAsync(string reason, string authorizedBy)
    {
        _state.State.Status = "DENIED";
        _state.State.DenialReason = reason;
        _state.State.AuthorizedBy = authorizedBy;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordPaymentAsync(string paymentMethod, DateTime paymentDate)
    {
        _state.State.Status = "PAID";
        _state.State.PaymentMethod = paymentMethod;
        _state.State.PaymentDate = paymentDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task PlaceOnHoldAsync(string reason)
    {
        _state.State.Status = "ON_HOLD";
        _state.State.DenialReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelAsync(string reason)
    {
        _state.State.Status = "CANCELLED";
        _state.State.DenialReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetSpecialModeAuthAsync(string authNumber)
    {
        _state.State.SpecialModeAuthNumber = authNumber;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    private void CalculateReimbursement()
    {
        decimal gross = _state.State.TotalMileage * _state.State.MileageRate;
        _state.State.GrossAmount = Math.Round(gross, 2);

        if (_state.State.DeductibleExempt)
        {
            _state.State.DeductiblesApplied = 0;
            _state.State.NetAmount = _state.State.GrossAmount;
        }
        else
        {
            int deductibles = _state.State.IsRoundTrip ? 2 : 1;
            _state.State.DeductiblesApplied = deductibles;
            decimal totalDeductible = deductibles * _state.State.DeductibleAmount;
            _state.State.NetAmount = Math.Max(0, Math.Round(_state.State.GrossAmount - totalDeductible, 2));
        }
    }
}

/// <summary>
/// Beneficiary Travel Index Grain — patient's claim index.
/// </summary>
public class BeneficiaryTravelIndexGrain : Grain, IBeneficiaryTravelIndexGrain
{
    private readonly IPersistentState<BeneficiaryTravelIndexState> _state;

    public BeneficiaryTravelIndexGrain(
        [PersistentState("btIndex", "btIndexStore")] IPersistentState<BeneficiaryTravelIndexState> state)
    {
        _state = state;
    }

    public Task<List<BeneficiaryTravelClaimEntry>> GetClaimsAsync()
        => Task.FromResult(_state.State.Claims.OrderByDescending(c => c.TravelDate).ToList());

    public Task<List<BeneficiaryTravelClaimEntry>> GetClaimsByStatusAsync(string status)
        => Task.FromResult(_state.State.Claims.Where(c => c.Status == status).OrderByDescending(c => c.TravelDate).ToList());

    public async Task AddOrUpdateAsync(BeneficiaryTravelClaimEntry entry)
    {
        int idx = _state.State.Claims.FindIndex(c => c.ClaimId == entry.ClaimId);
        if (idx >= 0)
            _state.State.Claims[idx] = entry;
        else
            _state.State.Claims.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemoveAsync(string claimId)
    {
        _state.State.Claims.RemoveAll(c => c.ClaimId == claimId);
        await _state.WriteStateAsync();
    }
}
