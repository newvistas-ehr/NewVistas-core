// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton grain representing the system-wide transplant waiting list index.
/// Grain key: "TX-WAITLIST-IDX"
/// </summary>
public interface ITransplantWaitlistIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all patients in the waitlist index, ordered by priority then listing date.</summary>
    Task<List<TransplantWaitlistEntry>> GetAllPatientsAsync();

    /// <summary>Returns patients filtered by organ type needed.</summary>
    Task<List<TransplantWaitlistEntry>> GetPatientsByOrganAsync(TransplantOrganType organType);

    /// <summary>Returns patients filtered by waitlist status.</summary>
    Task<List<TransplantWaitlistEntry>> GetPatientsByStatusAsync(TransplantStatus status);

    /// <summary>Returns only actively-listed patients (Status = Listed).</summary>
    Task<List<TransplantWaitlistEntry>> GetActiveWaitlistAsync();

    /// <summary>Adds or updates a patient's waitlist entry.</summary>
    Task UpsertPatientAsync(TransplantWaitlistEntry entry);

    /// <summary>Removes a patient from the waitlist index.</summary>
    Task RemovePatientAsync(string patientId);
}
