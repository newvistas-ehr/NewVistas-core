// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for a Beneficiary Travel claim.
/// Maps to VistA DGBT package — processes travel reimbursement claims
/// for veteran travel to/from VA medical appointments.
/// VistA File #392 (Beneficiary Travel Claim).
/// </summary>
[GenerateSerializer]
public class BeneficiaryTravelClaimState
{
    /// <summary>Unique claim identifier.</summary>
    [Id(0)] public string ClaimId { get; set; } = string.Empty;

    /// <summary>Patient/veteran ID.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name.</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Associated appointment or visit ID.</summary>
    [Id(3)] public string? AppointmentId { get; set; }

    /// <summary>Date of travel/appointment.</summary>
    [Id(4)] public DateTime TravelDate { get; set; }

    /// <summary>Claim type: MILEAGE, COMMON_CARRIER, SPECIAL_MODE.</summary>
    [Id(5)] public string ClaimType { get; set; } = "MILEAGE";

    /// <summary>One-way mileage from home to facility.</summary>
    [Id(6)] public decimal OneWayMileage { get; set; }

    /// <summary>Round-trip indicator.</summary>
    [Id(7)] public bool IsRoundTrip { get; set; } = true;

    /// <summary>Total mileage for reimbursement.</summary>
    [Id(8)] public decimal TotalMileage { get; set; }

    /// <summary>Mileage reimbursement rate ($/mile).</summary>
    [Id(9)] public decimal MileageRate { get; set; } = 0.415m;

    /// <summary>Deductible amount per one-way trip.</summary>
    [Id(10)] public decimal DeductibleAmount { get; set; } = 3.00m;

    /// <summary>Number of deductibles applied (1 for one-way, 2 for round-trip).</summary>
    [Id(11)] public int DeductiblesApplied { get; set; }

    /// <summary>Calculated reimbursement amount before deductible.</summary>
    [Id(12)] public decimal GrossAmount { get; set; }

    /// <summary>Net payment amount after deductibles.</summary>
    [Id(13)] public decimal NetAmount { get; set; }

    /// <summary>Claim status: PENDING, APPROVED, DENIED, PAID, ON_HOLD, CANCELLED.</summary>
    [Id(14)] public string Status { get; set; } = "PENDING";

    /// <summary>Eligibility code: SC (service-connected), NSC, etc.</summary>
    [Id(15)] public string? EligibilityCode { get; set; }

    /// <summary>Whether the veteran is exempt from deductible.</summary>
    [Id(16)] public bool DeductibleExempt { get; set; }

    /// <summary>Mode of transportation: POV (privately owned vehicle), BUS, TAXI, AIRLINE, AMBULANCE, SPECIAL_VAN.</summary>
    [Id(17)] public string TransportMode { get; set; } = "POV";

    /// <summary>Origin address.</summary>
    [Id(18)] public string? OriginAddress { get; set; }

    /// <summary>Destination facility.</summary>
    [Id(19)] public string? DestinationFacility { get; set; }

    /// <summary>Authorized by (clerk ID).</summary>
    [Id(20)] public string? AuthorizedBy { get; set; }

    /// <summary>Denial reason if denied.</summary>
    [Id(21)] public string? DenialReason { get; set; }

    /// <summary>Payment method: CHECK, EFT (direct deposit).</summary>
    [Id(22)] public string PaymentMethod { get; set; } = "EFT";

    /// <summary>Payment date.</summary>
    [Id(23)] public DateTime? PaymentDate { get; set; }

    /// <summary>Special mode authorization number.</summary>
    [Id(24)] public string? SpecialModeAuthNumber { get; set; }

    /// <summary>Created date.</summary>
    [Id(25)] public DateTime CreatedDate { get; set; }

    /// <summary>Last modified date.</summary>
    [Id(26)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Lightweight claim entry for patient index display.
/// </summary>
[GenerateSerializer]
public class BeneficiaryTravelClaimEntry
{
    /// <summary>Claim ID.</summary>
    [Id(0)] public string ClaimId { get; set; } = string.Empty;

    /// <summary>Travel date.</summary>
    [Id(1)] public DateTime TravelDate { get; set; }

    /// <summary>Claim type.</summary>
    [Id(2)] public string ClaimType { get; set; } = string.Empty;

    /// <summary>Net amount.</summary>
    [Id(3)] public decimal NetAmount { get; set; }

    /// <summary>Status.</summary>
    [Id(4)] public string Status { get; set; } = string.Empty;

    /// <summary>Destination facility.</summary>
    [Id(5)] public string? DestinationFacility { get; set; }

    /// <summary>Total mileage.</summary>
    [Id(6)] public decimal TotalMileage { get; set; }
}

/// <summary>
/// State for patient's beneficiary travel claim index.
/// Keyed: "DGBT-INDEX:{patientId}".
/// </summary>
[GenerateSerializer]
public class BeneficiaryTravelIndexState
{
    /// <summary>All claims for this patient.</summary>
    [Id(0)] public List<BeneficiaryTravelClaimEntry> Claims { get; set; } = new();
}
