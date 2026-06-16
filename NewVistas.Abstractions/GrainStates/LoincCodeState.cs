// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents a LOINC (Logical Observation Identifiers Names and Codes) entry.
/// LOINC is maintained by the Regenstrief Institute and is the standard
/// for identifying laboratory tests and clinical observations.
///
/// In VistA, LOINC codes map to:
///   - WKLD CODE field in LABORATORY TEST file (#60)
///   - Lab result standardization for HL7 messaging
///   - Health Level Seven (HL7) OBX segments
///
/// LOINC axis structure:
///   Component (analyte) + Property + Time + System + Scale + Method
///   e.g., "Glucose:MCnc:Pt:Ser/Plas:Qn" = Glucose mass concentration,
///         point in time, serum/plasma, quantitative
/// </summary>
[GenerateSerializer]
public class LoincCodeState
{
    /// <summary>
    /// LOINC code number (e.g., "2345-7", "718-7").
    /// Includes the check digit after the hyphen.
    /// </summary>
    [Id(0)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Component (analyte) — what is measured.
    /// e.g., "Glucose", "Hemoglobin", "Creatinine"
    /// </summary>
    [Id(1)]
    public string Component { get; set; } = string.Empty;

    /// <summary>
    /// Property of the measurement.
    /// e.g., "MCnc" (mass concentration), "NCnc" (number concentration)
    /// </summary>
    [Id(2)]
    public string Property { get; set; } = string.Empty;

    /// <summary>
    /// Time aspect of the measurement.
    /// e.g., "Pt" (point in time), "24H" (24-hour collection)
    /// </summary>
    [Id(3)]
    public string TimeAspect { get; set; } = string.Empty;

    /// <summary>
    /// System (specimen type).
    /// e.g., "Ser/Plas" (serum/plasma), "Bld" (blood), "Urine"
    /// </summary>
    [Id(4)]
    public string System { get; set; } = string.Empty;

    /// <summary>
    /// Scale of measurement.
    /// e.g., "Qn" (quantitative), "Ord" (ordinal), "Nom" (nominal)
    /// </summary>
    [Id(5)]
    public string Scale { get; set; } = string.Empty;

    /// <summary>
    /// Method of measurement (may be empty).
    /// e.g., "Automated count", "Enzymatic"
    /// </summary>
    [Id(6)]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Short name for display.
    /// e.g., "Glucose SerPl-mCnc"
    /// </summary>
    [Id(7)]
    public string ShortName { get; set; } = string.Empty;

    /// <summary>
    /// Long common name — human-readable description.
    /// e.g., "Glucose [Mass/volume] in Serum or Plasma"
    /// </summary>
    [Id(8)]
    public string LongCommonName { get; set; } = string.Empty;

    /// <summary>
    /// Code status: ACTIVE, TRIAL, DISCOURAGED, DEPRECATED.
    /// </summary>
    [Id(9)]
    public string Status { get; set; } = "ACTIVE";

    /// <summary>
    /// Class type categorization.
    /// e.g., "1" (Laboratory class), "2" (Clinical class)
    /// </summary>
    [Id(10)]
    public string ClassType { get; set; } = string.Empty;

    /// <summary>
    /// Record creation timestamp.
    /// </summary>
    [Id(11)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last modification timestamp.
    /// </summary>
    [Id(12)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
