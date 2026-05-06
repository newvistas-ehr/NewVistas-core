// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

// ─── Enums ────────────────────────────────────────────────────────────────────

/// <summary>Type of blind rehabilitation center (VistA File #782.1 field .04).</summary>
[GenerateSerializer]
public enum BRCenterType
{
    /// <summary>Comprehensive Blind Rehabilitation Center — full inpatient program.</summary>
    Comprehensive = 0,
    /// <summary>Visual Impairment Services Team (VIST) — outpatient coordination.</summary>
    Vist = 1,
    /// <summary>Advanced Low Vision Clinic — specialized optical/technology services.</summary>
    AdvancedLowVision = 2,
    /// <summary>Blind Rehabilitation Outpatient Specialist (BROS) clinic.</summary>
    Bros = 3
}

// ─── Supporting Record ────────────────────────────────────────────────────────

/// <summary>Lightweight entry in the BR Center Index.</summary>
[GenerateSerializer]
public class BRCenterIndexEntry
{
    /// <summary>Unique identifier for the center.</summary>
    [Id(0)]
    public string CenterId { get; set; } = string.Empty;

    /// <summary>Display name of the center.</summary>
    [Id(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>City location.</summary>
    [Id(2)]
    public string City { get; set; } = string.Empty;

    /// <summary>State abbreviation.</summary>
    [Id(3)]
    public string State { get; set; } = string.Empty;

    /// <summary>Type of center.</summary>
    [Id(4)]
    public BRCenterType CenterType { get; set; }

    /// <summary>Whether the center is currently accepting new patients.</summary>
    [Id(5)]
    public bool AcceptingPatients { get; set; }
}

// ─── State ────────────────────────────────────────────────────────────────────

/// <summary>
/// Blind Rehabilitation Training Center State.
/// Maps to VistA BLIND REHABILITATION CENTER file (#782.1).
/// </summary>
[GenerateSerializer]
public class BRCenterState
{
    /// <summary>Unique identifier for this center (.01).</summary>
    [Id(0)]
    public string CenterId { get; set; } = string.Empty;

    /// <summary>Full name of the blind rehabilitation center (.02).</summary>
    [Id(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>VA facility code (.03).</summary>
    [Id(2)]
    public string FacilityCode { get; set; } = string.Empty;

    /// <summary>City where the center is located (.04).</summary>
    [Id(3)]
    public string City { get; set; } = string.Empty;

    /// <summary>State abbreviation (.05).</summary>
    [Id(4)]
    public string State { get; set; } = string.Empty;

    /// <summary>Type of blind rehabilitation center (.06).</summary>
    [Id(5)]
    public BRCenterType CenterType { get; set; } = BRCenterType.Comprehensive;

    /// <summary>Maximum number of inpatient beds available (.07).</summary>
    [Id(6)]
    public int BedCapacity { get; set; }

    /// <summary>Whether the center is currently accepting new patient referrals (.08).</summary>
    [Id(7)]
    public bool AcceptingPatients { get; set; } = true;

    /// <summary>Training program areas offered at this center (.09).</summary>
    [Id(8)]
    public List<BRTrainingArea> ProgramsOffered { get; set; } = new();

    /// <summary>Main contact phone number (.10).</summary>
    [Id(9)]
    public string? PhoneNumber { get; set; }

    /// <summary>Primary contact person name (.11).</summary>
    [Id(10)]
    public string? ContactName { get; set; }

    /// <summary>Additional notes about the center (.12).</summary>
    [Id(11)]
    public string? Notes { get; set; }

    /// <summary>Date the center record was created.</summary>
    [Id(12)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date the center record was last modified.</summary>
    [Id(13)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

// ─── Index State ─────────────────────────────────────────────────────────────

/// <summary>
/// Blind Rehabilitation Center Index State — singleton index of all BR centers.
/// </summary>
[GenerateSerializer]
public class BRCenterIndexState
{
    /// <summary>All registered blind rehabilitation centers.</summary>
    [Id(0)]
    public List<BRCenterIndexEntry> Centers { get; set; } = new();

    /// <summary>Whether the default VA centers have been seeded.</summary>
    [Id(1)]
    public bool Seeded { get; set; }
}
