// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// The facility-wide home-care census / caseload roster — the analog of VistA's HBPC
/// <c>HBH</c> workload/case-mix reporting package. Holds one summary entry per episode and
/// powers the caseload view and workload roll-up.
/// Key pattern: "HHC-CENSUS:{siteId}" (default singleton "HHC-CENSUS:DEFAULT").
/// </summary>
[GenerateSerializer]
public class HomeCareCensusState
{
    /// <summary>Site identifier (the grain key).</summary>
    [Id(0)] public string SiteId { get; set; } = string.Empty;

    /// <summary>One summary entry per home-care episode.</summary>
    [Id(1)] public List<HomeCareCensusEntry> Entries { get; set; } = new();

    [Id(2)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>Aggregate workload counts for the home-care program (the HBH workload roll-up).</summary>
[GenerateSerializer]
public class HomeCareWorkloadStats
{
    [Id(0)] public int ActiveEpisodes { get; set; }
    [Id(1)] public int OnHoldEpisodes { get; set; }
    [Id(2)] public int BasicCare { get; set; }
    [Id(3)] public int EnhancedCare { get; set; }
    [Id(4)] public int PalliativeCare { get; set; }
    /// <summary>Active episodes with no completed visit in the last 30 days.</summary>
    [Id(5)] public int NoRecentVisit { get; set; }
    /// <summary>Active episodes with a visit scheduled in the next 7 days.</summary>
    [Id(6)] public int UpcomingVisits { get; set; }
}
