// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Person-identity orchestration from the patient's side (ADR-002). Bootstraps/reads the Person anchor
/// for this chart and links structured family-history relatives to a Person. Additive overlay — a null
/// PersonId behaves exactly as before. (Staff-side linking is done directly on the Person; see
/// <see cref="IPersonGrain"/>.) Cross-role reads are privileged in Phase 4.
/// </summary>
public partial class PatientWorkflowGrain
{
    private IPersonGrain Person(string personId) => GrainFactory.GetGrain<IPersonGrain>(personId);

    /// <summary>
    /// Returns this chart's Person anchor if one is linked; else null. (Phase 4 gates the cross-role
    /// detail behind an audited break-the-glass permission.)
    /// </summary>
    public async Task<PersonState?> GetPatientPersonAsync()
    {
        PatientState patient = await GetPatientAsync();
        if (string.IsNullOrEmpty(patient.PersonId)) return null;
        return await Person(patient.PersonId).GetAsync();
    }

    /// <summary>
    /// Bootstraps a Person from this patient's demographics and links the chart (idempotent — returns
    /// the existing PersonId if already linked). Returns the PersonId.
    /// </summary>
    public async Task<string> CreateOrGetPersonForPatientAsync(string facilityId, PersonLinkConfidence confidence, string byUser)
    {
        PatientState patient = await GetPatientAsync();
        if (!string.IsNullOrEmpty(patient.PersonId)) return patient.PersonId!;

        string personId = $"PERSON:{Guid.NewGuid()}";
        string ssn = patient.SocialSecurityNumber ?? string.Empty;
        string last4 = ssn.Length >= 4 ? ssn[^4..] : ssn;

        IPersonGrain person = Person(personId);
        await person.RegisterIdentityAsync(patient.Name, patient.DateOfBirth, patient.Sex, last4);
        await person.LinkPatientAsync(PatientId, facilityId, primary: true, confidence, byUser);
        return personId;
    }

    /// <summary>Links an already-created Person to this chart (registrar-confirmed).</summary>
    public Task LinkPatientToPersonAsync(string personId, string facilityId, PersonLinkConfidence confidence, string byUser)
        => Person(personId).LinkPatientAsync(PatientId, facilityId, primary: true, confidence, byUser);

    /// <summary>
    /// Links a structured family-history relative on THIS chart to a known Person (e.g. a relative who
    /// is also a patient). Sets the entry's LinkedPersonId and records the reverse relative-appearance
    /// on the Person.
    /// </summary>
    public async Task LinkFamilyMemberToPersonAsync(string memberId, string personId, string byUser)
    {
        FamilyHistoryState fh = await FamilyHx().GetAsync();
        FamilyMemberHistoryEntry? member = fh.Members.FirstOrDefault(m => m.MemberId == memberId);
        if (member is null) return;

        await FamilyHx().SetMemberPersonLinkAsync(memberId, personId);
        await Person(personId).AddRelativeAppearanceAsync(
            PatientId, member.Relationship.ToString(), PersonRelativeSource.FamilyHistory, memberId, byUser);
    }
}
