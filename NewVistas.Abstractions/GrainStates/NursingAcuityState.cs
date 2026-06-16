// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Patient classification (acuity) state — VistA File #212 (NURSING PATIENT), acuity sub-record.
/// Cross-referenced with File #210 (NURSING UNIT) for staffing-ratio calculations.
/// The acuity level (1–5) drives nursing workload and staffing ratios.
/// </summary>
[GenerateSerializer]
public class NursingAcuityState
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Most recently recorded acuity classification level.</summary>
    [Id(1)] public AcuityLevel CurrentAcuityLevel { get; set; } = AcuityLevel.NotAssigned;

    /// <summary>Numeric acuity score from the facility's classification tool.</summary>
    [Id(2)] public int? CurrentAcuityScore { get; set; }

    [Id(3)] public DateTime? CurrentAcuityDateTime { get; set; }
    [Id(4)] public string? CurrentAcuityNurseId { get; set; }
    [Id(5)] public string? CurrentAcuityNurseName { get; set; }

    /// <summary>Shift the current classification applies to: Days, Evenings, Nights.</summary>
    [Id(6)] public string? CurrentShift { get; set; }

    /// <summary>Full chronological history of acuity classifications.</summary>
    [Id(7)] public List<AcuityRecord> AcuityHistory { get; set; } = new();

    [Id(8)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>A single acuity classification event in the patient's history.</summary>
[GenerateSerializer]
public class AcuityRecord
{
    [Id(0)] public DateTime RecordedDateTime { get; set; }
    [Id(1)] public AcuityLevel Level { get; set; }

    /// <summary>Raw numeric score from the classification instrument.</summary>
    [Id(2)] public int? Score { get; set; }

    [Id(3)] public string? NurseId { get; set; }
    [Id(4)] public string? NurseName { get; set; }

    /// <summary>Shift context: Days, Evenings, Nights.</summary>
    [Id(5)] public string? Shift { get; set; }

    [Id(6)] public string? Notes { get; set; }
}
