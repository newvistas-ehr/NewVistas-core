// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents an ICD-10-CM diagnosis code entry.
/// Maps to VistA ICD DIAGNOSIS file (#80) with coding system 10D (ICD-10-CM).
///
/// The ICD DIAGNOSIS file (#80) stores both ICD-9-CM and ICD-10-CM codes,
/// distinguished by the CODING SYSTEM field (#1.1) which points to
/// ICD CODING SYSTEMS file (#80.4):
///   Entry 1  = ICD-9-CM  (system code "ICD")
///   Entry 30 = ICD-10-CM (system code "10D")
///
/// Key fields from File #80:
///   .01  - CODE NUMBER (e.g., "A00.0")
///   1.2  - IDENTIFIER / short description
///   67   - VERSIONED DIAGNOSIS (multiple, date-sensitive text)
///   66   - STATUS (multiple, activation/inactivation history)
///   1.1  - CODING SYSTEM pointer to #80.4
///   1.3  - UNACCEPTABLE AS PRINCIPAL DX
///   1.7  - ICD EXPANDED (long description)
/// </summary>
[GenerateSerializer]
public class Icd10State
{
    /// <summary>
    /// IEN in File #80. For ICD-10-CM codes this is auto-assigned.
    /// Used as the grain key.
    /// </summary>
    [Id(0)]
    public string Ien { get; set; } = string.Empty;

    /// <summary>
    /// ICD-10-CM code (File #80 field .01).
    /// e.g., "A00.0", "M54.5", "Z87.39"
    /// Stored without dot in the CMS order file; stored with dot in VistA.
    /// </summary>
    [Id(1)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Short description / Identifier (File #80 field 1.2).
    /// Limited to 60 characters in the CMS order file.
    /// </summary>
    [Id(2)]
    public string ShortDescription { get; set; } = string.Empty;

    /// <summary>
    /// Long description / ICD Expanded (File #80 field 1.7).
    /// Full clinical description of the code.
    /// </summary>
    [Id(3)]
    public string LongDescription { get; set; } = string.Empty;

    /// <summary>
    /// Whether this code is billable (1) or a header/category code (0).
    /// In VistA terms, header codes are not valid for filing on a patient record.
    /// Only billable codes can be used in the DIAGNOSIS field (.01) of
    /// PROBLEM LIST file (#9000011).
    /// </summary>
    [Id(4)]
    public bool IsBillable { get; set; }

    /// <summary>
    /// Coding system identifier. For ICD-10-CM this is "10D"
    /// matching ICD CODING SYSTEMS file (#80.4) entry 30.
    /// </summary>
    [Id(5)]
    public string CodingSystem { get; set; } = "10D";

    /// <summary>
    /// Whether this code is currently active.
    /// Maps to the STATUS multiple (66) in File #80.
    /// </summary>
    [Id(6)]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Order number from the CMS ICD-10-CM order file.
    /// Used for canonical ordering of codes.
    /// </summary>
    [Id(7)]
    public int OrderNumber { get; set; }

    /// <summary>
    /// Chapter/category grouping derived from the first character(s) of the code.
    /// e.g., "A00-B99" = Certain infectious and parasitic diseases.
    /// </summary>
    [Id(8)]
    public string? Chapter { get; set; }

    [Id(9)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(10)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
