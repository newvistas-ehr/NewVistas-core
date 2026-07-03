// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Clinical;
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
    private IPatientAccessControlGrain Pac() => GrainFactory.GetGrain<IPatientAccessControlGrain>($"PAC:{PatientId}");

    /// <summary>
    /// UNGATED system read of this chart's Person anchor (null if unlinked). Callers that represent a
    /// viewer MUST use <see cref="GetPatientPersonForViewerAsync"/> instead — that path enforces the
    /// access decision + audit so the cross-role/employee-patient status never leaks.
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

    // ─── Phase 4 — viewer-gated access (ADR-002) ──────────────────────────────

    /// <summary>
    /// Decides — and audits — a viewer's access to this chart. Treatment relationship is never gated;
    /// break-the-glass is only for access without one and never hard-blocks (attest-and-proceed).
    /// </summary>
    public Task<PatientAccessDecision> AccessPatientAsync(string viewerUserId, string viewerName, bool breakTheGlassAttested, string? justification)
        => Pac().DecideAccessAsync(viewerUserId, viewerName, breakTheGlassAttested, justification);

    /// <summary>
    /// Viewer-facing cross-role Person read. Runs the access decision (+ audit) and returns the Person
    /// detail ONLY when granted; otherwise the Person is null and the decision says why (e.g. requires
    /// break-the-glass). This is how the employee-patient / cross-role status is protected from leaking.
    /// </summary>
    public async Task<PersonViewResult> GetPatientPersonForViewerAsync(string viewerUserId, string viewerName, bool breakTheGlassAttested, string? justification)
    {
        PatientAccessDecision decision = await Pac().DecideAccessAsync(viewerUserId, viewerName, breakTheGlassAttested, justification);
        PersonState? person = decision.Granted ? await GetPatientPersonAsync() : null;
        return new PersonViewResult { Decision = decision, Person = person };
    }

    /// <summary>Sets this patient's own sharing preference (maximal-openness is a first-class choice).</summary>
    public Task SetPatientSharePreferenceAsync(PatientSharePreference preference)
        => Pac().SetSharePreferenceAsync(preference);

    /// <summary>This patient's own "who viewed my chart" access report (ADR-002 Phase 4b — accountability).</summary>
    public Task<List<PatientAccessLog>> GetMyAccessLogAsync() => Pac().GetAccessLogAsync();

    /// <summary>Suspicious accesses to this (sensitive) chart — break-the-glass / blocked attempts (anomaly surface).</summary>
    public Task<List<PatientAccessLog>> GetSuspiciousAccessesAsync() => Pac().GetSuspiciousAccessesAsync();

    /// <summary>
    /// Cascade-testing opportunities (ADR-002 Phase 5): family-history relatives on this chart that are
    /// LINKED to a Person who is also a patient here with a confirmed pathogenic germline finding — so
    /// targeted testing can be offered to this patient. Read-only decision support (in production the
    /// relative's result disclosure is gated by their consent / the genetics team).
    /// </summary>
    public async Task<List<CascadeOpportunity>> GetCascadeOpportunitiesAsync()
    {
        var result = new List<CascadeOpportunity>();
        FamilyHistoryState fh = await FamilyHx().GetAsync();
        foreach (FamilyMemberHistoryEntry member in fh.Members.Where(m => !string.IsNullOrEmpty(m.LinkedPersonId)))
        {
            PersonState person = await Person(member.LinkedPersonId).GetAsync();
            foreach (PersonPatientRole role in person.PatientRoles)
            {
                GenomicsState g = await GrainFactory.GetGrain<IGenomicsGrain>(role.PatientId).GetAsync();
                List<HereditaryFinding> findings = HereditaryRisk.AssessVariants(g.Reports.SelectMany(r => r.Variants));
                foreach (HereditaryFinding f in findings)
                {
                    result.Add(new CascadeOpportunity
                    {
                        RelativeName = string.IsNullOrWhiteSpace(member.Name) ? person.Name : member.Name,
                        Relationship = member.Relationship.ToString(),
                        RelativePatientId = role.PatientId,
                        Gene = f.Gene,
                        Variant = f.Variant,
                        Syndrome = f.Syndrome,
                        Recommendation = $"{member.Relationship} is a confirmed {f.Gene} carrier ({f.Syndrome}). Offer targeted (cascade) genetic testing to this patient."
                    });
                }
            }
        }
        return result;
    }
}
