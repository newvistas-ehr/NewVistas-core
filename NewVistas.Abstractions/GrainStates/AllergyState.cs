// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents a single allergy entry embedded in the patient grain.
/// Based on VistA PATIENT ALLERGIES file (#120.8).
/// No PatientId — the patient grain owns this data.
/// </summary>
[GenerateSerializer]
public class AllergyEntry
{
    /// <summary>
    /// Allergy Internal Entry Number (IEN)
    /// </summary>
    [Id(0)]
    public string AllergyId { get; set; } = string.Empty;

    /// <summary>
    /// Allergen Name (e.g., Penicillin, Peanuts, Latex)
    /// </summary>
    [Id(1)]
    public string Allergen { get; set; } = string.Empty;

    /// <summary>
    /// Allergen Type (e.g., Drug, Food, Other)
    /// </summary>
    [Id(2)]
    public string AllergenType { get; set; } = string.Empty;

    /// <summary>
    /// GMR Allergy - Reference to ALLERGEN file
    /// </summary>
    [Id(3)]
    public string? AllergenId { get; set; }

    /// <summary>
    /// Reaction Type (e.g., ALLERGY, ADVERSE REACTION, PHARMACOLOGIC)
    /// </summary>
    [Id(4)]
    public string ReactionType { get; set; } = "ALLERGY";

    /// <summary>
    /// List of Reactions/Symptoms (e.g., Rash, Itching, Anaphylaxis, Nausea)
    /// </summary>
    [Id(5)]
    public List<string> Reactions { get; set; } = new();

    /// <summary>
    /// Severity (e.g., Mild, Moderate, Severe)
    /// </summary>
    [Id(6)]
    public string? Severity { get; set; }

    /// <summary>
    /// Date/Time of Reaction
    /// </summary>
    [Id(7)]
    public DateTime? ReactionDateTime { get; set; }

    /// <summary>
    /// Onset Date (when the allergy was first identified)
    /// </summary>
    [Id(8)]
    public DateTime? OnsetDate { get; set; }

    /// <summary>
    /// Comments/Additional Information
    /// </summary>
    [Id(9)]
    public string? Comments { get; set; }

    /// <summary>
    /// Observed/Historical (O = Observed, H = Historical)
    /// </summary>
    [Id(10)]
    public string? ObservedHistorical { get; set; }

    /// <summary>
    /// Originator IEN - User who entered the allergy
    /// </summary>
    [Id(11)]
    public string? OriginatorId { get; set; }

    /// <summary>
    /// Originator Name
    /// </summary>
    [Id(12)]
    public string? OriginatorName { get; set; }

    /// <summary>
    /// Origination Date/Time
    /// </summary>
    [Id(13)]
    public DateTime OriginationDateTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Verified Flag
    /// </summary>
    [Id(14)]
    public bool IsVerified { get; set; }

    /// <summary>
    /// Verified By IEN
    /// </summary>
    [Id(15)]
    public string? VerifiedById { get; set; }

    /// <summary>
    /// Verified By Name
    /// </summary>
    [Id(16)]
    public string? VerifiedByName { get; set; }

    /// <summary>
    /// Verified Date/Time
    /// </summary>
    [Id(17)]
    public DateTime? VerifiedDateTime { get; set; }

    /// <summary>
    /// Chart Marked Flag (indicates if allergy is noted on patient chart)
    /// </summary>
    [Id(18)]
    public bool IsChartMarked { get; set; }

    /// <summary>
    /// Entered in Error Flag
    /// </summary>
    [Id(19)]
    public bool IsEnteredInError { get; set; }

    /// <summary>
    /// Error Entry Reason
    /// </summary>
    [Id(20)]
    public string? ErrorReason { get; set; }

    /// <summary>
    /// Date Record Created
    /// </summary>
    [Id(21)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date Record Last Modified
    /// </summary>
    [Id(22)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Deep copy. Used at event/state boundaries so that mutating the live
    /// allergy on the patient grain does not retroactively mutate the
    /// historical snapshot stored on a clinical event payload (which would
    /// break the hash chain).
    /// </summary>
    public AllergyEntry Clone() => new()
    {
        AllergyId = AllergyId,
        Allergen = Allergen,
        AllergenType = AllergenType,
        AllergenId = AllergenId,
        ReactionType = ReactionType,
        Reactions = new List<string>(Reactions),
        Severity = Severity,
        ReactionDateTime = ReactionDateTime,
        OnsetDate = OnsetDate,
        Comments = Comments,
        ObservedHistorical = ObservedHistorical,
        OriginatorId = OriginatorId,
        OriginatorName = OriginatorName,
        OriginationDateTime = OriginationDateTime,
        IsVerified = IsVerified,
        VerifiedById = VerifiedById,
        VerifiedByName = VerifiedByName,
        VerifiedDateTime = VerifiedDateTime,
        IsChartMarked = IsChartMarked,
        IsEnteredInError = IsEnteredInError,
        ErrorReason = ErrorReason,
        CreatedDate = CreatedDate,
        LastModifiedDate = LastModifiedDate
    };
}
