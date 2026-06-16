// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Event published to Orleans Streams when a new lab result is ingested.
/// Used by the PatientLabIndex grain for incremental updates while hot.
/// </summary>
[GenerateSerializer]
[Immutable]
public class LabResultEvent
{
    [Id(0)]
    public string PatientIcn { get; set; } = string.Empty;

    [Id(1)]
    public string ResultId { get; set; } = string.Empty;

    [Id(2)]
    public string LoincCode { get; set; } = string.Empty;

    [Id(3)]
    public string TestName { get; set; } = string.Empty;

    [Id(4)]
    public string Value { get; set; } = string.Empty;

    [Id(5)]
    public string Units { get; set; } = string.Empty;

    [Id(6)]
    public string ReferenceRange { get; set; } = string.Empty;

    [Id(7)]
    public LabAbnormalFlag AbnormalFlag { get; set; }

    [Id(8)]
    public DateTimeOffset ResultDate { get; set; }

    [Id(9)]
    public string FacilityCode { get; set; } = string.Empty;

    [Id(10)]
    public string? PanelName { get; set; }

    /// <summary>
    /// Creates a LabResultEvent from a detailed lab result.
    /// </summary>
    public static LabResultEvent FromResult(LabResultDetail result, string patientIcn)
    {
        return new LabResultEvent
        {
            PatientIcn = patientIcn,
            ResultId = result.ResultId,
            LoincCode = result.LoincCode,
            TestName = result.TestName,
            Value = result.Value,
            Units = result.Units,
            ReferenceRange = result.ReferenceRange,
            AbnormalFlag = result.AbnormalFlag,
            ResultDate = result.ResultDate,
            FacilityCode = result.FacilityCode,
            PanelName = result.PanelName
        };
    }
}
