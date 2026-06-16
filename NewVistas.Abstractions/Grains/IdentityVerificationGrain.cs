// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class IdentityVerificationGrain : Grain, IIdentityVerificationGrain
{
    private readonly IPersistentState<IdentityVerificationState> _state;
    public IdentityVerificationGrain([PersistentState("identityVerificationState", "identityVerificationStore")] IPersistentState<IdentityVerificationState> state) { _state = state; }

    public Task<IdentityVerificationState> GetAsync() => Task.FromResult(_state.State);

    public async Task<string> RecordVerificationAsync(IdentityDocumentType documentType, string? documentNumber,
        string? issuingAuthority, DateTime? expirationDate, IdentityVerificationResult result,
        bool photoOnFile, string? photoReference, string? discrepancyNotes,
        string verifiedByUserId, string verifiedByUserName, string? notes)
    {
        string verificationId = $"IDV-{Guid.NewGuid():N}";
        _state.State.PatientId = _state.State.PatientId == string.Empty ? this.GetPrimaryKeyString() : _state.State.PatientId;
        _state.State.VerificationHistory.Insert(0, new IdentityVerificationEvent
        {
            VerificationId = verificationId,
            VerificationDateTime = DateTime.UtcNow,
            DocumentType = documentType,
            DocumentNumber = documentNumber,
            DocumentIssuingAuthority = issuingAuthority,
            DocumentExpirationDate = expirationDate,
            Result = result,
            PhotoOnFile = photoOnFile,
            PhotoReference = photoReference,
            DiscrepancyNotes = discrepancyNotes,
            VerifiedByUserId = verifiedByUserId,
            VerifiedByUserName = verifiedByUserName,
            Notes = notes,
        });
        _state.State.IsVerified = result == IdentityVerificationResult.Verified || result == IdentityVerificationResult.VerifiedWithDiscrepancy;
        _state.State.CurrentVerificationResult = result;
        _state.State.LastVerificationDate = DateTime.UtcNow;
        if (photoOnFile && !string.IsNullOrEmpty(photoReference))
        {
            _state.State.HasPhotoOnFile = true;
            _state.State.PhotoReference = photoReference;
            _state.State.PhotoCaptureDate = DateTime.UtcNow;
        }
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return verificationId;
    }

    public async Task UpdatePhotoAsync(string photoReference)
    {
        _state.State.HasPhotoOnFile = true;
        _state.State.PhotoReference = photoReference;
        _state.State.PhotoCaptureDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
