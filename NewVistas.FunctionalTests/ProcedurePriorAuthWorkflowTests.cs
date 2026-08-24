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
/// The medical/procedure prior-auth lifecycle and the payer×procedure requirements intelligence:
/// a denial feeds the learned KB shard and the "fill these boxes" checklist reranks accordingly.
/// (The SharedCluster does not run the AuthorizationCallFilter, so the security-key gate is verified
/// declaratively via the interface attribute, not here.)
/// </summary>
[TestFixture]
public class ProcedurePriorAuthWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientWorkflowGrain Wf(string patientId) => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private Task<string> SubmitTka(IPatientWorkflowGrain wf, string payerId, ProcedureAuthSubmissionChannel channel = ProcedureAuthSubmissionChannel.Phone)
        => wf.SubmitProcedureAuthAsync("27447", "Total knee arthroplasty", payerId, "Test Payer",
            "PROV-1", "Dr. Test", new List<string> { "M17.11" }, "OA knee", null, null, channel, null, null, null);

    [Test]
    public async Task Lifecycle_SubmitApprove_UpdatesStateAndIndex()
    {
        string patient = $"PPA-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Wf(patient);
        string payer = $"PAYER-{Guid.NewGuid():N}";

        string id = await SubmitTka(wf, payer, ProcedureAuthSubmissionChannel.PayerPortal);
        ProcedureAuthorizationState s = await wf.GetProcedureAuthAsync(id);
        Assert.That(s.Status, Is.EqualTo(ProcedureAuthorizationStatus.Submitted));
        Assert.That(s.CptCode, Is.EqualTo("27447"));
        Assert.That(s.PayerId, Is.EqualTo(payer.ToUpperInvariant()));
        Assert.That(s.TransmissionDetail, Is.Null); // manual channel → no transmitter

        await wf.ApproveProcedureAuthAsync(id, "UM-1", "UM Nurse", "AUTH-123", new DateTime(2026, 12, 31),
            new List<PriorAuthRequirementCategory> { PriorAuthRequirementCategory.ConservativeTherapyTrial });
        s = await wf.GetProcedureAuthAsync(id);
        Assert.That(s.Status, Is.EqualTo(ProcedureAuthorizationStatus.Approved));
        Assert.That(s.AuthorizationNumber, Is.EqualTo("AUTH-123"));

        List<ProcedureAuthIndexEntry> index = await wf.GetProcedureAuthsAsync();
        Assert.That(index.Single(e => e.ProcAuthId == id).Status, Is.EqualTo(ProcedureAuthorizationStatus.Approved));
    }

    [Test]
    public async Task PendCancelExpire_MoveStatusAndIndex_AndPendRecordsWhatWasAsked()
    {
        string patient = $"PPA-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Wf(patient);
        string payer = $"PAYER-{Guid.NewGuid():N}";

        // Pend: the payer asked for more information — the ask must be recorded verbatim,
        // because "pended" with no note is indistinguishable from "lost".
        string pended = await SubmitTka(wf, payer);
        await wf.PendProcedureAuthAsync(pended, "Operative report and 6-week PT notes");
        ProcedureAuthorizationState s = await wf.GetProcedureAuthAsync(pended);
        Assert.That(s.Status, Is.EqualTo(ProcedureAuthorizationStatus.Pended));
        Assert.That(s.PendedInfoRequested, Is.EqualTo("Operative report and 6-week PT notes"));

        // A pended request can still be approved once the information lands.
        await wf.ApproveProcedureAuthAsync(pended, "UM-1", "UM Nurse", "AUTH-77", null,
            new List<PriorAuthRequirementCategory>());
        s = await wf.GetProcedureAuthAsync(pended);
        Assert.That(s.Status, Is.EqualTo(ProcedureAuthorizationStatus.Approved));

        string cancelled = await SubmitTka(wf, payer);
        await wf.CancelProcedureAuthAsync(cancelled);
        Assert.That((await wf.GetProcedureAuthAsync(cancelled)).Status,
            Is.EqualTo(ProcedureAuthorizationStatus.Cancelled));

        string expired = await SubmitTka(wf, payer);
        await wf.ExpireProcedureAuthAsync(expired);
        Assert.That((await wf.GetProcedureAuthAsync(expired)).Status,
            Is.EqualTo(ProcedureAuthorizationStatus.Expired));

        // The per-patient index tracks every terminal state.
        List<ProcedureAuthIndexEntry> index = await wf.GetProcedureAuthsAsync();
        Assert.That(index.Single(e => e.ProcAuthId == pended).Status, Is.EqualTo(ProcedureAuthorizationStatus.Approved));
        Assert.That(index.Single(e => e.ProcAuthId == cancelled).Status, Is.EqualTo(ProcedureAuthorizationStatus.Cancelled));
        Assert.That(index.Single(e => e.ProcAuthId == expired).Status, Is.EqualTo(ProcedureAuthorizationStatus.Expired));
    }

    [Test]
    public async Task ElectronicChannel_RecordsTransmitterStandIn()
    {
        string patient = $"PPA-{Guid.NewGuid()}";
        string id = await SubmitTka(Wf(patient), $"PAYER-{Guid.NewGuid():N}", ProcedureAuthSubmissionChannel.Electronic);
        ProcedureAuthorizationState s = await Wf(patient).GetProcedureAuthAsync(id);
        Assert.That(s.TransmissionDetail, Is.Not.Null);
        Assert.That(s.TransmissionDetail, Does.Contain("278"));
    }

    [Test]
    public async Task Denial_FeedsLearnedKb_AndChecklistReranks()
    {
        string patient = $"PPA-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Wf(patient);
        // A payer id with hyphens exercises the last-colon key split of PAYER-PROC:{payerId}:{cpt}.
        string payer = "PAYER-BCBS-XX-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        // Cold start: baseline only.
        PriorAuthRequirementChecklist cold = await wf.GetPriorAuthRequirementsAsync("27447", payer);
        Assert.That(cold.IsColdStart, Is.True);

        // Submit + deny for conservative-therapy + imaging.
        string id = await SubmitTka(wf, payer);
        await wf.DenyProcedureAuthAsync(id, "UM-1", "UM Nurse", new List<ProcedureDenialReason>
        {
            new() { Category = PriorAuthRequirementCategory.ConservativeTherapyTrial, ReasonText = "no conservative therapy" },
            new() { Category = PriorAuthRequirementCategory.ImagingEvidence, ReasonText = "no films" }
        });

        // Now the checklist is learned and those categories rank to the top.
        PriorAuthRequirementChecklist after = await wf.GetPriorAuthRequirementsAsync("27447", payer);
        Assert.That(after.IsColdStart, Is.False);
        Assert.That(after.ObservedDenialTotal, Is.EqualTo(1));
        var topTwo = after.Items.Take(2).Select(i => i.Category).ToList();
        Assert.That(topTwo, Does.Contain(PriorAuthRequirementCategory.ConservativeTherapyTrial));
        Assert.That(topTwo, Does.Contain(PriorAuthRequirementCategory.ImagingEvidence));
        Assert.That(after.Items.First().DenialCount, Is.EqualTo(1));
        Assert.That(after.Items.First().Source, Is.EqualTo(RequirementSource.Both));
    }

    [Test]
    public async Task LearnedShard_DerivesPayerAndCptFromHyphenatedKey()
    {
        string patient = $"PPA-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Wf(patient);
        string payer = "PAYER-AETNA-FL-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        string id = await SubmitTka(wf, payer);
        await wf.DenyProcedureAuthAsync(id, "UM-1", "UM Nurse", new List<ProcedureDenialReason>
        {
            new() { Category = PriorAuthRequirementCategory.MedicalNecessityNarrative, ReasonText = "insufficient detail" }
        });

        var shard = _cluster.GrainFactory.GetGrain<IPayerProcedureRequirementIndexGrain>(
            $"PAYER-PROC:{payer.ToUpperInvariant()}:27447");
        PayerProcedureRequirementProfile profile = await shard.GetProfileAsync();
        Assert.That(profile.PayerId, Is.EqualTo(payer.ToUpperInvariant()));   // hyphens preserved, last colon split
        Assert.That(profile.CptCode, Is.EqualTo("27447"));
        Assert.That(profile.TotalDenials, Is.EqualTo(1));
    }
}
