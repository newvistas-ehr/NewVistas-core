// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient identity verification and photo ID management.
/// Grain key: "IDENTITY:{patientId}"
/// </summary>
public interface IIdentityVerificationGrain : IGrainWithStringKey
{
    Task<IdentityVerificationState> GetAsync();
    Task<string> RecordVerificationAsync(IdentityDocumentType documentType, string? documentNumber,
        string? issuingAuthority, DateTime? expirationDate, IdentityVerificationResult result,
        bool photoOnFile, string? photoReference, string? discrepancyNotes,
        string verifiedByUserId, string verifiedByUserName, string? notes);
    Task UpdatePhotoAsync(string photoReference);
}
