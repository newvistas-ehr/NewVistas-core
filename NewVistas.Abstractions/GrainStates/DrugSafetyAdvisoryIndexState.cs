// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lightweight projection of an advisory for list/dashboard display and for
/// drug-class lookups (which advisories target a given class).
/// </summary>
[GenerateSerializer]
public class DrugSafetyAdvisorySummary
{
    /// <summary>Advisory id.</summary>
    [Id(0)]
    public string AdvisoryId { get; set; } = string.Empty;

    /// <summary>Advisory title.</summary>
    [Id(1)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Relative urgency.</summary>
    [Id(2)]
    public AdvisorySeverity Severity { get; set; } = AdvisorySeverity.Moderate;

    /// <summary>Lifecycle status.</summary>
    [Id(3)]
    public AdvisoryStatus Status { get; set; } = AdvisoryStatus.Draft;

    /// <summary>What the provider is being asked to do.</summary>
    [Id(4)]
    public AdvisoryActionType ActionType { get; set; } = AdvisoryActionType.WarnPatient;

    /// <summary>Target VA drug class codes.</summary>
    [Id(5)]
    public List<string> TargetDrugClassCodes { get; set; } = new();

    /// <summary>Date the source was published, when known.</summary>
    [Id(6)]
    public DateTime? SourcePublishedDate { get; set; }

    /// <summary>Patients reached so far.</summary>
    [Id(7)]
    public int TotalDispatched { get; set; }
}

/// <summary>
/// Singleton index of all drug safety advisories. Grain key: "DSA-INDEX".
/// </summary>
[GenerateSerializer]
public class DrugSafetyAdvisoryIndexState
{
    /// <summary>Advisory summaries by advisory id.</summary>
    [Id(0)]
    public Dictionary<string, DrugSafetyAdvisorySummary> ById { get; set; } = new();
}
