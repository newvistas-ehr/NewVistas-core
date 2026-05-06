// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

// ─── Enums ────────────────────────────────────────────────────────────────────

/// <summary>Transfusion administration status.</summary>
[GenerateSerializer]
public enum TransfusionStatus
{
    InProgress = 0,
    Completed = 1,
    Stopped = 2,
    Reaction = 3
}

/// <summary>
/// Transfusion reaction type (VistA BB TRANSFUSION REACTION file #65.02).
/// None = transfusion completed without adverse event.
/// </summary>
[GenerateSerializer]
public enum TransfusionReactionType
{
    None = 0,
    Febrile = 1,
    Allergic = 2,
    AcuteHemolytic = 3,
    DelayedHemolytic = 4,
    Anaphylactic = 5,
    TRALI = 6,
    TACO = 7,
    SepticBacterial = 8,
    Other = 9
}

// ─── Index entry ──────────────────────────────────────────────────────────────

/// <summary>Lightweight per-patient transfusion index entry.</summary>
[GenerateSerializer]
public class TransfusionIndexEntry
{
    [Id(0)]
    public string TransfusionId { get; set; } = string.Empty;

    [Id(1)]
    public string UnitId { get; set; } = string.Empty;

    [Id(2)]
    public string ProductType { get; set; } = string.Empty;

    [Id(3)]
    public string AboType { get; set; } = string.Empty;

    [Id(4)]
    public string RhType { get; set; } = string.Empty;

    [Id(5)]
    public DateTime StartDateTime { get; set; }

    [Id(6)]
    public DateTime? EndDateTime { get; set; }

    [Id(7)]
    public TransfusionStatus Status { get; set; }

    [Id(8)]
    public TransfusionReactionType ReactionType { get; set; }
}

// ─── State ────────────────────────────────────────────────────────────────────

/// <summary>
/// Transfusion State — a single blood product transfusion administration record.
/// Maps to VistA BLOOD BANK TRANSFUSION file (#65.01).
/// </summary>
[GenerateSerializer]
public class TransfusionState
{
    /// <summary>Unique transfusion identifier (.01).</summary>
    [Id(0)]
    public string TransfusionId { get; set; } = string.Empty;

    /// <summary>Patient receiving the transfusion (.02).</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Blood unit being transfused (.03).</summary>
    [Id(2)]
    public string UnitId { get; set; } = string.Empty;

    /// <summary>Crossmatch record that approved this transfusion (.04).</summary>
    [Id(3)]
    public string? CrossmatchId { get; set; }

    /// <summary>Blood product type description (.05), e.g. "PackedRBC".</summary>
    [Id(4)]
    public string ProductType { get; set; } = string.Empty;

    /// <summary>ABO type of the transfused unit (.06).</summary>
    [Id(5)]
    public string AboType { get; set; } = string.Empty;

    /// <summary>Rh type of the transfused unit (.07).</summary>
    [Id(6)]
    public string RhType { get; set; } = string.Empty;

    /// <summary>Date/time transfusion was started (.08).</summary>
    [Id(7)]
    public DateTime StartDateTime { get; set; }

    /// <summary>Date/time transfusion was ended (completed or stopped) (.09).</summary>
    [Id(8)]
    public DateTime? EndDateTime { get; set; }

    /// <summary>Administration status (.10).</summary>
    [Id(9)]
    public TransfusionStatus Status { get; set; } = TransfusionStatus.InProgress;

    /// <summary>Volume transfused in millilitres (.11).</summary>
    [Id(10)]
    public decimal? VolumeML { get; set; }

    /// <summary>UserId of the nurse/tech administering the transfusion (.12).</summary>
    [Id(11)]
    public string AdministeredByUserId { get; set; } = string.Empty;

    /// <summary>Name of the administering clinician (.13).</summary>
    [Id(12)]
    public string AdministeredByUserName { get; set; } = string.Empty;

    /// <summary>UserId of the ordering provider (.14).</summary>
    [Id(13)]
    public string OrderedByUserId { get; set; } = string.Empty;

    /// <summary>Name of the ordering provider (.15).</summary>
    [Id(14)]
    public string OrderedByUserName { get; set; } = string.Empty;

    /// <summary>IV infusion site description (.16), e.g. "Right antecubital".</summary>
    [Id(15)]
    public string? InfusionSite { get; set; }

    /// <summary>Pre-transfusion vital signs, free text (.17).</summary>
    [Id(16)]
    public string? PreTransfusionVitals { get; set; }

    /// <summary>Post-transfusion vital signs, free text (.18).</summary>
    [Id(17)]
    public string? PostTransfusionVitals { get; set; }

    /// <summary>Adverse reaction type (.19).</summary>
    [Id(18)]
    public TransfusionReactionType ReactionType { get; set; } = TransfusionReactionType.None;

    /// <summary>Clinical notes about the reaction (.20).</summary>
    [Id(19)]
    public string? ReactionNotes { get; set; }

    /// <summary>Reason a transfusion was stopped before completion (.21).</summary>
    [Id(20)]
    public string? StopReason { get; set; }

    /// <summary>Free-text clinical notes about this transfusion.</summary>
    [Id(21)]
    public string? Notes { get; set; }

    /// <summary>Date this record was created.</summary>
    [Id(22)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date this record was last modified.</summary>
    [Id(23)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
