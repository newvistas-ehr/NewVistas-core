// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

// ─── Enums ────────────────────────────────────────────────────────────────────

/// <summary>ABO blood group system types (VistA BB file #65 field .04).</summary>
[GenerateSerializer]
public enum AboBloodType
{
    Unknown = 0,
    A = 1,
    B = 2,
    AB = 3,
    O = 4
}

/// <summary>Rh (Rhesus) factor — positive or negative.</summary>
[GenerateSerializer]
public enum RhBloodType
{
    Unknown = 0,
    Positive = 1,
    Negative = 2
}

/// <summary>Antibody screen result (VistA BB field .06).</summary>
[GenerateSerializer]
public enum AntibodyScreenResult
{
    NotDone = 0,
    Negative = 1,
    Positive = 2,
    Pending = 3
}

// ─── State ────────────────────────────────────────────────────────────────────

/// <summary>
/// Blood Bank Patient State — the patient's blood bank master record.
/// Maps to VistA BLOOD BANK PATIENT file (#65).
/// </summary>
[GenerateSerializer]
public class BloodBankPatientState
{
    /// <summary>Patient identifier (.01).</summary>
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>ABO blood type (.04) — determined by type &amp; screen.</summary>
    [Id(1)]
    public AboBloodType AboType { get; set; } = AboBloodType.Unknown;

    /// <summary>Rh factor (.05) — positive or negative.</summary>
    [Id(2)]
    public RhBloodType RhType { get; set; } = RhBloodType.Unknown;

    /// <summary>
    /// Most recent antibody screen result (.06).
    /// Negative = no unexpected antibodies found.
    /// Positive = unexpected antibody(ies) present — see AntibodyIdentification.
    /// </summary>
    [Id(3)]
    public AntibodyScreenResult AntibodyScreenResult { get; set; } = AntibodyScreenResult.NotDone;

    /// <summary>Date of most recent antibody screen (.07).</summary>
    [Id(4)]
    public DateTime? AntibodyScreenDate { get; set; }

    /// <summary>
    /// Direct Antibody Test (DAT / direct Coombs) result (.08).
    /// Free text: "Negative", "Positive (IgG)", etc.
    /// </summary>
    [Id(5)]
    public string? DirectAntibodyTest { get; set; }

    /// <summary>
    /// Identified antibodies from a positive screen (.09).
    /// Free text, e.g. "Anti-K, Anti-E".
    /// </summary>
    [Id(6)]
    public string? AntibodyIdentification { get; set; }

    /// <summary>
    /// Special product requirements (.10).
    /// E.g. "Irradiated", "CMV-Negative", "Sickle cell trait negative".
    /// </summary>
    [Id(7)]
    public string? SpecialRequirements { get; set; }

    /// <summary>Cumulative lifetime number of transfusions (.11).</summary>
    [Id(8)]
    public int TransfusionCount { get; set; }

    /// <summary>Date patient was last typed (.12).</summary>
    [Id(9)]
    public DateTime? LastTypedDate { get; set; }

    /// <summary>Free-text clinical notes about this patient's blood bank status.</summary>
    [Id(10)]
    public string? Notes { get; set; }

    /// <summary>Date this record was created.</summary>
    [Id(11)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date this record was last modified.</summary>
    [Id(12)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
