// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents the state of an Automated Lab Instrument grain based on VistA
/// AUTO INSTRUMENT file (#62.4) and LA7 MESSAGE PARAMETER file (#62.48).
/// Each grain represents one physical lab analyzer or point-of-care device.
/// </summary>
[GenerateSerializer]
public class AutoInstrumentState
{
    /// <summary>
    /// Instrument Internal Entry Number — unique identifier (.01)
    /// </summary>
    [Id(0)]
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>
    /// Instrument Name (.01) — e.g., "Beckman AU5800", "Sysmex XN-1000"
    /// </summary>
    [Id(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Instrument Type — UNIVERSAL (1), POINT_OF_CARE (20-29), SHIPPING (30)
    /// Maps to VistA interface type codes in file #62.48
    /// </summary>
    [Id(2)]
    public InstrumentType InstrumentType { get; set; } = InstrumentType.UNIVERSAL;

    /// <summary>
    /// Active status — mirrors VistA STATUS field in #62.48
    /// </summary>
    [Id(3)]
    public InstrumentStatus Status { get; set; } = InstrumentStatus.INACTIVE;

    /// <summary>
    /// HL7 version used for message exchange (e.g., "2.3", "2.4", "2.5.1")
    /// </summary>
    [Id(4)]
    public string HL7Version { get; set; } = "2.5.1";

    /// <summary>
    /// TCP/IP host address for instrument connection
    /// </summary>
    [Id(5)]
    public string TcpHost { get; set; } = string.Empty;

    /// <summary>
    /// TCP/IP port for instrument connection
    /// </summary>
    [Id(6)]
    public int TcpPort { get; set; }

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    [Id(7)]
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Auto-Download flag — when true, orders are automatically sent to instrument.
    /// Mirrors VistA AUTO DOWNLOAD field (#62.4, field 98)
    /// </summary>
    [Id(8)]
    public bool AutoDownload { get; set; }

    /// <summary>
    /// Auto-Release flag — when true, results within normal range are auto-verified.
    /// Mirrors VistA AUTO RELEASE field (#62.4, field 99)
    /// </summary>
    [Id(9)]
    public bool AutoRelease { get; set; }

    /// <summary>
    /// Reference to LA7 MESSAGE PARAMETER configuration (#62.48 IEN)
    /// </summary>
    [Id(10)]
    public string? MessageParameterId { get; set; }

    /// <summary>
    /// Reference to LOAD/WORK LIST (#68.2 IEN) — defines which tests this instrument processes
    /// </summary>
    [Id(11)]
    public string? LoadWorkListId { get; set; }

    /// <summary>
    /// Manufacturer name (e.g., "Beckman Coulter", "Sysmex", "Abbott")
    /// </summary>
    [Id(12)]
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Model number
    /// </summary>
    [Id(13)]
    public string? Model { get; set; }

    /// <summary>
    /// Serial number for the physical device
    /// </summary>
    [Id(14)]
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Lab section/division this instrument belongs to (e.g., "CHEMISTRY", "HEMATOLOGY")
    /// </summary>
    [Id(15)]
    public string? LabSection { get; set; }

    /// <summary>
    /// Physical location of the instrument
    /// </summary>
    [Id(16)]
    public string? Location { get; set; }

    /// <summary>
    /// Sending application identifier for HL7 MSH segment
    /// </summary>
    [Id(17)]
    public string? SendingApplication { get; set; }

    /// <summary>
    /// Sending facility identifier for HL7 MSH segment
    /// </summary>
    [Id(18)]
    public string? SendingFacility { get; set; }

    /// <summary>
    /// Test mappings — maps external instrument codes to internal LOINC/NLT codes.
    /// Mirrors VistA AUTO INSTRUMENT subfile #62.41 (orderable/reportable tests)
    /// </summary>
    [Id(19)]
    public List<InstrumentTestMapping> TestMappings { get; set; } = new();

    /// <summary>
    /// Total messages processed by this instrument
    /// </summary>
    [Id(20)]
    public long TotalMessagesProcessed { get; set; }

    /// <summary>
    /// Total errors encountered
    /// </summary>
    [Id(21)]
    public long TotalErrors { get; set; }

    /// <summary>
    /// Last message received timestamp
    /// </summary>
    [Id(22)]
    public DateTime? LastMessageReceivedDate { get; set; }

    /// <summary>
    /// Last successful connection timestamp
    /// </summary>
    [Id(23)]
    public DateTime? LastConnectionDate { get; set; }

    /// <summary>
    /// Date Record Created
    /// </summary>
    [Id(24)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date Record Last Modified
    /// </summary>
    [Id(25)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Maps an external instrument test code to an internal LOINC/NLT code.
/// Mirrors VistA AUTO INSTRUMENT subfile #62.41 orderable/reportable test entries.
/// </summary>
[GenerateSerializer]
public class InstrumentTestMapping
{
    /// <summary>
    /// External test code used by the instrument (NLT or proprietary)
    /// </summary>
    [Id(0)]
    public string ExternalTestCode { get; set; } = string.Empty;

    /// <summary>
    /// Internal test identifier — LOINC code
    /// </summary>
    [Id(1)]
    public string InternalLoincCode { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable test name
    /// </summary>
    [Id(2)]
    public string TestName { get; set; } = string.Empty;

    /// <summary>
    /// Specimen type expected (e.g., "Blood", "Serum", "Urine")
    /// </summary>
    [Id(3)]
    public string? Specimen { get; set; }

    /// <summary>
    /// Number of decimal places for result values
    /// </summary>
    [Id(4)]
    public int ResultDecimalPlaces { get; set; } = 2;

    /// <summary>
    /// Result units (e.g., "mg/dL", "K/cmm")
    /// </summary>
    [Id(5)]
    public string? ResultUnits { get; set; }

    /// <summary>
    /// Normal range low value — used for auto-verify
    /// </summary>
    [Id(6)]
    public double? NormalRangeLow { get; set; }

    /// <summary>
    /// Normal range high value — used for auto-verify
    /// </summary>
    [Id(7)]
    public double? NormalRangeHigh { get; set; }

    /// <summary>
    /// Critical low value — triggers critical alert
    /// </summary>
    [Id(8)]
    public double? CriticalLow { get; set; }

    /// <summary>
    /// Critical high value — triggers critical alert
    /// </summary>
    [Id(9)]
    public double? CriticalHigh { get; set; }

    /// <summary>
    /// Whether auto-verify is enabled for this specific test on this instrument
    /// </summary>
    [Id(10)]
    public bool AutoVerifyEnabled { get; set; }

    /// <summary>
    /// VistA lab subscript for result routing — CH, MI, SP, CY, EM
    /// </summary>
    [Id(11)]
    public string Subscript { get; set; } = "CH";
}

/// <summary>
/// Instrument type — mirrors VistA interface type codes in file #62.48
/// </summary>
[GenerateSerializer]
public enum InstrumentType
{
    /// <summary>Universal interface (type 1) — bidirectional HL7</summary>
    UNIVERSAL = 1,
    /// <summary>Point-of-care device (types 20-29) — e.g., i-STAT, glucometers</summary>
    POINT_OF_CARE = 20,
    /// <summary>Reference lab shipping interface (type 30)</summary>
    SHIPPING = 30
}

/// <summary>
/// Instrument operational status
/// </summary>
[GenerateSerializer]
public enum InstrumentStatus
{
    INACTIVE,
    ACTIVE,
    MAINTENANCE,
    ERROR
}
