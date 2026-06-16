// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents a single immunization entry embedded in the patient grain.
/// Based on VistA V IMMUNIZATION file (#9000010.11).
/// No PatientId — the patient grain owns this data.
/// </summary>
[GenerateSerializer]
public class ImmunizationEntry
{
    [Id(0)]
    public string ImmunizationId { get; set; } = string.Empty;

    /// <summary>
    /// Immunization (.01) � pointer to IMMUNIZATION file (#9999999.14)
    /// </summary>
    [Id(2)]
    public string ImmunizationName { get; set; } = string.Empty;

    [Id(3)]
    public string? ImmunizationDefId { get; set; }

    /// <summary>
    /// CVX Code � CDC vaccine code
    /// </summary>
    [Id(4)]
    public string? CvxCode { get; set; }

    /// <summary>
    /// Event Date and Time (.01)
    /// </summary>
    [Id(5)]
    public DateTime EventDateTime { get; set; }

    /// <summary>
    /// Series � dose number in series
    /// </summary>
    [Id(6)]
    public string? Series { get; set; }

    [Id(7)]
    public string? LotNumber { get; set; }

    [Id(8)]
    public string? Manufacturer { get; set; }

    [Id(9)]
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Administered By
    /// </summary>
    [Id(10)]
    public string? AdministeredById { get; set; }

    [Id(11)]
    public string? AdministeredByName { get; set; }

    /// <summary>
    /// Ordering Provider
    /// </summary>
    [Id(12)]
    public string? OrderingProviderId { get; set; }

    [Id(13)]
    public string? OrderingProviderName { get; set; }

    /// <summary>
    /// Administration Site � LEFT DELTOID, RIGHT DELTOID, LEFT THIGH, etc.
    /// </summary>
    [Id(14)]
    public string? AdministrationSite { get; set; }

    /// <summary>
    /// Route � INTRAMUSCULAR, SUBCUTANEOUS, INTRADERMAL, ORAL, NASAL, etc.
    /// </summary>
    [Id(15)]
    public string? Route { get; set; }

    [Id(16)]
    public string? Dose { get; set; }

    [Id(17)]
    public string? Units { get; set; }

    /// <summary>
    /// Reaction � any adverse reaction noted
    /// </summary>
    [Id(18)]
    public string? Reaction { get; set; }

    /// <summary>
    /// Contraindicated � YES or NO
    /// </summary>
    [Id(19)]
    public bool IsContraindicated { get; set; }

    [Id(20)]
    public string? ContraindicationReason { get; set; }

    [Id(21)]
    public string? LocationId { get; set; }

    [Id(22)]
    public string? LocationName { get; set; }

    /// <summary>
    /// VIS (Vaccine Info Statement) date offered
    /// </summary>
    [Id(23)]
    public DateTime? VisDateOffered { get; set; }

    [Id(24)]
    public string? Comments { get; set; }

    /// <summary>
    /// Dose number in series (1, 2, 3, etc.)
    /// </summary>
    [Id(25)]
    public int? DoseNumber { get; set; }

    /// <summary>
    /// Total doses required in the series
    /// </summary>
    [Id(26)]
    public int? DosesInSeries { get; set; }

    /// <summary>
    /// Whether the vaccination series is complete
    /// </summary>
    [Id(27)]
    public bool SeriesComplete { get; set; }

    /// <summary>
    /// VIS (Vaccine Information Statement) date published
    /// </summary>
    [Id(28)]
    public DateTime? VisDatePublished { get; set; }

    /// <summary>
    /// Lot expiration date
    /// </summary>
    [Id(29)]
    public string? LotExpirationDate { get; set; }

    /// <summary>
    /// Information Source — ADMINISTERED or HISTORICAL
    /// </summary>
    [Id(30)]
    public string? InformationSource { get; set; }

    /// <summary>
    /// Vaccine Group Name — e.g., COVID-19, INFLUENZA, HEPATITIS B
    /// </summary>
    [Id(31)]
    public string? VaccineGroupName { get; set; }

    /// <summary>
    /// CVX group code
    /// </summary>
    [Id(32)]
    public string? VaccineGroupCode { get; set; }

    /// <summary>
    /// Manufacturer Name — e.g., PFIZER, MODERNA
    /// </summary>
    [Id(33)]
    public string? ManufacturerName { get; set; }

    /// <summary>
    /// MVX manufacturer code
    /// </summary>
    [Id(34)]
    public string? ManufacturerCode { get; set; }

    /// <summary>
    /// Registry Status — REPORTED, PENDING, ERROR
    /// </summary>
    [Id(35)]
    public string? RegistryStatus { get; set; }

    /// <summary>
    /// Timestamped comments
    /// </summary>
    [Id(36)]
    public List<ImmunizationComment> ImmunizationComments { get; set; } = new();

    [Id(37)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(38)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A timestamped comment on an immunization record
/// </summary>
[GenerateSerializer]
public class ImmunizationComment
{
    /// <summary>
    /// Date/time the comment was recorded
    /// </summary>
    [Id(0)]
    public DateTime CommentDateTime { get; set; }

    /// <summary>
    /// Author of the comment
    /// </summary>
    [Id(1)]
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>
    /// Comment text
    /// </summary>
    [Id(2)]
    public string CommentText { get; set; } = string.Empty;
}
