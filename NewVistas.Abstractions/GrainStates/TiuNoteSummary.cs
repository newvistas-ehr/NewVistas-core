// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Summary of a TIU document for cover sheet display and note listing.
/// Derived from TIUSRVL.m (list notes for patient).
/// </summary>
[GenerateSerializer]
public class TiuNoteSummary
{
    [Id(0)]
    public string DocumentId { get; set; } = string.Empty;

    [Id(1)]
    public string DocumentType { get; set; } = string.Empty;

    [Id(2)]
    public string? Subject { get; set; }

    [Id(3)]
    public string? AuthorName { get; set; }

    [Id(4)]
    public string Status { get; set; } = string.Empty;

    [Id(5)]
    public DateTime ReferenceDate { get; set; }

    [Id(6)]
    public string? LocationName { get; set; }

    [Id(7)]
    public bool HasAddenda { get; set; }
}
