// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

// ─── Enums ────────────────────────────────────────────────────────────────────

/// <summary>Blood product types (VistA BB component codes).</summary>
[GenerateSerializer]
public enum BloodProductType
{
    WholeBlood = 0,
    PackedRBC = 1,
    FreshFrozenPlasma = 2,
    Platelets = 3,
    Cryoprecipitate = 4,
    Granulocytes = 5,
    Albumin = 6,
    IVIg = 7,
    Other = 8
}

/// <summary>Lifecycle status of a blood unit in inventory.</summary>
[GenerateSerializer]
public enum BloodUnitStatus
{
    Available = 0,
    Reserved = 1,
    Transfused = 2,
    Discarded = 3,
    Quarantine = 4,
    Expired = 5
}

// ─── Index entry ──────────────────────────────────────────────────────────────

/// <summary>Lightweight inventory index entry for a blood unit.</summary>
[GenerateSerializer]
public class BloodUnitIndexEntry
{
    [Id(0)]
    public string UnitId { get; set; } = string.Empty;

    [Id(1)]
    public BloodProductType ProductType { get; set; }

    [Id(2)]
    public AboBloodType AboType { get; set; }

    [Id(3)]
    public RhBloodType RhType { get; set; }

    [Id(4)]
    public BloodUnitStatus Status { get; set; }

    [Id(5)]
    public DateTime ExpirationDate { get; set; }

    [Id(6)]
    public bool IsIrradiated { get; set; }

    [Id(7)]
    public bool IsLeukoreduced { get; set; }

    [Id(8)]
    public bool IsAntigenNegative { get; set; }

    [Id(9)]
    public string? AntigenNegativeFor { get; set; }

    [Id(10)]
    public string? ReservedForPatientId { get; set; }
}

// ─── State ────────────────────────────────────────────────────────────────────

/// <summary>
/// Blood Unit State — a single blood product unit in the blood bank inventory.
/// Maps to VistA BLOOD BANK file (#65.04).
/// </summary>
[GenerateSerializer]
public class BloodUnitState
{
    /// <summary>Unique unit identifier / donation number (.01).</summary>
    [Id(0)]
    public string UnitId { get; set; } = string.Empty;

    /// <summary>Blood product component type (.02).</summary>
    [Id(1)]
    public BloodProductType ProductType { get; set; }

    /// <summary>ABO type of the donated unit (.03).</summary>
    [Id(2)]
    public AboBloodType AboType { get; set; }

    /// <summary>Rh factor of the donated unit (.04).</summary>
    [Id(3)]
    public RhBloodType RhType { get; set; }

    /// <summary>Date blood was collected (.05).</summary>
    [Id(4)]
    public DateTime CollectionDate { get; set; }

    /// <summary>Date the unit expires (.06).</summary>
    [Id(5)]
    public DateTime ExpirationDate { get; set; }

    /// <summary>Current lifecycle status (.07).</summary>
    [Id(6)]
    public BloodUnitStatus Status { get; set; } = BloodUnitStatus.Available;

    /// <summary>Collecting or supplying facility (.08).</summary>
    [Id(7)]
    public string? SourceFacility { get; set; }

    /// <summary>Donor identifier for autologous units (.09).</summary>
    [Id(8)]
    public string? DonorId { get; set; }

    /// <summary>FDA product code / ISBT 128 product code (.10).</summary>
    [Id(9)]
    public string? ProductCode { get; set; }

    /// <summary>Volume of the unit in millilitres (.11).</summary>
    [Id(10)]
    public decimal? VolumeML { get; set; }

    /// <summary>True if the unit has been irradiated (.12).</summary>
    [Id(11)]
    public bool IsIrradiated { get; set; }

    /// <summary>True if the unit is leukoreduced (.13).</summary>
    [Id(12)]
    public bool IsLeukoreduced { get; set; }

    /// <summary>True if the unit has been washed (.14).</summary>
    [Id(13)]
    public bool IsWashed { get; set; }

    /// <summary>True if the unit is antigen-negative for specific antigen(s) (.15).</summary>
    [Id(14)]
    public bool IsAntigenNegative { get; set; }

    /// <summary>Antigen(s) confirmed negative for, e.g. "K, E, c" (.16).</summary>
    [Id(15)]
    public string? AntigenNegativeFor { get; set; }

    /// <summary>PatientId this unit is currently reserved for (.17).</summary>
    [Id(16)]
    public string? ReservedForPatientId { get; set; }

    /// <summary>CrossmatchId that triggered the reservation (.18).</summary>
    [Id(17)]
    public string? ReservedForCrossmatchId { get; set; }

    /// <summary>PatientId the unit was ultimately transfused to (.19).</summary>
    [Id(18)]
    public string? TransfusedToPatientId { get; set; }

    /// <summary>TransfusionId that consumed this unit (.20).</summary>
    [Id(19)]
    public string? TransfusionId { get; set; }

    /// <summary>Date/time the unit was transfused (.21).</summary>
    [Id(20)]
    public DateTime? TransfusedDate { get; set; }

    /// <summary>Reason for disposal when Status = Discarded (.22).</summary>
    [Id(21)]
    public string? DisposalReason { get; set; }

    /// <summary>Free-text notes about this unit.</summary>
    [Id(22)]
    public string? Notes { get; set; }

    /// <summary>Date this record was created.</summary>
    [Id(23)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date this record was last modified.</summary>
    [Id(24)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
