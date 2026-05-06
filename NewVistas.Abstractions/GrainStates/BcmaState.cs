// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents the state of a BCMA Administration grain based on VistA BCMA MEDICATION LOG file (#53.79)
/// </summary>
[GenerateSerializer]
public class BcmaState
{
    [Id(0)]
    public string AdministrationId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    [Id(2)]
    public string DrugName { get; set; } = string.Empty;

    [Id(3)]
    public string? DrugId { get; set; }

    [Id(4)]
    public string? Dosage { get; set; }

    [Id(5)]
    public string? Route { get; set; }

    /// <summary>
    /// Action Status — GIVEN, HELD, REFUSED, NOT GIVEN, REMOVED
    /// </summary>
    [Id(6)]
    public string ActionStatus { get; set; } = string.Empty;

    [Id(7)]
    public DateTime? ScheduledDateTime { get; set; }

    [Id(8)]
    public DateTime? AdministrationDateTime { get; set; }

    [Id(9)]
    public string? AdministeredById { get; set; }

    [Id(10)]
    public string? AdministeredByName { get; set; }

    [Id(11)]
    public string? WitnessId { get; set; }

    [Id(12)]
    public string? WitnessName { get; set; }

    [Id(13)]
    public string? InjectionSite { get; set; }

    [Id(14)]
    public string? PrescriptionId { get; set; }

    [Id(15)]
    public string? OrderId { get; set; }

    [Id(16)]
    public string? Comments { get; set; }

    [Id(17)]
    public string? ReasonNotGiven { get; set; }

    /// <summary>
    /// PRN Reason
    /// </summary>
    [Id(18)]
    public string? PrnReason { get; set; }

    /// <summary>
    /// PRN Effectiveness
    /// </summary>
    [Id(19)]
    public string? PrnEffectiveness { get; set; }

    [Id(20)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(21)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
