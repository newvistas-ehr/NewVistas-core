// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// A home-care comprehensive assessment (HBPC) or OASIS assessment (reserved Phase 2).
/// Key pattern: "HHC-ASSESS:{guid}". VistA File #750 assessment; (Phase 2) OASIS data set.
/// </summary>
public interface IHomeCareAssessmentGrain : IGrainWithStringKey
{
    /// <summary>Records an HBPC interdisciplinary comprehensive assessment.</summary>
    Task RecordComprehensiveAsync(
        string episodeId,
        string patientId,
        string assessorId,
        string assessorName,
        DateTime assessmentDate,
        HbpcComprehensiveAssessment assessment);

    /// <summary>Records an OASIS assessment (Phase 2) at a given OASIS time point.</summary>
    Task RecordOasisAsync(
        string episodeId,
        string patientId,
        HomeCareAssessmentType assessmentType,
        string assessorId,
        string assessorName,
        DateTime assessmentDate,
        OasisDataSet oasis);

    Task<HomeCareAssessmentState> GetAssessmentAsync();
}
