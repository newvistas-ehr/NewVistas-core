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
/// Functional tests for VistA EDI / Electronic Billing — Files #361, #361.1, #364.
/// Tests the full chain: IB Billing Action → EDI Claim → Transmission → ERA → AR Payment.
/// </summary>
[TestFixture]
public class EdiWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IEdiClaimGrain NewClaim()
        => _cluster.GrainFactory.GetGrain<IEdiClaimGrain>($"EDI-CLAIM:{Guid.NewGuid()}");

    private IEdiTransmissionGrain NewTransmission()
        => _cluster.GrainFactory.GetGrain<IEdiTransmissionGrain>($"EDI-TX:{Guid.NewGuid()}");

    private IEraGrain NewEra()
        => _cluster.GrainFactory.GetGrain<IEraGrain>($"ERA:{Guid.NewGuid()}");

    private IIBillingActionGrain NewIBAction()
        => _cluster.GrainFactory.GetGrain<IIBillingActionGrain>($"IB-ACTION:{Guid.NewGuid()}");

    private IARAccountGrain NewARAccount()
        => _cluster.GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{Guid.NewGuid()}");

    private List<EdiServiceLine> DefaultServiceLines() => new()
    {
        new EdiServiceLine
        {
            LineNumber = 1,
            ProcedureCode = "99213",
            BilledAmount = 150m,
            Units = 1m,
            DiagnosisPointer = "1"
        }
    };

    // ─── EDI Claim Preparation ────────────────────────────────────────────

    [Test]
    public async Task PrepareEdiClaim_SetsDraftStatus()
    {
        IEdiClaimGrain claim = NewClaim();
        await claim.PrepareAsync(
            "PAT-EDI-001", null, null, null, null,
            "PAYER-001", "Blue Cross", EdiClaimType.Professional837P,
            150m, new List<string> { "J11.1" }, DefaultServiceLines(),
            DateTime.UtcNow, null, null);

        EdiClaimState state = await claim.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EdiClaimStatus.Draft));
        Assert.That(state.TotalBilledAmount, Is.EqualTo(150m));
    }

    [Test]
    public async Task PrepareEdiClaim_StoresPayerAndPatient()
    {
        IEdiClaimGrain claim = NewClaim();
        await claim.PrepareAsync(
            "PAT-EDI-002", null, null, null, null,
            "PAYER-002", "United Health", EdiClaimType.Institutional837I,
            300m, new List<string> { "I10" }, DefaultServiceLines(),
            DateTime.UtcNow, null, null);

        EdiClaimState state = await claim.GetAsync();
        Assert.That(state.PayerId, Is.EqualTo("PAYER-002"));
        Assert.That(state.PayerName, Is.EqualTo("United Health"));
        Assert.That(state.PatientId, Is.EqualTo("PAT-EDI-002"));
        Assert.That(state.ClaimType, Is.EqualTo(EdiClaimType.Institutional837I));
    }

    [Test]
    public async Task PrepareEdiClaim_WithBillingAction_UpdatesIBActionToBilled()
    {
        // Arrange: EdiClaimGrain prepends "IB-ACTION:" to billingActionId internally,
        // so pass the raw GUID and reference the grain with the full prefixed key.
        string rawIbActionId = Guid.NewGuid().ToString();
        IIBillingActionGrain ibAction = _cluster.GrainFactory.GetGrain<IIBillingActionGrain>($"IB-ACTION:{rawIbActionId}");
        await ibAction.CreateAsync(
            "PAT-EDI-003", "COPAY-OP", "Outpatient Copay",
            IBActionCategory.Outpatient, 150m,
            DateTime.UtcNow, "USR-001", "Test User",
            null, null, null, null, null, null, null);

        // Act: prepare EDI claim linked to that IB action (pass raw ID, grain prepends prefix)
        IEdiClaimGrain claim = NewClaim();
        await claim.PrepareAsync(
            "PAT-EDI-003", rawIbActionId, null, null, null,
            "PAYER-001", "Blue Cross", EdiClaimType.Professional837P,
            150m, new List<string> { "J11.1" }, DefaultServiceLines(),
            DateTime.UtcNow, null, null);

        // Assert: IB action status updated to Billed
        IBillingActionState ibState = await ibAction.GetAsync();
        Assert.That(ibState.Status, Is.EqualTo(IBillingActionStatus.Billed));
    }

    // ─── Claim Lifecycle ──────────────────────────────────────────────────

    [Test]
    public async Task AddToTransmission_SetsInTransmissionStatus()
    {
        IEdiClaimGrain claim = NewClaim();
        await claim.PrepareAsync(
            "PAT-EDI-010", null, null, null, null,
            "PAYER-001", "Blue Cross", EdiClaimType.Professional837P,
            150m, new List<string>(), DefaultServiceLines(),
            DateTime.UtcNow, null, null);

        await claim.AddToTransmissionAsync("EDI-TX-001");

        EdiClaimState state = await claim.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EdiClaimStatus.InTransmission));
        Assert.That(state.TransmissionId, Is.EqualTo("EDI-TX-001"));
    }

    [Test]
    public async Task MarkTransmitted_SetsTransmittedStatus()
    {
        IEdiClaimGrain claim = NewClaim();
        await claim.PrepareAsync(
            "PAT-EDI-011", null, null, null, null,
            "PAYER-001", "Blue Cross", EdiClaimType.Professional837P,
            150m, new List<string>(), DefaultServiceLines(),
            DateTime.UtcNow, null, null);
        await claim.AddToTransmissionAsync("EDI-TX-001");

        await claim.MarkTransmittedAsync(DateTime.UtcNow);

        EdiClaimState state = await claim.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EdiClaimStatus.Transmitted));
        Assert.That(state.SubmittedDate, Is.Not.Null);
    }

    [Test]
    public async Task RecordAcknowledgment_SetsAcknowledgedStatus()
    {
        IEdiClaimGrain claim = NewClaim();
        await claim.PrepareAsync(
            "PAT-EDI-012", null, null, null, null,
            "PAYER-001", "Blue Cross", EdiClaimType.Professional837P,
            150m, new List<string>(), DefaultServiceLines(),
            DateTime.UtcNow, null, null);
        await claim.AddToTransmissionAsync("EDI-TX-001");
        await claim.MarkTransmittedAsync(DateTime.UtcNow);

        await claim.RecordAcknowledgmentAsync(DateTime.UtcNow);

        EdiClaimState state = await claim.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EdiClaimStatus.Acknowledged));
    }

    [Test]
    public async Task RecordEraPayment_SetsPaidStatus()
    {
        IEdiClaimGrain claim = NewClaim();
        await claim.PrepareAsync(
            "PAT-EDI-020", null, null, null, null,
            "PAYER-001", "Blue Cross", EdiClaimType.Professional837P,
            150m, new List<string>(), DefaultServiceLines(),
            DateTime.UtcNow, null, null);

        await claim.RecordEraPaymentAsync("ERA-001", 135m, 150m, 15m, null, null);

        EdiClaimState state = await claim.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EdiClaimStatus.Paid));
        Assert.That(state.PaidAmount, Is.EqualTo(135m));
    }

    [Test]
    public async Task RecordEraPayment_ZeroAmount_SetsDeniedStatus()
    {
        IEdiClaimGrain claim = NewClaim();
        await claim.PrepareAsync(
            "PAT-EDI-021", null, null, null, null,
            "PAYER-001", "Blue Cross", EdiClaimType.Professional837P,
            150m, new List<string>(), DefaultServiceLines(),
            DateTime.UtcNow, null, null);

        await claim.RecordEraPaymentAsync("ERA-002", 0m, null, null, "CO-96", "Non-covered service");

        EdiClaimState state = await claim.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EdiClaimStatus.Denied));
    }

    [Test]
    public async Task RecordEraPayment_WithARAccount_PostsPaymentToAR()
    {
        // Arrange: EdiClaimGrain prepends "AR-ACCOUNT:" to arAccountId internally,
        // so pass the raw GUID and reference the grain with the full prefixed key.
        string rawArId = Guid.NewGuid().ToString();
        IARAccountGrain arAcct = _cluster.GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{rawArId}");
        await arAcct.CreateAsync("PAT-EDI-022", null, ARAccountCategory.CopayOutpatient, 150m, null);

        IEdiClaimGrain claim = NewClaim();
        await claim.PrepareAsync(
            "PAT-EDI-022", null, rawArId, null, null,
            "PAYER-001", "Blue Cross", EdiClaimType.Professional837P,
            150m, new List<string>(), DefaultServiceLines(),
            DateTime.UtcNow, null, null);

        // Act: record ERA payment
        await claim.RecordEraPaymentAsync("ERA-003", 150m, 150m, 0m, null, null);

        // Assert: AR account balance reduced
        ARAccountState arState = await arAcct.GetAsync();
        Assert.That(arState.CurrentBalance, Is.EqualTo(0m));
        Assert.That(arState.ARStatus, Is.EqualTo(ARAccountStatus.Paid));
    }

    // ─── Transmission Batch ───────────────────────────────────────────────

    [Test]
    public async Task OpenTransmission_SetsOpenStatus()
    {
        IEdiTransmissionGrain tx = NewTransmission();
        await tx.OpenAsync("BATCH-2024-001", "PAYER-001", "Blue Cross", null);

        EdiTransmissionState state = await tx.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EdiTransmissionStatus.Open));
        Assert.That(state.BatchNumber, Is.EqualTo("BATCH-2024-001"));
    }

    [Test]
    public async Task AddClaimToTransmission_IncrementsTotalClaims()
    {
        IEdiTransmissionGrain tx = NewTransmission();
        await tx.OpenAsync("BATCH-2024-002", "PAYER-001", "Blue Cross", null);

        await tx.AddClaimAsync("EDI-CLAIM-A", "PAT-001", 150m, "Professional");
        await tx.AddClaimAsync("EDI-CLAIM-B", "PAT-002", 300m, "Professional");

        EdiTransmissionState state = await tx.GetAsync();
        Assert.That(state.TotalClaims, Is.EqualTo(2));
        Assert.That(state.TotalBilledAmount, Is.EqualTo(450m));
    }

    [Test]
    public async Task SendTransmission_SetsSentStatus()
    {
        IEdiTransmissionGrain tx = NewTransmission();
        await tx.OpenAsync("BATCH-2024-003", "PAYER-001", "Blue Cross", null);
        await tx.AddClaimAsync("EDI-CLAIM-C", "PAT-003", 100m, "Professional");

        await tx.SendAsync(DateTime.UtcNow);

        EdiTransmissionState state = await tx.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EdiTransmissionStatus.Sent));
        Assert.That(state.SentDate, Is.Not.Null);
    }

    [Test]
    public async Task RecordAcknowledgment_AllAccepted_SetsAcceptedStatus()
    {
        IEdiTransmissionGrain tx = NewTransmission();
        await tx.OpenAsync("BATCH-2024-004", "PAYER-001", "Blue Cross", null);
        await tx.AddClaimAsync("EDI-CLAIM-D", "PAT-004", 100m, "Professional");
        await tx.SendAsync(DateTime.UtcNow);

        await tx.RecordAcknowledgmentAsync("AA", 1, 0, DateTime.UtcNow);

        EdiTransmissionState state = await tx.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EdiTransmissionStatus.Accepted));
    }

    [Test]
    public async Task RecordAcknowledgment_AllRejected_SetsRejectedStatus()
    {
        IEdiTransmissionGrain tx = NewTransmission();
        await tx.OpenAsync("BATCH-2024-005", "PAYER-001", "Blue Cross", null);
        await tx.AddClaimAsync("EDI-CLAIM-E", "PAT-005", 100m, "Professional");
        await tx.SendAsync(DateTime.UtcNow);

        await tx.RecordAcknowledgmentAsync("TA1", 0, 1, DateTime.UtcNow);

        EdiTransmissionState state = await tx.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EdiTransmissionStatus.Rejected));
    }

    [Test]
    public async Task RecordAcknowledgment_Mixed_SetsPartiallyAcceptedStatus()
    {
        IEdiTransmissionGrain tx = NewTransmission();
        await tx.OpenAsync("BATCH-2024-006", "PAYER-001", "Blue Cross", null);
        await tx.AddClaimAsync("EDI-CLAIM-F", "PAT-006", 100m, "Professional");
        await tx.AddClaimAsync("EDI-CLAIM-G", "PAT-007", 150m, "Professional");
        await tx.SendAsync(DateTime.UtcNow);

        await tx.RecordAcknowledgmentAsync("AA", 1, 1, DateTime.UtcNow);

        EdiTransmissionState state = await tx.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EdiTransmissionStatus.PartiallyAccepted));
    }

    // ─── ERA Processing (full chain) ─────────────────────────────────────

    [Test]
    public async Task RecordERA_SetsReceivedStatus()
    {
        IEraGrain era = NewEra();
        await era.RecordAsync(
            "PAYER-001", "Blue Cross", "CHK-ERA-001", "Check",
            DateTime.UtcNow, 500m, "TXN-SET-001",
            new List<EraClaimPayment>(), null);

        EraState state = await era.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EraStatus.Received));
        Assert.That(state.TotalPaymentAmount, Is.EqualTo(500m));
    }

    [Test]
    public async Task ProcessERA_WithLinkedClaims_PostsPaymentsToAllClaims()
    {
        // Arrange: EdiClaimGrain prepends "AR-ACCOUNT:" to arAccountId internally;
        // EraGrain prepends "EDI-CLAIM:" to cp.ClaimId internally.
        // Pass raw GUIDs to the grain methods and reference grains with full prefixed keys.
        string rawArId1 = Guid.NewGuid().ToString();
        IARAccountGrain ar1 = _cluster.GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{rawArId1}");
        await ar1.CreateAsync("PAT-ERA-001", null, ARAccountCategory.CopayOutpatient, 200m, null);

        string rawArId2 = Guid.NewGuid().ToString();
        IARAccountGrain ar2 = _cluster.GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{rawArId2}");
        await ar2.CreateAsync("PAT-ERA-002", null, ARAccountCategory.CopayOutpatient, 300m, null);

        string rawClaimId1 = Guid.NewGuid().ToString();
        IEdiClaimGrain claim1 = _cluster.GrainFactory.GetGrain<IEdiClaimGrain>($"EDI-CLAIM:{rawClaimId1}");
        await claim1.PrepareAsync(
            "PAT-ERA-001", null, rawArId1, null, null,
            "PAYER-001", "Blue Cross", EdiClaimType.Professional837P,
            200m, new List<string>(), DefaultServiceLines(),
            DateTime.UtcNow, null, null);

        string rawClaimId2 = Guid.NewGuid().ToString();
        IEdiClaimGrain claim2 = _cluster.GrainFactory.GetGrain<IEdiClaimGrain>($"EDI-CLAIM:{rawClaimId2}");
        await claim2.PrepareAsync(
            "PAT-ERA-002", null, rawArId2, null, null,
            "PAYER-001", "Blue Cross", EdiClaimType.Professional837P,
            300m, new List<string>(), DefaultServiceLines(),
            DateTime.UtcNow, null, null);

        // Create ERA with payments for both claims (ClaimId is the raw GUID; EraGrain prepends prefix)
        IEraGrain era = NewEra();
        List<EraClaimPayment> payments = new()
        {
            new EraClaimPayment
            {
                ClaimId = rawClaimId1,
                PatientId = "PAT-ERA-001",
                PaidAmount = 200m,
                AllowedAmount = 200m,
                AdjustmentAmount = 0m
            },
            new EraClaimPayment
            {
                ClaimId = rawClaimId2,
                PatientId = "PAT-ERA-002",
                PaidAmount = 300m,
                AllowedAmount = 300m,
                AdjustmentAmount = 0m
            }
        };

        await era.RecordAsync(
            "PAYER-001", "Blue Cross", "CHK-ERA-CHAIN", "Check",
            DateTime.UtcNow, 500m, "TXN-SET-CHAIN",
            payments, null);

        // Act: process the ERA
        await era.ProcessAsync();

        // Assert: ERA posted
        EraState eraState = await era.GetAsync();
        Assert.That(eraState.Status, Is.EqualTo(EraStatus.Posted));

        // Assert: both AR accounts paid
        ARAccountState ar1State = await ar1.GetAsync();
        Assert.That(ar1State.ARStatus, Is.EqualTo(ARAccountStatus.Paid));

        ARAccountState ar2State = await ar2.GetAsync();
        Assert.That(ar2State.ARStatus, Is.EqualTo(ARAccountStatus.Paid));
    }
}
