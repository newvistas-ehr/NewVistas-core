// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the Lab EDI Configuration grain. Stores connection and
/// automation settings for a single external reference laboratory partner.
/// </summary>
[GenerateSerializer]
public class LabEdiConfigState
{
    /// <summary>
    /// Reference lab identifier (grain key suffix)
    /// </summary>
    [Id(0)]
    public string ReferenceLabId { get; set; } = string.Empty;

    /// <summary>
    /// Reference laboratory display name
    /// </summary>
    [Id(1)]
    public string LabName { get; set; } = string.Empty;

    /// <summary>
    /// CLIA (Clinical Laboratory Improvement Amendments) number
    /// </summary>
    [Id(2)]
    public string CliaNumber { get; set; } = string.Empty;

    /// <summary>
    /// Connection type: "TCP/MLLP", "SFTP", "HTTPS"
    /// </summary>
    [Id(3)]
    public string ConnectionType { get; set; } = string.Empty;

    /// <summary>
    /// TCP/IP host address for MLLP connections
    /// </summary>
    [Id(4)]
    public string? Host { get; set; }

    /// <summary>
    /// TCP port number for MLLP connections
    /// </summary>
    [Id(5)]
    public int? Port { get; set; }

    /// <summary>
    /// sFTP path for file-based exchange
    /// </summary>
    [Id(6)]
    public string? FtpPath { get; set; }

    /// <summary>
    /// HL7 version used for message exchange (e.g., "2.3.1", "2.5.1")
    /// </summary>
    [Id(7)]
    public string? Hl7Version { get; set; }

    /// <summary>
    /// Sending facility OID or identifier for MSH-4
    /// </summary>
    [Id(8)]
    public string SendingFacilityId { get; set; } = string.Empty;

    /// <summary>
    /// Sending facility display name for MSH-4
    /// </summary>
    [Id(9)]
    public string SendingFacilityName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the reference lab is active for EDI processing
    /// </summary>
    [Id(10)]
    public bool IsActive { get; set; }

    /// <summary>
    /// Automatically acknowledge inbound results (no manual review required for ACK)
    /// </summary>
    [Id(11)]
    public bool AutoAcknowledge { get; set; }

    /// <summary>
    /// Automatically file accepted results into patient lab records
    /// </summary>
    [Id(12)]
    public bool AutoFileResults { get; set; }

    /// <summary>
    /// Test code mappings: local test code → reference lab test code + LOINC
    /// </summary>
    [Id(13)]
    public Dictionary<string, LabEdiTestMapping> TestMappings { get; set; } = new();

    /// <summary>
    /// Date/time configuration was created
    /// </summary>
    [Id(14)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date/time configuration was last modified
    /// </summary>
    [Id(15)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Maps a local test code to a reference lab's test code and LOINC.
/// </summary>
[GenerateSerializer]
public class LabEdiTestMapping
{
    /// <summary>
    /// Local test code used in this facility
    /// </summary>
    [Id(0)]
    public string LocalTestCode { get; set; } = string.Empty;

    /// <summary>
    /// Test code used by the reference lab
    /// </summary>
    [Id(1)]
    public string ReferenceLabTestCode { get; set; } = string.Empty;

    /// <summary>
    /// LOINC code for cross-reference (if mapped)
    /// </summary>
    [Id(2)]
    public string? LoincCode { get; set; }

    /// <summary>
    /// Test display name
    /// </summary>
    [Id(3)]
    public string TestName { get; set; } = string.Empty;
}
