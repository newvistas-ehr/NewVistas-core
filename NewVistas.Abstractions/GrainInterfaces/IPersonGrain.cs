// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// The identity anchor for a HUMAN (ADR-002). Unifies the patient-role (a chart), the staff-role (a
/// File #200 record), and relative-appearances (on others' charts). Sits ABOVE the ICN/MPI patient-
/// identity layer. The Person orchestrates linking — it sets the back-pointer on the linked record so
/// the anchor and pointer never drift. Key pattern: "PERSON:{guid}".
/// </summary>
public interface IPersonGrain : IGrainWithStringKey
{
    /// <summary>Sets the identity spine (name / DOB / sex / SSN last-4) and indexes the Person.</summary>
    Task RegisterIdentityAsync(string name, DateTime? dateOfBirth, string sex, string ssnLast4);

    /// <summary>Links a patient chart to this Person (sets <c>PatientState.PersonId</c>). Idempotent by patientId.</summary>
    Task LinkPatientAsync(string patientId, string facilityId, bool primary, PersonLinkConfidence confidence, string linkedBy);

    /// <summary>Links a staff/provider record to this Person (sets <c>NewPersonState.PersonId</c>). Idempotent by userId.</summary>
    Task LinkStaffAsync(string userId, PersonLinkConfidence confidence, string linkedBy);

    /// <summary>Records that this Person appears as a relative on another patient's chart. Idempotent by (onPatientId, sourceEntryId).</summary>
    Task AddRelativeAppearanceAsync(string onPatientId, string relationship, PersonRelativeSource source, string sourceEntryId, string linkedBy);

    Task UnlinkPatientAsync(string patientId);
    Task UnlinkStaffAsync(string userId);
    Task RemoveRelativeAppearanceAsync(string onPatientId, string sourceEntryId);

    Task<PersonState> GetAsync();
}
