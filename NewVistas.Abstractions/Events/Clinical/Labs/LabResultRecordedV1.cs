// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.Events.Clinical.Labs;

/// <summary>
/// Causal event recording a result value for a collected lab specimen — VistA
/// LRVER1 RESULT workflow. Recording precedes verification.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record LabResultRecordedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "LABS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string LabTestId { get; init; } = string.Empty;

    [Id(7)] public DateTime ResultDateTime { get; init; }
    [Id(8)] public string ResultValue { get; init; } = string.Empty;
    [Id(9)] public string? ResultUnit { get; init; }
    [Id(10)] public string? ReferenceRangeLow { get; init; }
    [Id(11)] public string? ReferenceRangeHigh { get; init; }
    [Id(12)] public string? AbnormalFlag { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(LabResultRecordedV1),
        LabTestId,
        ResultDateTime.ToString("O"),
        ResultValue,
        ResultUnit ?? string.Empty,
        ReferenceRangeLow ?? string.Empty,
        ReferenceRangeHigh ?? string.Empty,
        AbnormalFlag ?? string.Empty);
}
