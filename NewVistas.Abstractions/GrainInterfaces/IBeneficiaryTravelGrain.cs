// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Beneficiary Travel Claim Grain — manages a single travel pay claim.
/// Key: "DGBT:{claimId}"
/// Maps to VistA DGBT package / File #392.
/// </summary>
public interface IBeneficiaryTravelClaimGrain : IGrainWithStringKey
{
    Task<BeneficiaryTravelClaimState> GetClaimAsync();

    Task CreateClaimAsync(
        string patientId, string patientName, DateTime travelDate,
        string claimType, decimal oneWayMileage, bool isRoundTrip,
        string transportMode, string? originAddress, string? destinationFacility,
        string? appointmentId, string? eligibilityCode, bool deductibleExempt);

    Task ApproveAsync(string authorizedBy);
    Task DenyAsync(string reason, string authorizedBy);
    Task RecordPaymentAsync(string paymentMethod, DateTime paymentDate);
    Task PlaceOnHoldAsync(string reason);
    Task CancelAsync(string reason);
    Task SetSpecialModeAuthAsync(string authNumber);
}

/// <summary>
/// Beneficiary Travel Index Grain — tracks all claims for a patient.
/// Key: "DGBT-INDEX:{patientId}"
/// </summary>
public interface IBeneficiaryTravelIndexGrain : IGrainWithStringKey
{
    Task<List<BeneficiaryTravelClaimEntry>> GetClaimsAsync();
    Task<List<BeneficiaryTravelClaimEntry>> GetClaimsByStatusAsync(string status);
    Task AddOrUpdateAsync(BeneficiaryTravelClaimEntry entry);
    Task RemoveAsync(string claimId);
}
