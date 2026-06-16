// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
