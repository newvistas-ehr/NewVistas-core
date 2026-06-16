// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents a CPT (Current Procedural Terminology) code entry.
/// Maps to VistA CPT file (#81) maintained by the AMA.
///
/// CPT codes are used across VistA for:
///   - PCE encounter procedure documentation (File #9000010.18)
///   - Integrated Billing claim line items
///   - Fee Basis authorization details
///   - Event Capture workload tracking
///   - Surgery case coding
///
/// Key fields from File #81:
///   .01  - CPT CODE (e.g., "99213", "36415")
///   2    - SHORT NAME
///   3    - CPT CATEGORY (E&M, Surgery, Radiology, etc.)
///   5    - DESCRIPTION (long text)
///   60   - STATUS (Active/Inactive)
/// </summary>
[GenerateSerializer]
public class CptCodeState
{
    /// <summary>
    /// CPT code value (File #81 field .01).
    /// e.g., "99213", "36415", "10060"
    /// </summary>
    [Id(0)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Short name / descriptor (File #81 field 2).
    /// Brief description of the procedure.
    /// </summary>
    [Id(1)]
    public string ShortName { get; set; } = string.Empty;

    /// <summary>
    /// Long description (File #81 field 5).
    /// Full clinical description of the procedure.
    /// </summary>
    [Id(2)]
    public string LongDescription { get; set; } = string.Empty;

    /// <summary>
    /// CPT category grouping (File #81 field 3).
    /// e.g., "E&M", "Surgery", "Radiology", "Pathology", "Medicine"
    /// </summary>
    [Id(3)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Work Relative Value Unit — physician work component.
    /// Used for resource-based relative value scale (RBRVS) reimbursement.
    /// </summary>
    [Id(4)]
    public decimal WorkRvu { get; set; }

    /// <summary>
    /// Total Relative Value Unit — combined work + practice expense + malpractice.
    /// </summary>
    [Id(5)]
    public decimal TotalRvu { get; set; }

    /// <summary>
    /// Whether this code is currently active (File #81 field 60).
    /// </summary>
    [Id(6)]
    public string Status { get; set; } = "ACTIVE";

    /// <summary>
    /// Effective date for this code version.
    /// </summary>
    [Id(7)]
    public DateTime? EffectiveDate { get; set; }

    /// <summary>
    /// Record creation timestamp.
    /// </summary>
    [Id(8)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last modification timestamp.
    /// </summary>
    [Id(9)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
