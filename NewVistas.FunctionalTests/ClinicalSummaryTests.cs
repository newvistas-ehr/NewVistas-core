// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Verification is the guard that stops a fluent-but-wrong summary from being trusted.
/// These are pure tests — no cluster — over the structural grounding check.
/// </summary>
[TestFixture]
public class ClinicalSummaryVerifierTests
{
    private static ClinicalSummaryContext ContextWith(params string[] factIds) => new()
    {
        PatientId = "P",
        Facts = factIds.Select(id => new ClinicalFact { FactId = id, Text = id }).ToList(),
    };

    [Test]
    public void Verify_PassesClaimGroundedInKnownFacts()
    {
        ClinicalSummaryContext ctx = ContextWith("F1", "F2");
        SummaryClaim claim = new() { Text = "x", SupportingFactIds = ["F1", "F2"] };

        int flagged = ClinicalSummaryVerifier.Verify(ctx, [claim]);

        Assert.That(flagged, Is.EqualTo(0));
        Assert.That(claim.Verified, Is.True);
        Assert.That(claim.VerificationNote, Is.Null);
    }

    [Test]
    public void Verify_FlagsClaimCitingAFactNotInTheRecord()
    {
        // The model invented a citation — exactly the hallucination this catches.
        ClinicalSummaryContext ctx = ContextWith("F1");
        SummaryClaim claim = new() { Text = "patient on drug Z", SupportingFactIds = ["F9"] };

        int flagged = ClinicalSummaryVerifier.Verify(ctx, [claim]);

        Assert.That(flagged, Is.EqualTo(1));
        Assert.That(claim.Verified, Is.False);
        Assert.That(claim.VerificationNote, Does.Contain("F9"));
    }

    [Test]
    public void Verify_FlagsUngroundedClaimThatCitesNothing()
    {
        ClinicalSummaryContext ctx = ContextWith("F1");
        SummaryClaim claim = new() { Text = "free-floating assertion", SupportingFactIds = [] };

        int flagged = ClinicalSummaryVerifier.Verify(ctx, [claim]);

        Assert.That(flagged, Is.EqualTo(1));
        Assert.That(claim.Verified, Is.False);
        Assert.That(claim.VerificationNote, Does.Contain("Ungrounded"));
    }
}

/// <summary>
/// End-to-end: the per-patient summary grain grounds in real chart data, produces a
/// verified draft, and gates on clinician sign-off.
/// </summary>
[TestFixture]
public class PatientSummaryGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientWorkflowGrain Workflow(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPatientSummaryGrain Summary(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientSummaryGrain>(patientId);

    [Test]
    public async Task Generate_GroundsInPatientData_AllClaimsVerified_PendingSignoff()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";

        // An active medication...
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>($"RX-{Guid.NewGuid()}");
        await rx.CreatePrescriptionAsync(
            patientId, "LISINOPRIL 10MG", $"DRUG-{Guid.NewGuid()}", "10mg", "ORAL", "QD", "Take once daily",
            30, 30, 3, null, null, null, null, null, null);

        // ...and a documented allergy.
        await Workflow(patientId).RecordAllergyAsync(
            "PENICILLIN", "DRUG", null, "OBSERVED", ["HIVES"], "SEVERE", "PROV-1", "Dr. A", null);

        ClinicalSummaryDraft draft = await Summary(patientId).GenerateAsync("pre-op review");

        // Grounded in discrete chart data, with provenance.
        Assert.That(draft.ModelProvider, Is.EqualTo("offline-template"));
        Assert.That(draft.GroundingFacts.Any(f =>
            f.Category == ClinicalFactCategory.Medication && f.Text.Contains("LISINOPRIL")), Is.True);
        Assert.That(draft.GroundingFacts.Any(f =>
            f.Category == ClinicalFactCategory.Allergy && f.Text.Contains("PENICILLIN")), Is.True);

        // Every claim traces to a source fact — nothing ungrounded survives.
        Assert.That(draft.Claims, Is.Not.Empty);
        Assert.That(draft.Claims.All(c => c.Verified), Is.True);
        Assert.That(draft.UnverifiedClaimCount, Is.EqualTo(0));

        // The narrative actually reflects the patient's data.
        Assert.That(draft.Narrative, Does.Contain("LISINOPRIL"));
        Assert.That(draft.Narrative, Does.Contain("PENICILLIN"));

        // It's a draft — not trusted until a clinician signs.
        Assert.That(draft.Status, Is.EqualTo(SummaryStatus.DraftPendingSignoff));
    }

    [Test]
    public async Task SignOff_MarksDraftSigned()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        await Workflow(patientId).RecordAllergyAsync(
            "SULFA", "DRUG", null, "HISTORICAL", ["RASH"], "MODERATE", "PROV-1", "Dr. A", null);

        await Summary(patientId).GenerateAsync("annual review");
        await Summary(patientId).SignOffAsync("DR-SMITH");

        ClinicalSummaryDraft? draft = await Summary(patientId).GetCurrentDraftAsync();
        Assert.That(draft, Is.Not.Null);
        Assert.That(draft!.Status, Is.EqualTo(SummaryStatus.Signed));
        Assert.That(draft.SignedBy, Is.EqualTo("DR-SMITH"));
        Assert.That(draft.SignedDate, Is.Not.Null);
    }

    [Test]
    public void SignOff_WithNoDraft_Throws()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        Assert.That(async () => await Summary(patientId).SignOffAsync("DR-1"),
            Throws.InvalidOperationException);
    }
}
