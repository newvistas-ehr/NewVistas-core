// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Holds a patient's active insurance benefit plan details.
/// Grain key: "PBM-PATIENT:{patientId}"
/// </summary>
public interface IPatientBenefitPlanGrain : IGrainWithStringKey
{
    Task<PatientBenefitPlanState> GetPlanAsync();

    Task SetPlanAsync(string planId, string planName, string insuranceName,
        string? groupNumber, string? memberId, DateTime? effectiveDate,
        DateTime? terminationDate, decimal copayTier1, decimal copayTier2,
        decimal copayTier3, int daySupplyLimit, decimal annualDeductible);

    Task MarkDeductibleMetAsync();

    Task DeactivateAsync();
}
