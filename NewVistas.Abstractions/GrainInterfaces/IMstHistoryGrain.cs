// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages a patient's MST (Military Sexual Trauma) screening history
/// (VistA File #29.11 MST HISTORY).
/// Key: <c>"MST:{patientId}"</c>
/// MUMPS references: DGMSTSC.m, DGMSTS.m
/// </summary>
public interface IMstHistoryGrain : IGrainWithStringKey
{
    /// <summary>Returns the full MST history record.</summary>
    Task<MstHistoryState> GetAsync();

    /// <summary>
    /// Records a new MST screening encounter. If status is Verified and
    /// the patient was not previously MST positive, sets MstPositive = true.
    /// </summary>
    Task RecordScreeningAsync(
        DateTime screeningDate,
        MstStatus status,
        string screenedByUserId,
        string screenedByUserName,
        string? location,
        string? notes);

    /// <summary>Updates the current overall MST status without adding a screening entry.</summary>
    Task SetCurrentStatusAsync(MstStatus status);

    /// <summary>Records the location and date of the original MST disclosure.</summary>
    Task SetDisclosureAsync(string location, DateTime date);
}
