// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// TIU Document Grain Interface based on VistA TIU DOCUMENT file (#8925)
/// Progress Notes, Discharge Summaries, Consult Notes
/// </summary>
public interface ITiuDocumentGrain : IGrainWithStringKey
{
    Task<GrainStates.TiuDocumentState> GetDocumentAsync();

    Task CreateDocumentAsync(
        string patientId,
        string documentType,
        string? documentTypeId,
        string reportText,
        string? subject,
        string? authorId,
        string? authorName,
        string? cosignerId,
        string? cosignerName,
        string? locationId,
        string? locationName,
        string? visitId,
        DateTime referenceDate);

    Task SignDocumentAsync(DateTime signedDateTime);
    Task CosignDocumentAsync(DateTime cosignedDateTime);
    Task AmendDocumentAsync(string amendedText);
    Task AddAddendumAsync(string addendumId);
    Task SetParentDocumentIdAsync(string parentDocumentId);
    Task RetractDocumentAsync();

    // ── GAP 8: Document Locking ───────────────────────────────────────────────

    /// <summary>
    /// Acquires a document lock for editing. Mirrors VistA rTIU.pas LockDocument().
    /// Returns false if the document is already locked by another user.
    /// </summary>
    Task<bool> LockDocumentAsync(string userId, DateTime lockDateTime);

    /// <summary>
    /// Releases the document lock. Mirrors VistA rTIU.pas UnlockDocument().
    /// </summary>
    Task UnlockDocumentAsync(string userId);

    // ── GAP 8: Delete Justification ───────────────────────────────────────────

    /// <summary>
    /// Records the required justification when deleting a signed document.
    /// Mirrors VistA rTIU.pas JustifyDocumentDelete().
    /// </summary>
    Task DeleteWithJustificationAsync(string reason);

    // ── GAP 8: Cosigner Configuration ─────────────────────────────────────────

    /// <summary>
    /// Sets whether a cosigner is required for this document title.
    /// Mirrors VistA rTIU.pas AskCosignerForDocument/Title().
    /// </summary>
    Task SetCosignerRequiredAsync(bool required);

    /// <summary>
    /// Adds an additional signer to the document.
    /// </summary>
    Task AddAdditionalSignerAsync(string signerId);

    // ── GAP 8: ID Notes (Interdisciplinary Notes) ─────────────────────────────

    /// <summary>
    /// Attaches this entry as an ID Note child to a parent document.
    /// Mirrors VistA rTIU.pas AttachEntryToParent().
    /// </summary>
    Task AttachToParentAsync(string parentDocumentId);

    /// <summary>
    /// Detaches this entry from its parent ID Note.
    /// Mirrors VistA rTIU.pas DetachEntryFromParent().
    /// </summary>
    Task DetachFromParentAsync();

    /// <summary>
    /// Registers a child ID Note document under this parent.
    /// Mirrors VistA rTIU.pas CanReceiveAttachment() acceptance path.
    /// </summary>
    Task AddIdNoteChildAsync(string childDocumentId);
}
