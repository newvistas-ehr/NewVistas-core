// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages the list of treating facilities for a patient (VistA File #391.91 TREATING FACILITY LIST).
/// Key: <c>"TREATING-FAC:{patientId}"</c>
/// MUMPS references: VAFHLTR.m, VAEFSPID.m
/// </summary>
public interface ITreatingFacilityListGrain : IGrainWithStringKey
{
    /// <summary>Returns the full treating facility list.</summary>
    Task<TreatingFacilityListState> GetAsync();

    /// <summary>Returns only currently active treating facility relationships.</summary>
    Task<List<TreatingFacilityEntry>> GetActiveFacilitiesAsync();

    /// <summary>Adds a new facility or updates the existing entry with the same FacilityId.</summary>
    Task AddOrUpdateFacilityAsync(TreatingFacilityEntry facility);

    /// <summary>Marks a treating facility relationship as inactive.</summary>
    Task DeactivateFacilityAsync(string facilityId);

    /// <summary>Sets the patient's primary enrollment facility.</summary>
    Task SetPrimaryFacilityAsync(string facilityId, string facilityName);
}
