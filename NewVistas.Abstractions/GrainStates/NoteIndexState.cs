// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the per-patient note index grain.
/// Stores note metadata sorted by ReferenceDate descending (most recent first).
/// Notes have mutable status (UNSIGNED → COMPLETED, AMENDED, RETRACTED),
/// so entries support AddOrUpdate like the order index.
/// </summary>
[GenerateSerializer]
public class PatientNoteIndexState
{
    /// <summary>
    /// Patient ID this index belongs to
    /// </summary>
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// All note index entries, sorted by ReferenceDate descending (most recent first).
    /// </summary>
    [Id(1)]
    public List<NoteIndexEntry> Entries { get; set; } = new();

    [Id(2)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Lightweight index entry for a TIU document.
/// Stores enough metadata to support filtering by status, document type, and date range
/// without activating the TIU document grain.
/// </summary>
[GenerateSerializer]
public class NoteIndexEntry
{
    /// <summary>
    /// The grain key of the TIU document grain (TIU-{Guid}).
    /// </summary>
    [Id(0)]
    public string DocumentGrainKey { get; set; } = string.Empty;

    /// <summary>
    /// Reference date — for range queries and sorting.
    /// </summary>
    [Id(1)]
    public DateTime ReferenceDate { get; set; }

    /// <summary>
    /// Document type — PROGRESS NOTE, DISCHARGE SUMMARY, CONSULT NOTE, etc.
    /// </summary>
    [Id(2)]
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Current status — UNSIGNED, UNCOSIGNED, COMPLETED, AMENDED, RETRACTED.
    /// Updated when the document status changes.
    /// </summary>
    [Id(3)]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Subject/title — for display without grain activation.
    /// </summary>
    [Id(4)]
    public string? Subject { get; set; }

    /// <summary>
    /// Author name — for display without grain activation.
    /// </summary>
    [Id(5)]
    public string? AuthorName { get; set; }

    /// <summary>
    /// Location name — for display without grain activation.
    /// </summary>
    [Id(6)]
    public string? LocationName { get; set; }

    /// <summary>
    /// Whether this document has addenda attached.
    /// </summary>
    [Id(7)]
    public bool HasAddenda { get; set; }

    /// <summary>
    /// Whether this is an addendum (has a parent document).
    /// Addenda are excluded from top-level note listings.
    /// </summary>
    [Id(8)]
    public bool IsAddendum { get; set; }
}
