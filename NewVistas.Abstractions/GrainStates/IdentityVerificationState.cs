// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Type of identity document used for verification.
/// </summary>
[GenerateSerializer]
public enum IdentityDocumentType
{
    PhotoId = 0,
    DriversLicense = 1,
    MilitaryId = 2,
    Passport = 3,
    VaIdCard = 4,
    StateId = 5,
    TribalId = 6,
    Other = 7,
}

/// <summary>
/// Result of identity verification.
/// </summary>
[GenerateSerializer]
public enum IdentityVerificationResult
{
    NotVerified = 0,
    Verified = 1,
    VerifiedWithDiscrepancy = 2,
    Failed = 3,
    UnableToVerify = 4,
}

/// <summary>
/// A single identity verification event.
/// </summary>
[GenerateSerializer]
public record IdentityVerificationEvent
{
    [Id(0)] public string VerificationId { get; init; } = string.Empty;
    [Id(1)] public DateTime VerificationDateTime { get; init; }
    [Id(2)] public IdentityDocumentType DocumentType { get; init; }
    [Id(3)] public string? DocumentNumber { get; init; }
    [Id(4)] public string? DocumentIssuingAuthority { get; init; }
    [Id(5)] public DateTime? DocumentExpirationDate { get; init; }
    [Id(6)] public IdentityVerificationResult Result { get; init; }
    [Id(7)] public bool PhotoOnFile { get; init; }
    [Id(8)] public string? PhotoReference { get; init; }
    [Id(9)] public string? DiscrepancyNotes { get; init; }
    [Id(10)] public string VerifiedByUserId { get; init; } = string.Empty;
    [Id(11)] public string VerifiedByUserName { get; init; } = string.Empty;
    [Id(12)] public string? Notes { get; init; }
}

/// <summary>
/// State for patient identity verification.
/// Maps to VistA DG identity verification step in registration.
/// Grain key: "IDENTITY:{patientId}"
/// </summary>
[GenerateSerializer]
public class IdentityVerificationState
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Whether identity has been verified at least once.</summary>
    [Id(1)] public bool IsVerified { get; set; }

    /// <summary>Most recent verification result.</summary>
    [Id(2)] public IdentityVerificationResult CurrentVerificationResult { get; set; }

    /// <summary>Date of most recent verification.</summary>
    [Id(3)] public DateTime? LastVerificationDate { get; set; }

    /// <summary>Whether a photo is on file.</summary>
    [Id(4)] public bool HasPhotoOnFile { get; set; }

    /// <summary>Reference to stored photo (file path, blob URL, etc.).</summary>
    [Id(5)] public string? PhotoReference { get; set; }

    /// <summary>Date photo was captured.</summary>
    [Id(6)] public DateTime? PhotoCaptureDate { get; set; }

    /// <summary>Verification history.</summary>
    [Id(7)] public List<IdentityVerificationEvent> VerificationHistory { get; set; } = new();

    /// <summary>When last modified.</summary>
    [Id(8)] public DateTime LastModifiedDate { get; set; }
}
