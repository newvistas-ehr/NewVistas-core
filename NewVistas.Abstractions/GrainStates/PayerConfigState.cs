// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Configuration for an insurance payer's EDI eligibility verification connection.
/// Stores payer-specific routing, format, and connection details for 270/271 transactions.
/// Grain key: "PAYER-CFG:{payerId}"
/// </summary>
[GenerateSerializer]
public class PayerConfigState
{
    /// <summary>Payer identifier (insurance company trading partner ID). (.01)</summary>
    [Id(0)] public string PayerId { get; set; } = string.Empty;

    /// <summary>Payer display name. (.02)</summary>
    [Id(1)] public string PayerName { get; set; } = string.Empty;

    /// <summary>Whether this payer supports real-time EDI 270/271 eligibility verification. (.03)</summary>
    [Id(2)] public bool SupportsRealTimeEligibility { get; set; }

    /// <summary>EDI interchange receiver ID (ISA08). (.04)</summary>
    [Id(3)] public string? InterchangeReceiverId { get; set; }

    /// <summary>EDI interchange receiver qualifier (ISA07). (.05)</summary>
    [Id(4)] public string? InterchangeReceiverQualifier { get; set; }

    /// <summary>Payer's eligibility verification endpoint URL or address. (.06)</summary>
    [Id(5)] public string? EligibilityEndpoint { get; set; }

    /// <summary>Contact phone for eligibility inquiries. (.07)</summary>
    [Id(6)] public string? EligibilityPhone { get; set; }

    /// <summary>Whether this payer is currently active and accepting inquiries. (.08)</summary>
    [Id(7)] public bool IsActive { get; set; } = true;

    /// <summary>Supported service type codes for this payer (e.g., "30", "1", "47").</summary>
    [Id(8)] public List<string> SupportedServiceTypeCodes { get; set; } = new();

    /// <summary>Average response time in seconds (for monitoring).</summary>
    [Id(9)] public int? AverageResponseTimeSeconds { get; set; }

    /// <summary>Optional notes about this payer configuration.</summary>
    [Id(10)] public string? Notes { get; set; }

    /// <summary>When this configuration was created.</summary>
    [Id(11)] public DateTime CreatedDate { get; set; }

    /// <summary>When this configuration was last modified.</summary>
    [Id(12)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Lightweight payer entry for the payer config index.
/// </summary>
[GenerateSerializer]
public record PayerConfigIndexEntry
{
    /// <summary>Payer identifier.</summary>
    [Id(0)] public string PayerId { get; init; } = string.Empty;

    /// <summary>Payer display name.</summary>
    [Id(1)] public string PayerName { get; init; } = string.Empty;

    /// <summary>Whether real-time 270/271 is supported.</summary>
    [Id(2)] public bool SupportsRealTimeEligibility { get; init; }

    /// <summary>Whether this payer configuration is active.</summary>
    [Id(3)] public bool IsActive { get; init; }
}

/// <summary>
/// Index of all configured payers for eligibility verification.
/// Grain key: "PAYER-CFG-INDEX" (singleton)
/// </summary>
[GenerateSerializer]
public class PayerConfigIndexState
{
    /// <summary>All configured payers.</summary>
    [Id(0)] public List<PayerConfigIndexEntry> Entries { get; set; } = new();
}
