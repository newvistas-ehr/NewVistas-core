// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.Helpers;

/// <summary>
/// Represents a single parsed ZWR (MUMPS global export) line.
/// Format: ^GLOBAL(subscript1,subscript2,...)="value"
/// </summary>
public class ZwrRecord
{
    /// <summary>
    /// The global name (e.g., "DPT", "GMR", "OR", "LR", "TIU", "SRF")
    /// </summary>
    public string Global { get; set; } = string.Empty;

    /// <summary>
    /// The file number extracted from compound globals (e.g., "120.8" from ^GMR(120.8,...)).
    /// Null for simple globals like ^DPT or ^SRF.
    /// </summary>
    public string? FileNumber { get; set; }

    /// <summary>
    /// The primary IEN (Internal Entry Number) — first numeric subscript after any file number.
    /// </summary>
    public long Ien { get; set; }

    /// <summary>
    /// Remaining subscripts after the IEN (e.g., ["0"], [".11"], ["CH","7250101","1"]).
    /// </summary>
    public List<string> Subscripts { get; set; } = new();

    /// <summary>
    /// The raw value string (right side of the = sign, with outer quotes stripped).
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Piece-delimited values from Value split on ^ (VistA delimiter).
    /// </summary>
    public string[] Pieces => Value.Split('^');
}
