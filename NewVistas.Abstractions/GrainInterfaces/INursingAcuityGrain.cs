// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Patient classification (acuity) grain — VistA File #212 (NURSING PATIENT), acuity sub-record.
/// Records per-shift acuity scores used for nursing staffing decisions.
/// Grain key: "NURS-ACUITY:{patientId}"
/// </summary>
public interface INursingAcuityGrain : IGrainWithStringKey
{
    /// <summary>Returns the current acuity state including history.</summary>
    Task<NursingAcuityState> GetAsync();

    /// <summary>
    /// Records a new acuity classification.
    /// Updates CurrentAcuityLevel and appends a record to AcuityHistory.
    /// </summary>
    Task RecordAcuityAsync(
        AcuityLevel level,
        int? score,
        string nurseId,
        string nurseName,
        string? shift,
        string? notes);
}
