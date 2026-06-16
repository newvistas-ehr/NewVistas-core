// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.EventSourcing;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents the state of a TIU Document grain based on VistA TIU DOCUMENT file (#8925).
///
/// Inherits the clinical event-sourcing outbox from <see cref="EventSourcedStateBase"/>
/// so that causal events for this note are persisted atomically with the
/// projection in a single <c>WriteStateAsync</c>.
/// </summary>
[GenerateSerializer]
public class TiuDocumentState : EventSourcedStateBase
{
    [Id(0)]
    public string DocumentId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Document Type (.01) � pointer to TIU DOCUMENT DEFINITION file (#8925.1)
    /// e.g., PROGRESS NOTE, DISCHARGE SUMMARY, CONSULT NOTE, SURGICAL NOTE
    /// </summary>
    [Id(2)]
    public string DocumentType { get; set; } = string.Empty;

    [Id(3)]
    public string? DocumentTypeId { get; set; }

    /// <summary>
    /// Status � UNSIGNED, UNCOSIGNED, COMPLETED, AMENDED, RETRACTED
    /// </summary>
    [Id(4)]
    public string Status { get; set; } = "UNSIGNED";

    /// <summary>
    /// Reference Date (.07)
    /// </summary>
    [Id(5)]
    public DateTime ReferenceDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Entry Date (.03)
    /// </summary>
    [Id(6)]
    public DateTime EntryDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Author (.02) � pointer to NEW PERSON file (#200)
    /// </summary>
    [Id(7)]
    public string? AuthorId { get; set; }

    [Id(8)]
    public string? AuthorName { get; set; }

    /// <summary>
    /// Expected Cosigner (.09)
    /// </summary>
    [Id(9)]
    public string? CosignerId { get; set; }

    [Id(10)]
    public string? CosignerName { get; set; }

    /// <summary>
    /// Signature Date/Time
    /// </summary>
    [Id(11)]
    public DateTime? SignedDateTime { get; set; }

    [Id(12)]
    public DateTime? CosignedDateTime { get; set; }

    /// <summary>
    /// Hospital Location (.06) � pointer to HOSPITAL LOCATION file (#44)
    /// </summary>
    [Id(13)]
    public string? LocationId { get; set; }

    [Id(14)]
    public string? LocationName { get; set; }

    /// <summary>
    /// Visit � pointer to VISIT file (#9000010)
    /// </summary>
    [Id(15)]
    public string? VisitId { get; set; }

    /// <summary>
    /// Subject (title/summary)
    /// </summary>
    [Id(16)]
    public string? Subject { get; set; }

    /// <summary>
    /// Report Text � the full document body
    /// </summary>
    [Id(17)]
    public string ReportText { get; set; } = string.Empty;

    /// <summary>
    /// Addendum IDs � linked addenda
    /// </summary>
    [Id(18)]
    public List<string> AddendumIds { get; set; } = new();

    /// <summary>
    /// Parent Document ID � if this is an addendum
    /// </summary>
    [Id(19)]
    public string? ParentDocumentId { get; set; }

    [Id(20)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(21)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    // ── GAP 8: Document Locking ───────────────────────────────────────────────

    /// <summary>
    /// True when document is currently locked for editing.
    /// Mirrors VistA rTIU.pas LockDocument() / UnlockDocument().
    /// </summary>
    [Id(22)]
    public bool IsLocked { get; set; }

    /// <summary>
    /// User who holds the current document lock
    /// </summary>
    [Id(23)]
    public string? LockedByUserId { get; set; }

    /// <summary>
    /// Date/time the document lock was acquired
    /// </summary>
    [Id(24)]
    public DateTime? LockDateTime { get; set; }

    // ── GAP 8: Delete Justification ───────────────────────────────────────────

    /// <summary>
    /// Justification text required when deleting a completed document.
    /// Mirrors VistA rTIU.pas JustifyDocumentDelete() — required for COMPLETED/AMENDED docs.
    /// </summary>
    [Id(25)]
    public string? DeleteReason { get; set; }

    // ── GAP 8: Cosigner Required Flag ─────────────────────────────────────────

    /// <summary>
    /// True when the document title definition requires a cosigner.
    /// Mirrors VistA rTIU.pas AskCosignerForDocument/Title() logic.
    /// </summary>
    [Id(26)]
    public bool CosignerRequired { get; set; }

    /// <summary>
    /// Additional signer IDs beyond the primary author/cosigner
    /// </summary>
    [Id(27)]
    public List<string> AdditionalSignerIds { get; set; } = new();

    // ── GAP 8: ID Notes (Interdisciplinary Notes) ─────────────────────────────

    /// <summary>
    /// True when this document is an ID Note child entry.
    /// Mirrors VistA rTIU.pas CanTitleBeIDChild() / AttachEntryToParent().
    /// </summary>
    [Id(28)]
    public bool IsIdNoteChild { get; set; }

    /// <summary>
    /// Parent document ID when this is an ID Note child (attached to a parent entry)
    /// </summary>
    [Id(29)]
    public string? IdParentDocumentId { get; set; }

    /// <summary>
    /// IDs of child ID Note entries attached to this document.
    /// Mirrors VistA rTIU.pas CanReceiveAttachment().
    /// </summary>
    [Id(30)]
    public List<string> IdChildDocumentIds { get; set; } = new();

    /// <summary>
    /// Deep copy. Used at event/state boundaries so that mutating the live
    /// document on the grain does not retroactively mutate the historical
    /// snapshot stored on a clinical event payload.
    /// </summary>
    public TiuDocumentState Clone() => new()
    {
        DocumentId = DocumentId,
        PatientId = PatientId,
        DocumentType = DocumentType,
        DocumentTypeId = DocumentTypeId,
        Status = Status,
        ReferenceDate = ReferenceDate,
        EntryDate = EntryDate,
        AuthorId = AuthorId,
        AuthorName = AuthorName,
        CosignerId = CosignerId,
        CosignerName = CosignerName,
        SignedDateTime = SignedDateTime,
        CosignedDateTime = CosignedDateTime,
        LocationId = LocationId,
        LocationName = LocationName,
        VisitId = VisitId,
        Subject = Subject,
        ReportText = ReportText,
        AddendumIds = new List<string>(AddendumIds),
        ParentDocumentId = ParentDocumentId,
        CreatedDate = CreatedDate,
        LastModifiedDate = LastModifiedDate,
        IsLocked = IsLocked,
        LockedByUserId = LockedByUserId,
        LockDateTime = LockDateTime,
        DeleteReason = DeleteReason,
        CosignerRequired = CosignerRequired,
        AdditionalSignerIds = new List<string>(AdditionalSignerIds),
        IsIdNoteChild = IsIdNoteChild,
        IdParentDocumentId = IdParentDocumentId,
        IdChildDocumentIds = new List<string>(IdChildDocumentIds)
    };
}
