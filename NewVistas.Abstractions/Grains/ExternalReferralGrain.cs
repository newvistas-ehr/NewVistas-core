// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Optional feature grain for external (community care) referral tracking.
/// Maps to IHS RPMS RCIS referral record.
/// Keyed by "EXT-REF:{guid}".
/// </summary>
public class ExternalReferralGrain : Grain, IExternalReferralGrain
{
    private readonly IPersistentState<ExternalReferralState> _state;

    public ExternalReferralGrain(
        [PersistentState("externalReferralState", "externalReferralStore")]
        IPersistentState<ExternalReferralState> state)
    {
        _state = state;
    }

    public Task<ExternalReferralState> GetReferralAsync() => Task.FromResult(_state.State);

    public async Task<ExternalReferralState> CreateReferralAsync(
        string patientId, string patientName, string referralType,
        string externalFacilityName, string? externalFacilityId,
        string? externalProviderName, string? externalProviderId,
        string purpose, string? diagnosis, string urgency,
        string referredByProviderId, string referredByProviderName,
        string? consultId, string? authorizationNumber,
        DateTime? appointmentDateTime, string? specialInstructions)
    {
        _state.State.ReferralId = this.GetPrimaryKeyString();
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.ReferralType = referralType;
        _state.State.ExternalFacilityName = externalFacilityName;
        _state.State.ExternalFacilityId = externalFacilityId;
        _state.State.ExternalProviderName = externalProviderName;
        _state.State.ExternalProviderId = externalProviderId;
        _state.State.Purpose = purpose;
        _state.State.Diagnosis = diagnosis;
        _state.State.Urgency = urgency;
        _state.State.Status = "SUBMITTED";
        _state.State.ReferredByProviderId = referredByProviderId;
        _state.State.ReferredByProviderName = referredByProviderName;
        _state.State.ReferralDate = DateTime.UtcNow;
        _state.State.ConsultId = consultId;
        _state.State.AuthorizationNumber = authorizationNumber;
        _state.State.AppointmentDateTime = appointmentDateTime;
        _state.State.SpecialInstructions = specialInstructions;
        _state.State.RequiresFollowUp = true;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();

        // Update the system-level index
        await UpdateIndexAsync();

        return _state.State;
    }

    public async Task UpdateStatusAsync(string status, string? statusReason, string updatedById, string updatedByName)
    {
        _state.State.Status = status;
        _state.State.StatusReason = statusReason;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        _state.State.FollowUps.Add(new ReferralFollowUp
        {
            FollowUpDate = DateTime.UtcNow,
            AuthorName = updatedByName,
            Note = $"Status changed to {status}. {statusReason ?? ""}"
        });

        if (status is "COMPLETED" or "DENIED" or "CANCELLED")
            _state.State.RequiresFollowUp = false;

        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task RecordAppointmentAsync(DateTime appointmentDateTime, string? confirmationNumber)
    {
        _state.State.AppointmentDateTime = appointmentDateTime;
        _state.State.ConfirmationNumber = confirmationNumber;
        if (_state.State.Status == "SUBMITTED" || _state.State.Status == "AUTHORIZED")
            _state.State.Status = "SCHEDULED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task RecordCompletionAsync(DateTime completionDate, string? outcomeNotes, string? clinicalFindings)
    {
        _state.State.CompletionDate = completionDate;
        _state.State.OutcomeNotes = outcomeNotes;
        _state.State.ClinicalFindings = clinicalFindings;
        _state.State.Status = "COMPLETED";
        _state.State.RequiresFollowUp = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task RecordDenialAsync(string denialReason, string deniedById, string deniedByName)
    {
        _state.State.Status = "DENIED";
        _state.State.StatusReason = denialReason;
        _state.State.RequiresFollowUp = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        _state.State.FollowUps.Add(new ReferralFollowUp
        {
            FollowUpDate = DateTime.UtcNow,
            AuthorName = deniedByName,
            Note = $"Referral denied: {denialReason}"
        });

        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task AddFollowUpAsync(string followUpNote, string authorName)
    {
        _state.State.FollowUps.Add(new ReferralFollowUp
        {
            FollowUpDate = DateTime.UtcNow,
            AuthorName = authorName,
            Note = followUpNote
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ─── Contract Health Services (CHS / PRC) ─────────────────────────────

    public async Task RequestChsAuthorizationAsync(
        decimal estimatedCost,
        string medicalPriorityClass,
        bool alternateResourcesChecked,
        string? alternateResourcesNote,
        string requestedByProviderId,
        string requestedByProviderName)
    {
        if (estimatedCost < 0)
            throw new ArgumentException("Estimated cost cannot be negative.", nameof(estimatedCost));
        if (string.IsNullOrWhiteSpace(medicalPriorityClass))
            throw new ArgumentException("Medical priority class is required.", nameof(medicalPriorityClass));

        _state.State.IsChsReferral = true;
        _state.State.EstimatedCost = estimatedCost;
        _state.State.MedicalPriorityClass = medicalPriorityClass;
        _state.State.AlternateResourcesChecked = alternateResourcesChecked;
        _state.State.AlternateResourcesNote = alternateResourcesNote;
        _state.State.Status = "PENDING_CHS_AUTH";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        _state.State.FollowUps.Add(new ReferralFollowUp
        {
            FollowUpDate = DateTime.UtcNow,
            AuthorName = requestedByProviderName,
            Note = $"CHS authorization requested. Priority class {medicalPriorityClass}, " +
                   $"estimated cost ${estimatedCost:F2}. " +
                   $"Alternate resources checked: {(alternateResourcesChecked ? "yes" : "no")}." +
                   (string.IsNullOrEmpty(alternateResourcesNote) ? "" : $" Note: {alternateResourcesNote}")
        });

        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task ApproveChsAuthorizationAsync(
        decimal authorizedAmount,
        string? authorizationNumber,
        string approvedById,
        string approvedByName)
    {
        if (!_state.State.IsChsReferral)
            throw new InvalidOperationException("This referral was not submitted as a CHS request; call RequestChsAuthorizationAsync first.");
        if (_state.State.Status != "PENDING_CHS_AUTH")
            throw new InvalidOperationException(
                $"CHS authorization can only be approved from PENDING_CHS_AUTH status (currently {_state.State.Status}).");
        if (authorizedAmount < 0)
            throw new ArgumentException("Authorized amount cannot be negative.", nameof(authorizedAmount));

        _state.State.AuthorizedAmount = authorizedAmount;
        _state.State.AuthorizationNumber = authorizationNumber;
        _state.State.Status = "AUTHORIZED";
        _state.State.ChsAuthorizationDate = DateTime.UtcNow;
        _state.State.ChsAuthorizedById = approvedById;
        _state.State.ChsAuthorizedByName = approvedByName;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        _state.State.FollowUps.Add(new ReferralFollowUp
        {
            FollowUpDate = DateTime.UtcNow,
            AuthorName = approvedByName,
            Note = $"CHS authorization approved: ${authorizedAmount:F2}" +
                   (string.IsNullOrEmpty(authorizationNumber) ? "." : $", auth# {authorizationNumber}.")
        });

        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task DenyChsAuthorizationAsync(
        string denialReason,
        string deniedById,
        string deniedByName)
    {
        if (!_state.State.IsChsReferral)
            throw new InvalidOperationException("This referral was not submitted as a CHS request; call RequestChsAuthorizationAsync first.");
        if (_state.State.Status != "PENDING_CHS_AUTH")
            throw new InvalidOperationException(
                $"CHS authorization can only be denied from PENDING_CHS_AUTH status (currently {_state.State.Status}).");

        _state.State.Status = "DENIED";
        _state.State.StatusReason = denialReason;
        _state.State.RequiresFollowUp = false;
        _state.State.ChsAuthorizationDate = DateTime.UtcNow;
        _state.State.ChsAuthorizedById = deniedById;
        _state.State.ChsAuthorizedByName = deniedByName;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        _state.State.FollowUps.Add(new ReferralFollowUp
        {
            FollowUpDate = DateTime.UtcNow,
            AuthorName = deniedByName,
            Note = $"CHS authorization denied: {denialReason}"
        });

        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task AttachDocumentAsync(string documentId, string documentType, string description)
    {
        if (!_state.State.Documents.Any(d => d.DocumentId == documentId))
        {
            _state.State.Documents.Add(new ReferralDocument
            {
                DocumentId = documentId,
                DocumentType = documentType,
                Description = description,
                AttachedDate = DateTime.UtcNow
            });
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    private async Task UpdateIndexAsync()
    {
        IExternalReferralIndexGrain index =
            GrainFactory.GetGrain<IExternalReferralIndexGrain>("EXT-REF-IDX");

        await index.AddOrUpdateAsync(new ExternalReferralIndexEntry
        {
            ReferralId = _state.State.ReferralId,
            PatientId = _state.State.PatientId,
            PatientName = _state.State.PatientName,
            ReferralType = _state.State.ReferralType,
            ExternalFacilityName = _state.State.ExternalFacilityName,
            Status = _state.State.Status,
            Urgency = _state.State.Urgency,
            ReferralDate = _state.State.ReferralDate,
            AppointmentDateTime = _state.State.AppointmentDateTime,
            ReferredByProviderName = _state.State.ReferredByProviderName,
            RequiresFollowUp = _state.State.RequiresFollowUp,
            IsChsReferral = _state.State.IsChsReferral,
            MedicalPriorityClass = _state.State.MedicalPriorityClass,
            AuthorizedAmount = _state.State.AuthorizedAmount,
        });
    }
}
