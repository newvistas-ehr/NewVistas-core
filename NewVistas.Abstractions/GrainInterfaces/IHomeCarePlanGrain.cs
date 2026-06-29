// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// The interdisciplinary plan of care for a home-care episode.
/// Key pattern: "HHC-POC:{guid}". VistA File #750 plan of care; (Phase 2) CMS-485.
/// </summary>
public interface IHomeCarePlanGrain : IGrainWithStringKey
{
    Task CreateAsync(string episodeId, string patientId, string establishedById, string establishedByName);
    Task AddProblemAsync(CarePlanProblem problem);
    Task UpdateProblemAsync(CarePlanProblem problem);
    Task ResolveProblemAsync(string problemId);
    Task ReviewAsync(DateTime reviewDate, DateTime? nextReviewDue);
    Task<HomeCarePlanState> GetPlanAsync();
}
