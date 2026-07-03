// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// ADR-002 Phase 4b (auto-established treatment relationships) + Phase 5 (cascade-testing
/// opportunities). Verifies: surgery/appointment/order write paths auto-establish a treatment
/// relationship so the treating cast gains frictionless access (AllowedByRelationship, no
/// break-the-glass); an expired relationship no longer grants; a non-team viewer still needs BTG;
/// suspicious accesses surface. And Phase 5: a family-history relative LINKED to a real chart with a
/// confirmed pathogenic germline finding surfaces a cascade-testing opportunity.
/// </summary>
[TestFixture]
public class PersonRelationshipCascadeTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientWorkflowGrain Workflow(string patientId) => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
    private IPersonGrain Person(string personId) => _cluster.GrainFactory.GetGrain<IPersonGrain>(personId);
    private INewPersonGrain Staff(string userId) => _cluster.GrainFactory.GetGrain<INewPersonGrain>($"USER:{userId}");
    private IPatientAccessControlGrain Pac(string patientId) => _cluster.GrainFactory.GetGrain<IPatientAccessControlGrain>($"PAC:{patientId}");

    private async Task<(IPatientWorkflowGrain Wf, string PatientId)> NewPatientAsync(string name = "REL,TEST")
    {
        string pid = $"REL-PT-{Guid.NewGuid()}";
        var wf = Workflow(pid);
        await wf.UpdateDemographicsAsync(name, "F", new DateTime(1980, 1, 1), "666123456");
        return (wf, pid);
    }

    private async Task<string> NewStaffAsync(string name = "REL,STAFF")
    {
        string userId = $"STAFF-{Guid.NewGuid()}";
        await Staff(userId).UpdateProfileAsync(name, "Registered Nurse", "RN", "NURSING", "NURSE", "NURSE",
            "Medical-Surgical", "INST-500", "VA MEDICAL CENTER", "DIV-500", "MAIN DIVISION");
        return userId;
    }

    // A patient chart whose owner is also on staff → auto-flagged employee-patient (sensitive).
    private async Task<(IPatientWorkflowGrain Wf, string PatientId, string PersonId)> NewEmployeePatientAsync()
    {
        var (wf, pid) = await NewPatientAsync("NIGHTINGALE,NORA");
        string userId = await NewStaffAsync("NIGHTINGALE,NORA");
        string personId = await wf.CreateOrGetPersonForPatientAsync("500", PersonLinkConfidence.ConfirmedByRegistration, "TEST");
        await Person(personId).LinkStaffAsync(userId, PersonLinkConfidence.ConfirmedByRegistration, "TEST");
        return (wf, pid, personId);
    }

    // ─────────────────────────── Phase 4b — relationship auto-establishment ───────────────────────────

    [Test]
    public async Task Surgery_AutoEstablishesSurgeonRelationship_AllowedByRelationship_NoBreakTheGlass()
    {
        var (wf, _, _) = await NewEmployeePatientAsync();   // sensitive employee-patient

        await wf.ScheduleSurgeryAsync("Rotator cuff repair", "29827", DateTime.UtcNow.Date.AddDays(14),
            "DR-SURGEON", "Dr Surgeon", "General", "Ortho", "tear", null, "OR", null);

        PatientAccessDecision d = await wf.AccessPatientAsync("DR-SURGEON", "Dr Surgeon",
            breakTheGlassAttested: false, justification: null);

        Assert.That(d.Outcome, Is.EqualTo(PatientAccessOutcome.AllowedByRelationship));
        Assert.That(d.Granted, Is.True);
        Assert.That(d.WasBreakTheGlass, Is.False);   // the surgeon is authorized WITHOUT break-the-glass
    }

    [Test]
    public async Task Appointment_AutoEstablishesProviderRelationship_AllowedByRelationship()
    {
        var (wf, pid, _) = await NewEmployeePatientAsync();

        await wf.ScheduleAppointmentAsync("CLINIC1", "Ortho Clinic",
            DateTime.UtcNow.Date.AddDays(3).AddHours(9), 30, "DR-PROV", "Dr Prov", "Follow-up", "ROUTINE");

        PatientAccessDecision d = await wf.AccessPatientAsync("DR-PROV", "Dr Prov",
            breakTheGlassAttested: false, justification: null);

        Assert.That(d.Outcome, Is.EqualTo(PatientAccessOutcome.AllowedByRelationship));
        Assert.That(d.Granted, Is.True);
        Assert.That(d.WasBreakTheGlass, Is.False);
    }

    [Test]
    public async Task EstablishRelationshipDirect_Order_AllowedByRelationship()
    {
        var (wf, pid, _) = await NewEmployeePatientAsync();

        await Pac(pid).EstablishRelationshipAsync("DR-ORDER", TreatmentRelationshipReason.Order, "ORD-1", null);

        PatientAccessDecision d = await wf.AccessPatientAsync("DR-ORDER", "Dr Order",
            breakTheGlassAttested: false, justification: null);

        Assert.That(d.Outcome, Is.EqualTo(PatientAccessOutcome.AllowedByRelationship));
        Assert.That(d.Granted, Is.True);
    }

    [Test]
    public async Task ExpiredRelationship_DoesNotGrant_RequiresBreakTheGlass()
    {
        var (wf, pid, _) = await NewEmployeePatientAsync();

        // Establish a relationship that already expired yesterday.
        await Pac(pid).EstablishRelationshipAsync("DR-OLD", TreatmentRelationshipReason.Encounter, "ENC-1",
            DateTime.UtcNow.AddDays(-1));

        PatientAccessDecision d = await wf.AccessPatientAsync("DR-OLD", "Dr Old",
            breakTheGlassAttested: false, justification: null);

        Assert.That(d.Outcome, Is.EqualTo(PatientAccessOutcome.RequiresBreakTheGlass));   // expired ⇒ no relationship
        Assert.That(d.Granted, Is.False);
    }

    [Test]
    public async Task NonTeamViewer_StillNeedsBreakTheGlass_EvenAfterAnotherProviderGotRelationship()
    {
        var (wf, pid, _) = await NewEmployeePatientAsync();
        await Pac(pid).EstablishRelationshipAsync("DR-TEAM", TreatmentRelationshipReason.Order, "ORD-9", null);

        PatientAccessDecision d = await wf.AccessPatientAsync("STRANGER", "A Stranger",
            breakTheGlassAttested: false, justification: null);

        Assert.That(d.Outcome, Is.EqualTo(PatientAccessOutcome.RequiresBreakTheGlass));
        Assert.That(d.Granted, Is.False);   // one provider's relationship does not authorize everyone
    }

    [Test]
    public async Task SuspiciousAccesses_IncludeBlockedPendingBtg_AndBreakTheGlass()
    {
        var (wf, pid, _) = await NewEmployeePatientAsync();

        await wf.AccessPatientAsync("STRANGER", "A Stranger", breakTheGlassAttested: false, justification: null);   // pending-BTG
        await wf.AccessPatientAsync("STRANGER", "A Stranger", breakTheGlassAttested: true, justification: "Reason.");// BTG access

        List<PatientAccessLog> suspicious = await Pac(pid).GetSuspiciousAccessesAsync();

        Assert.That(suspicious, Is.Not.Empty);
        Assert.That(suspicious, Has.Some.Matches<PatientAccessLog>(e => e.AccessReason == "BLOCKED_PENDING_BTG"));
        Assert.That(suspicious, Has.Some.Matches<PatientAccessLog>(e => e.WasBreakTheGlass));
    }

    // ─────────────────────────── Phase 5 — cascade-testing opportunities ───────────────────────────

    // Builds a mother (KAY) with a confirmed pathogenic germline BRCA1 variant, a child (KIM) whose
    // Mother family-history entry is LINKED to KAY's Person. Returns KAY's patient id + KIM's workflow.
    private async Task<(IPatientWorkflowGrain Kim, string KimId, IPatientWorkflowGrain Kay, string KayId)>
        BuildLinkedPathogenicRelativeAsync()
    {
        var (kay, kayId) = await NewPatientAsync("HEREDITY,KAY");
        var (kim, kimId) = await NewPatientAsync("HEREDITY,KIM");

        // KAY carries a pathogenic germline BRCA1 variant.
        string rid = await kay.RecordGeneticTestReportAsync("Hereditary Cancer Panel", "Invitae",
            GeneticTestMethod.NextGenSequencing, "hx", null, DateTime.UtcNow,
            GeneticReportResult.PositivePathogenic, "Dr", "", "TEST");
        await kay.AddGeneticVariantAsync(rid, "BRCA1", "c.68_69delAG", "p.Glu23ValfsTer17", "NM_007294.4",
            VariantClassification.Pathogenic, VariantZygosity.Heterozygous, VariantOrigin.Germline, "", "", "");

        // KIM records KAY as her Mother in family history.
        string mid = await kim.AddFamilyMemberAsync(FamilyRelationship.Mother, "KAY", "F",
            FamilyVitalStatus.Alive, 60, null, "", "");

        // Bootstrap KAY's Person and LINK the family entry to it (the anchor that makes cascade possible).
        string personKay = await kay.CreateOrGetPersonForPatientAsync("500",
            PersonLinkConfidence.ConfirmedByRegistration, "TEST");
        await kim.LinkFamilyMemberToPersonAsync(mid, personKay, "TEST");

        return (kim, kimId, kay, kayId);
    }

    [Test]
    public async Task Cascade_LinkedRelativeWithPathogenicVariant_SurfacesOpportunity()
    {
        var (kim, _, _, kayId) = await BuildLinkedPathogenicRelativeAsync();

        List<CascadeOpportunity> opportunities = await kim.GetCascadeOpportunitiesAsync();

        Assert.That(opportunities, Is.Not.Empty);
        CascadeOpportunity first = opportunities[0];
        Assert.That(first.Gene, Is.EqualTo("BRCA1"));
        Assert.That(first.RelativePatientId, Is.EqualTo(kayId));
        Assert.That(first.Relationship, Does.Contain("Mother"));
    }

    [Test]
    public async Task Cascade_UnlinkedRelative_NoOpportunity()
    {
        // KIM2 has a Mother family-history entry but it is NOT linked to any Person.
        var (kim2, _) = await NewPatientAsync("HEREDITY,KIM2");
        await kim2.AddFamilyMemberAsync(FamilyRelationship.Mother, "KAY", "F",
            FamilyVitalStatus.Alive, 60, null, "", "");

        List<CascadeOpportunity> opportunities = await kim2.GetCascadeOpportunitiesAsync();

        Assert.That(opportunities, Is.Empty);
    }

    [Test]
    public async Task Cascade_LinkedRelative_NoPathogenicVariant_NoOpportunity()
    {
        // The relative (MOM) is linked to a real chart/Person but has NO pathogenic variants.
        var (mom, momId) = await NewPatientAsync("HEREDITY,MOM");
        var (child, _) = await NewPatientAsync("HEREDITY,CHILD");

        string mid = await child.AddFamilyMemberAsync(FamilyRelationship.Mother, "MOM", "F",
            FamilyVitalStatus.Alive, 60, null, "", "");
        string personMom = await mom.CreateOrGetPersonForPatientAsync("500",
            PersonLinkConfidence.ConfirmedByRegistration, "TEST");
        await child.LinkFamilyMemberToPersonAsync(mid, personMom, "TEST");

        List<CascadeOpportunity> opportunities = await child.GetCascadeOpportunitiesAsync();

        Assert.That(opportunities, Is.Empty);
    }
}
