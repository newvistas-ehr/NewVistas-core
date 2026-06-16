// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Claim Status Inquiry — EDI 276/277 (IBCSC*.m).
/// </summary>
[TestFixture]
public class ClaimStatusInquiryWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IClaimStatusInquiryGrain NewInquiry()
        => _cluster.GrainFactory.GetGrain<IClaimStatusInquiryGrain>($"CSI-276:{Guid.NewGuid()}");

    private IClaimStatusInquiryIndexGrain GetIndex(string patientId)
        => _cluster.GrainFactory.GetGrain<IClaimStatusInquiryIndexGrain>($"CSI-IDX:{patientId}");

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Grain Level ─────────────────────────────────────────────────────────

    [Test]
    public async Task CreateInquiry_SetsDraftStatus()
    {
        IClaimStatusInquiryGrain inq = NewInquiry();
        await inq.CreateAsync("PAT-CSI-001", "CLM-001", "PAYER-001", "Medicare", 500m, "USR-1", "Test", null);
        ClaimStatusInquiryState state = await inq.GetAsync();
        Assert.That(state.Status, Is.EqualTo(ClaimStatusInquiryStatus.Draft));
        Assert.That(state.ClaimId, Is.EqualTo("CLM-001"));
    }

    [Test]
    public async Task SubmitInquiry_SetsSubmittedStatus()
    {
        IClaimStatusInquiryGrain inq = NewInquiry();
        await inq.CreateAsync("PAT-CSI-002", "CLM-002", "PAYER-002", "BCBS", 200m, null, null, null);
        await inq.SubmitAsync("TN-CSI-001");
        ClaimStatusInquiryState state = await inq.GetAsync();
        Assert.That(state.Status, Is.EqualTo(ClaimStatusInquiryStatus.Submitted));
        Assert.That(state.TraceNumber, Is.EqualTo("TN-CSI-001"));
    }

    [Test]
    public async Task RecordResponse_SetsResponseReceivedStatus()
    {
        IClaimStatusInquiryGrain inq = NewInquiry();
        await inq.CreateAsync("PAT-CSI-003", "CLM-003", "PAYER-003", "Aetna", 300m, null, null, null);
        await inq.SubmitAsync("TN-002");
        await inq.RecordResponseAsync(
            ClaimStatusCategory.Finalized,
            new List<ClaimStatusDetail>
            {
                new() { StatusCategoryCode = "F3", StatusCategoryDescription = "Finalized — payment", PaymentAmount = 280m }
            },
            280m, "Claim paid.");

        ClaimStatusInquiryState state = await inq.GetAsync();
        Assert.That(state.Status, Is.EqualTo(ClaimStatusInquiryStatus.ResponseReceived));
        Assert.That(state.ClaimStatusCategory, Is.EqualTo(ClaimStatusCategory.Finalized));
        Assert.That(state.ReportedPaidAmount, Is.EqualTo(280m));
    }

    [Test]
    public async Task RecordResponse_StoresStatusDetails()
    {
        IClaimStatusInquiryGrain inq = NewInquiry();
        await inq.CreateAsync("PAT-CSI-004", "CLM-004", "PAYER-004", "Cigna", 150m, null, null, null);
        await inq.SubmitAsync("TN-003");

        List<ClaimStatusDetail> details = new()
        {
            new() { StatusCategoryCode = "P1", StatusCategoryDescription = "In Process", TotalChargeAmount = 150m },
            new() { StatusCategoryCode = "R1", StatusCategoryDescription = "Additional info requested", Message = "Need medical records" },
        };
        await inq.RecordResponseAsync(ClaimStatusCategory.AdditionalInfoRequested, details, null, "Multiple statuses.");

        ClaimStatusInquiryState state = await inq.GetAsync();
        Assert.That(state.StatusDetails, Has.Count.EqualTo(2));
        Assert.That(state.StatusDetails[1].Message, Does.Contain("medical records"));
    }

    [Test]
    public async Task RecordError_SetsErrorStatus()
    {
        IClaimStatusInquiryGrain inq = NewInquiry();
        await inq.CreateAsync("PAT-CSI-005", "CLM-005", "PAYER-005", "United", 400m, null, null, null);
        await inq.SubmitAsync("TN-004");
        await inq.RecordErrorAsync(new List<string> { "42", "N5" }, "Subscriber not found.");

        ClaimStatusInquiryState state = await inq.GetAsync();
        Assert.That(state.Status, Is.EqualTo(ClaimStatusInquiryStatus.Error));
        Assert.That(state.RejectReasonCodes, Has.Count.EqualTo(2));
    }

    // ─── Index ───────────────────────────────────────────────────────────────

    [Test]
    public async Task Index_AddOrUpdate_AppearsInGetAll()
    {
        string patientId = $"PAT-CSI-IDX-{Guid.NewGuid()}";
        IClaimStatusInquiryIndexGrain index = GetIndex(patientId);
        string inquiryId = Guid.NewGuid().ToString();
        await index.AddOrUpdateAsync(new ClaimStatusInquiryIndexEntry
        {
            InquiryId = inquiryId, ClaimId = "CLM-IDX-1", PayerName = "Medicare",
            Status = ClaimStatusInquiryStatus.ResponseReceived, ClaimStatusCategory = ClaimStatusCategory.Pending,
        });
        List<ClaimStatusInquiryIndexEntry> all = await index.GetAllAsync();
        Assert.That(all.Any(e => e.InquiryId == inquiryId), Is.True);
    }

    [Test]
    public async Task Index_GetByClaim_FiltersCorrectly()
    {
        string patientId = $"PAT-CSI-CLM-{Guid.NewGuid()}";
        IClaimStatusInquiryIndexGrain index = GetIndex(patientId);
        await index.AddOrUpdateAsync(new ClaimStatusInquiryIndexEntry { InquiryId = "I1", ClaimId = "CLM-A", PayerName = "P1", Status = ClaimStatusInquiryStatus.ResponseReceived });
        await index.AddOrUpdateAsync(new ClaimStatusInquiryIndexEntry { InquiryId = "I2", ClaimId = "CLM-B", PayerName = "P2", Status = ClaimStatusInquiryStatus.ResponseReceived });
        await index.AddOrUpdateAsync(new ClaimStatusInquiryIndexEntry { InquiryId = "I3", ClaimId = "CLM-A", PayerName = "P1", Status = ClaimStatusInquiryStatus.ResponseReceived });

        List<ClaimStatusInquiryIndexEntry> forA = await index.GetByClaimAsync("CLM-A");
        Assert.That(forA, Has.Count.EqualTo(2));
    }

    // ─── Workflow ────────────────────────────────────────────────────────────

    [Test]
    public async Task Workflow_SubmitClaimStatusInquiry_ReturnsId()
    {
        string patientId = $"PAT-CSI-WF-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        // First create an EDI claim for this patient
        IEdiClaimGrain claim = _cluster.GrainFactory.GetGrain<IEdiClaimGrain>($"EDI-CLAIM:CSI-TEST-{Guid.NewGuid()}");
        string claimId = await claim.PrepareAsync(
            patientId, null, null, null, null,
            "PAYER-MED", "Medicare", EdiClaimType.Professional837P,
            500m, new List<string> { "J11.1" },
            new List<EdiServiceLine> { new() { LineNumber = 1, ProcedureCode = "99213", BilledAmount = 500m } },
            DateTime.UtcNow, null, null);

        string inquiryId = await wf.SubmitClaimStatusInquiryAsync(
            claimId, "PAYER-MED", "Medicare", "USR-WF", "WF", null);

        Assert.That(inquiryId, Is.Not.Null.And.Not.Empty);
        ClaimStatusInquiryState state = await wf.GetClaimStatusInquiryAsync(inquiryId);
        Assert.That(state.Status, Is.EqualTo(ClaimStatusInquiryStatus.ResponseReceived));
    }

    [Test]
    public async Task Workflow_SubmitInquiry_UpdatesIndex()
    {
        string patientId = $"PAT-CSI-WF2-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        IEdiClaimGrain claim = _cluster.GrainFactory.GetGrain<IEdiClaimGrain>($"EDI-CLAIM:CSI-TEST2-{Guid.NewGuid()}");
        string claimId = await claim.PrepareAsync(
            patientId, null, null, null, null,
            "PAYER-BCBS", "BCBS", EdiClaimType.Professional837P,
            200m, new List<string>(), new List<EdiServiceLine>(), DateTime.UtcNow, null, null);

        await wf.SubmitClaimStatusInquiryAsync(claimId, "PAYER-BCBS", "BCBS", null, null, null);

        List<ClaimStatusInquiryIndexEntry> inquiries = await wf.GetClaimStatusInquiriesAsync();
        Assert.That(inquiries, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Workflow_GetByClaimId_ReturnsFiltered()
    {
        string patientId = $"PAT-CSI-WF3-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        IEdiClaimGrain claim1 = _cluster.GrainFactory.GetGrain<IEdiClaimGrain>($"EDI-CLAIM:CSI-A-{Guid.NewGuid()}");
        string claimId1 = await claim1.PrepareAsync(patientId, null, null, null, null, "P1", "Payer1", EdiClaimType.Professional837P, 100m, new List<string>(), new List<EdiServiceLine>(), DateTime.UtcNow, null, null);

        IEdiClaimGrain claim2 = _cluster.GrainFactory.GetGrain<IEdiClaimGrain>($"EDI-CLAIM:CSI-B-{Guid.NewGuid()}");
        string claimId2 = await claim2.PrepareAsync(patientId, null, null, null, null, "P2", "Payer2", EdiClaimType.Professional837P, 200m, new List<string>(), new List<EdiServiceLine>(), DateTime.UtcNow, null, null);

        await wf.SubmitClaimStatusInquiryAsync(claimId1, "P1", "Payer1", null, null, null);
        await wf.SubmitClaimStatusInquiryAsync(claimId2, "P2", "Payer2", null, null, null);

        List<ClaimStatusInquiryIndexEntry> forClaim1 = await wf.GetClaimStatusInquiriesByClaimAsync(claimId1);
        Assert.That(forClaim1, Has.Count.EqualTo(1));
        Assert.That(forClaim1[0].PayerName, Is.EqualTo("Payer1"));
    }

    // ─── Response Categories ─────────────────────────────────────────────────

    [Test]
    public async Task Response_DeniedClaim_SetsDeniedCategory()
    {
        IClaimStatusInquiryGrain inq = NewInquiry();
        await inq.CreateAsync("PAT-CSI-DEN", "CLM-DEN", "P-DEN", "Payer", 500m, null, null, null);
        await inq.SubmitAsync("TN-DEN");
        await inq.RecordResponseAsync(ClaimStatusCategory.Denied, new List<ClaimStatusDetail>(), null, "Claim denied.");

        ClaimStatusInquiryState state = await inq.GetAsync();
        Assert.That(state.ClaimStatusCategory, Is.EqualTo(ClaimStatusCategory.Denied));
    }

    [Test]
    public async Task Response_RejectedClaim_SetsRejectedCategory()
    {
        IClaimStatusInquiryGrain inq = NewInquiry();
        await inq.CreateAsync("PAT-CSI-REJ", "CLM-REJ", "P-REJ", "Payer", 300m, null, null, null);
        await inq.SubmitAsync("TN-REJ");
        await inq.RecordResponseAsync(ClaimStatusCategory.Rejected, new List<ClaimStatusDetail>(), null, "Invalid data.");

        ClaimStatusInquiryState state = await inq.GetAsync();
        Assert.That(state.ClaimStatusCategory, Is.EqualTo(ClaimStatusCategory.Rejected));
    }

    [Test]
    public async Task Response_PendingClaim_SetsPendingCategory()
    {
        IClaimStatusInquiryGrain inq = NewInquiry();
        await inq.CreateAsync("PAT-CSI-PND", "CLM-PND", "P-PND", "Payer", 250m, null, null, null);
        await inq.SubmitAsync("TN-PND");
        await inq.RecordResponseAsync(ClaimStatusCategory.Pending,
            new List<ClaimStatusDetail> { new() { StatusCategoryCode = "P1", StatusCategoryDescription = "In adjudication" } },
            null, "Under review.");

        ClaimStatusInquiryState state = await inq.GetAsync();
        Assert.That(state.ClaimStatusCategory, Is.EqualTo(ClaimStatusCategory.Pending));
        Assert.That(state.StatusDetails, Has.Count.EqualTo(1));
    }
}
