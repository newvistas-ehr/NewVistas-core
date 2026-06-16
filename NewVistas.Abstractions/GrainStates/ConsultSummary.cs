// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Summary of a consult for cover sheet display and consult listing.
/// Derived from VistA REQUEST/CONSULTATION file (#123).
/// </summary>
[GenerateSerializer]
public class ConsultSummary
{
    [Id(0)]
    public string ConsultId { get; set; } = string.Empty;

    [Id(1)]
    public string ToService { get; set; } = string.Empty;

    [Id(2)]
    public string? FromService { get; set; }

    [Id(3)]
    public string Status { get; set; } = string.Empty;

    [Id(4)]
    public string Urgency { get; set; } = string.Empty;

    [Id(5)]
    public DateTime RequestDateTime { get; set; }

    [Id(6)]
    public string? RequestingProviderName { get; set; }

    [Id(7)]
    public string? AttentionProviderName { get; set; }

    [Id(8)]
    public string? ProvisionalDiagnosis { get; set; }

    [Id(9)]
    public bool HasResultDocument { get; set; }
}
