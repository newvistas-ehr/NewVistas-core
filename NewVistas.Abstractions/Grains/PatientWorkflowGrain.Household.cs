// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Person-anchored household orchestration from the patient's side (Whole-Person Social Care, Phase 1).
/// Resolves the patient's household via the Person anchor (ADR-002) and manages membership. Feature-gated
/// by <c>SOCIAL_CARE</c>; the const and guard defined here are shared by the SDOH workflow partial.
/// Household depends on Person identity — a patient with no <c>PersonId</c> has an empty household by
/// design (it degrades, never guesses).
/// </summary>
public partial class PatientWorkflowGrain
{
    internal const string SocialCareFeature = "SOCIAL_CARE";

    private IHouseholdGrain Household(string householdId) => GrainFactory.GetGrain<IHouseholdGrain>(householdId);

    private IPersonHouseholdIndexGrain PersonHouseholdIndex(string personId) =>
        GrainFactory.GetGrain<IPersonHouseholdIndexGrain>($"PERSON-HOUSEHOLD-IDX:{personId}");

    /// <summary>The patient's current household (resolved via their Person anchor), or null when the
    /// feature is off, the patient has no Person, or they belong to no household.</summary>
    public async Task<HouseholdState?> GetPatientHouseholdAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(SocialCareFeature);
        if (!enabled)
            return null;

        PatientState patient = await GetPatientAsync();
        if (string.IsNullOrEmpty(patient.PersonId))
            return null;

        string? householdId = await PersonHouseholdIndex(patient.PersonId!).GetCurrentHouseholdIdAsync();
        if (string.IsNullOrEmpty(householdId))
            return null;

        return await Household(householdId).GetAsync();
    }

    /// <summary>Creates a new household with this patient (via their Person) as head. Returns the household id.</summary>
    public async Task<string> CreateHouseholdForPatientAsync(string label, string relationship, string facilityId, string byUser)
    {
        await RequireSocialCareFeatureAsync();
        string personId = await CreateOrGetPersonForPatientAsync(facilityId, PersonLinkConfidence.ConfirmedByRegistration, byUser);
        PatientState patient = await GetPatientAsync();

        string householdId = $"HOUSEHOLD:{Guid.NewGuid()}";
        await Household(householdId).CreateAsync(label, byUser);
        await Household(householdId).AddMemberAsync(personId, patient.Name, relationship, HouseholdMemberRole.HeadOfHousehold, byUser);
        return householdId;
    }

    /// <summary>Adds this patient (via their Person) to an existing household.</summary>
    public async Task AddPatientToHouseholdAsync(string householdId, string relationship, HouseholdMemberRole role, string facilityId, string byUser)
    {
        await RequireSocialCareFeatureAsync();
        string personId = await CreateOrGetPersonForPatientAsync(facilityId, PersonLinkConfidence.ConfirmedByRegistration, byUser);
        PatientState patient = await GetPatientAsync();
        await Household(householdId).AddMemberAsync(personId, patient.Name, relationship, role, byUser);
    }

    /// <summary>
    /// Adds a NON-patient member to a household (a family member not in the system as a patient) by
    /// minting a bare Person record for them. Returns the new Person id.
    /// </summary>
    public async Task<string> AddNonPatientMemberToHouseholdAsync(string householdId, string name,
        DateTime? dateOfBirth, string sex, string ssnLast4, string relationship, HouseholdMemberRole role, string byUser)
    {
        await RequireSocialCareFeatureAsync();
        string personId = $"PERSON:{Guid.NewGuid()}";
        await Person(personId).RegisterIdentityAsync(name, dateOfBirth, sex, ssnLast4 ?? string.Empty);
        await Household(householdId).AddMemberAsync(personId, name, relationship, role, byUser);
        return personId;
    }

    private async Task RequireSocialCareFeatureAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(SocialCareFeature);
        if (!enabled)
            throw new InvalidOperationException(
                "Social Care is not enabled for this site. Enable the SOCIAL_CARE feature in Site Parameters.");
    }
}
