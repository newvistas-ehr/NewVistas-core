// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Notes;
using NewVistas.Abstractions.EventSourcing;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// TIU Document Grain implementation based on VistA TIU DOCUMENT file (#8925)
/// </summary>
public class TiuDocumentGrain : Grain, ITiuDocumentGrain
{
    private readonly IPersistentState<TiuDocumentState> _state;

    public TiuDocumentGrain(
        [PersistentState("tiuDocumentState", "tiuDocumentStore")] IPersistentState<TiuDocumentState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.DocumentId))
        {
            _state.State.DocumentId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        await base.OnActivateAsync(cancellationToken);

        // Drain anything left in the clinical-event outbox from a prior crash.
        if (_state.State.PendingEvents.Count > 0)
            await this.DrainOutboxAsync(_state, GrainFactory);
    }

    private string? CurrentUserId => RequestContext.Get(RequestContextKeys.UserId) as string;
    private string? CurrentUserName => RequestContext.Get(RequestContextKeys.UserName) as string;

    public Task<TiuDocumentState> GetDocumentAsync() => Task.FromResult(_state.State);

    public async Task CreateDocumentAsync(
        string patientId, string documentType, string? documentTypeId,
        string reportText, string? subject,
        string? authorId, string? authorName,
        string? cosignerId, string? cosignerName,
        string? locationId, string? locationName,
        string? visitId, DateTime referenceDate)
    {
        // Idempotent: re-issued create on the same grain key is a no-op.
        if (!string.IsNullOrEmpty(_state.State.PatientId))
            return;

        _state.State.PatientId = patientId;
        _state.State.DocumentType = documentType;
        _state.State.DocumentTypeId = documentTypeId;
        _state.State.ReportText = reportText;
        _state.State.Subject = subject;
        _state.State.AuthorId = authorId;
        _state.State.AuthorName = authorName;
        _state.State.CosignerId = cosignerId;
        _state.State.CosignerName = cosignerName;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.VisitId = visitId;
        _state.State.ReferenceDate = referenceDate;
        _state.State.EntryDate = DateTime.UtcNow;
        _state.State.Status = "UNSIGNED";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new NoteCreatedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = patientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            DocumentId = _state.State.DocumentId,
            Snapshot = _state.State.Clone()
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task SignDocumentAsync(DateTime signedDateTime)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId)) return;
        if (_state.State.SignedDateTime.HasValue) return; // already signed

        string resultingStatus =
            _state.State.CosignerId != null ? "UNCOSIGNED" : "COMPLETED";

        _state.State.SignedDateTime = signedDateTime;
        _state.State.Status = resultingStatus;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new NoteSignedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            DocumentId = _state.State.DocumentId,
            SignedDateTime = signedDateTime,
            ResultingStatus = resultingStatus
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task CosignDocumentAsync(DateTime cosignedDateTime)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId)) return;
        if (_state.State.CosignedDateTime.HasValue) return; // already cosigned

        _state.State.CosignedDateTime = cosignedDateTime;
        _state.State.Status = "COMPLETED";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new NoteCosignedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            DocumentId = _state.State.DocumentId,
            CosignedDateTime = cosignedDateTime
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task AmendDocumentAsync(string amendedText)
    {
        _state.State.ReportText = amendedText;
        _state.State.Status = "AMENDED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddAddendumAsync(string addendumId)
    {
        _state.State.AddendumIds.Add(addendumId);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetParentDocumentIdAsync(string parentDocumentId)
    {
        _state.State.ParentDocumentId = parentDocumentId;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RetractDocumentAsync()
    {
        _state.State.Status = "RETRACTED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── GAP 8: Document Locking ───────────────────────────────────────────────

    public async Task<bool> LockDocumentAsync(string userId, DateTime lockDateTime)
    {
        if (_state.State.IsLocked && _state.State.LockedByUserId != userId)
            return false;

        _state.State.IsLocked = true;
        _state.State.LockedByUserId = userId;
        _state.State.LockDateTime = lockDateTime;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return true;
    }

    public async Task UnlockDocumentAsync(string userId)
    {
        if (_state.State.LockedByUserId == userId || !_state.State.IsLocked)
        {
            _state.State.IsLocked = false;
            _state.State.LockedByUserId = null;
            _state.State.LockDateTime = null;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    // ── GAP 8: Delete Justification ───────────────────────────────────────────

    public async Task DeleteWithJustificationAsync(string reason)
    {
        _state.State.DeleteReason = reason;
        _state.State.Status = "DELETED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── GAP 8: Cosigner Configuration ─────────────────────────────────────────

    public async Task SetCosignerRequiredAsync(bool required)
    {
        _state.State.CosignerRequired = required;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddAdditionalSignerAsync(string signerId)
    {
        if (!_state.State.AdditionalSignerIds.Contains(signerId))
            _state.State.AdditionalSignerIds.Add(signerId);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── GAP 8: ID Notes (Interdisciplinary Notes) ─────────────────────────────

    public async Task AttachToParentAsync(string parentDocumentId)
    {
        _state.State.IsIdNoteChild = true;
        _state.State.IdParentDocumentId = parentDocumentId;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DetachFromParentAsync()
    {
        _state.State.IsIdNoteChild = false;
        _state.State.IdParentDocumentId = null;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddIdNoteChildAsync(string childDocumentId)
    {
        if (!_state.State.IdChildDocumentIds.Contains(childDocumentId))
            _state.State.IdChildDocumentIds.Add(childDocumentId);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
