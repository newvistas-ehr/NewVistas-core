// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient grain tracking suicide risk level, high-risk flag, and follow-up contacts.
/// Key pattern: "SP-RISK:{patientId}"
/// </summary>
public interface IPatientRiskGrain : IGrainWithStringKey
{
    Task<PatientRiskState> GetRiskStateAsync();

    Task SetRiskLevelAsync(RiskLevel level, string patientId, string patientName, string providerId, string providerName);

    Task SetHighRiskFlagAsync(bool flagged);

    Task AddFollowUpContactAsync(FollowUpContact contact);
}
