// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Medicare home-health billing for one episode — Notice of Admission and per-period claims.
/// Key pattern: "HHC-BILLING:{episodeId}". (Phase 2 / HOME_HEALTH_MEDICARE.)
/// </summary>
public interface IHomeHealthBillingGrain : IGrainWithStringKey
{
    /// <summary>Submits the Notice of Admission (flags it late if &gt; 5 days after Start of Care).</summary>
    Task SubmitNoticeOfAdmissionAsync(string patientId, DateTime admissionDate, DateTime submittedDate);

    /// <summary>Creates a draft claim for a 30-day payment period. Returns the claim id.</summary>
    Task<string> GenerateClaimAsync(string certificationPeriodId, string paymentPeriodId, string hippsCode, bool isLupa);

    /// <summary>Submits a previously generated claim.</summary>
    Task SubmitClaimAsync(string claimId, DateTime submittedDate);

    Task<HomeHealthBillingState> GetBillingAsync();
}
