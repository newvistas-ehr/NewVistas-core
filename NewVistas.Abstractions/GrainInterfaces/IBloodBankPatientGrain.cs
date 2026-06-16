// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Blood Bank Patient Grain — maintains a patient's blood bank record.
///
/// Derived from VistA Blood Bank module (BBAPI.m, BBTM.m):
///   File #65    — BLOOD BANK PATIENT record (ABO/Rh type, antibodies, history)
///
/// Grain key: "BB-PATIENT:{patientId}"
/// </summary>
public interface IBloodBankPatientGrain : IGrainWithStringKey
{
    Task<BloodBankPatientState> GetAsync();

    Task InitializeAsync(string patientId);

    /// <summary>
    /// Records or updates the patient's ABO/Rh blood type and antibody screen.
    /// Corresponds to VistA BB TYPE &amp; SCREEN order (BBTM SCREEN).
    /// </summary>
    Task UpdateBloodTypeAsync(
        AboBloodType aboType,
        RhBloodType rhType,
        AntibodyScreenResult antibodyScreenResult,
        DateTime? antibodyScreenDate,
        string? directAntibodyTest,
        string? specialRequirements,
        string? notes);

    /// <summary>Increments the cumulative transfusion count.</summary>
    Task IncrementTransfusionCountAsync();
}
