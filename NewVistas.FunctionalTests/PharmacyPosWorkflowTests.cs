// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Pharmacy Point of Sale — RPMS ABSP (File #9002313).
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/> with
/// Site Flavor Architecture (Option 4 — Composition) feature gate.
/// </summary>
[TestFixture]
public class PharmacyPosWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private ISiteParametersGrain GetSiteParams()
        => _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");

    [SetUp]
    public async Task SetUp()
    {
        await GetSiteParams().EnableFeatureAsync("PHARMACY_POS");
    }

    // ── Submit ───────────────────────────────────────────────────────────────

    [Test]
    public async Task SubmitClaim_ReturnsId_AndAppearsInIndex()
    {
        // Arrange
        string patientId = $"POS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        string claimId = await wf.SubmitPosClaimAsync(
            "RX-001", NcpdpTransactionType.B1,
            "999999", "PROC01", "D0",
            "GRP-100", "CARD-5678", "1",
            "INS-001", "TRICARE",
            "12345678901", "Lisinopril 10mg", 30m, 30,
            DateTime.UtcNow.Date,
            150.00m, 2.50m, 175.00m,
            null, null, null, null, null);

        List<PosClaimIndexEntry> claims = await wf.GetPosClaimsAsync();

        // Assert
        Assert.That(claimId, Is.Not.Null.And.Not.Empty);
        Assert.That(claims, Has.Count.EqualTo(1));
        Assert.That(claims[0].ClaimId, Is.EqualTo(claimId));
        Assert.That(claims[0].Status, Is.EqualTo(PosClaimStatus.Pending));
    }

    [Test]
    public async Task SubmitClaim_B1Billing_FullNcpdpFields()
    {
        // Arrange
        string patientId = $"POS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        string claimId = await wf.SubmitPosClaimAsync(
            "RX-002", NcpdpTransactionType.B1,
            "610014", "PROC", "D0",
            "GRP-200", "CARD-9999", "1",
            "INS-002", "Medicaid",
            "98765432100", "Metformin 500mg", 60m, 30,
            DateTime.UtcNow.Date,
            45.00m, 1.75m, 55.00m,
            "NCPDP-001", "Smith, RPh",
            "1234567890", "Jones, MD",
            null);

        PharmacyPosClaimState detail = await wf.GetPosClaimAsync(claimId);

        // Assert
        Assert.That(detail.PatientId, Is.EqualTo(patientId));
        Assert.That(detail.TransactionType, Is.EqualTo(NcpdpTransactionType.B1));
        Assert.That(detail.Bin, Is.EqualTo("610014"));
        Assert.That(detail.Pcn, Is.EqualTo("PROC"));
        Assert.That(detail.Ndc, Is.EqualTo("98765432100"));
        Assert.That(detail.DrugName, Is.EqualTo("Metformin 500mg"));
        Assert.That(detail.QuantityDispensed, Is.EqualTo(60m));
        Assert.That(detail.DaysSupply, Is.EqualTo(30));
        Assert.That(detail.InsurerName, Is.EqualTo("Medicaid"));
    }

    // ── Adjudicate ──────────────────────────────────────────────────────────

    [Test]
    public async Task AdjudicateClaim_Paid_SyncsIndex()
    {
        // Arrange
        string patientId = $"POS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string claimId = await wf.SubmitPosClaimAsync(
            "RX-003", NcpdpTransactionType.B1,
            "999999", "PROC01", "D0",
            null, null, null,
            "INS-001", "TRICARE",
            "12345678901", "Atorvastatin 20mg", 30m, 30,
            DateTime.UtcNow.Date,
            120.00m, 2.00m, 140.00m,
            null, null, null, null, null);

        // Act
        await wf.AdjudicatePosClaimAsync(claimId,
            PosClaimStatus.Paid,
            insurancePaidAmount: 115.00m,
            patientResponsibility: 10.00m,
            copayAmount: 10.00m,
            coinsuranceAmount: null,
            deductibleAmount: null,
            authorizationNumber: "AUTH-001",
            rejections: null,
            durMessages: null);

        List<PosClaimIndexEntry> claims = await wf.GetPosClaimsAsync();

        // Assert
        PosClaimIndexEntry entry = claims.First(c => c.ClaimId == claimId);
        Assert.That(entry.Status, Is.EqualTo(PosClaimStatus.Paid));
        Assert.That(entry.InsurancePaidAmount, Is.EqualTo(115.00m));
        Assert.That(entry.PatientResponsibility, Is.EqualTo(10.00m));
    }

    [Test]
    public async Task AdjudicateClaim_Rejected_StoresRejections()
    {
        // Arrange
        string patientId = $"POS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string claimId = await wf.SubmitPosClaimAsync(
            "RX-004", NcpdpTransactionType.B1,
            "999999", "PROC01", "D0",
            null, null, null,
            "INS-001", "TRICARE",
            "12345678901", "Amoxicillin 500mg", 21m, 10,
            DateTime.UtcNow.Date,
            15.00m, 1.50m, 20.00m,
            null, null, null, null, null);

        List<PosRejection> rejections = new()
        {
            new PosRejection { Code = "79", Description = "Refill Too Soon", Category = "B" },
            new PosRejection { Code = "76", Description = "Plan Limitations", Category = "B" }
        };

        // Act
        await wf.AdjudicatePosClaimAsync(claimId,
            PosClaimStatus.Rejected,
            insurancePaidAmount: null,
            patientResponsibility: null,
            copayAmount: null,
            coinsuranceAmount: null,
            deductibleAmount: null,
            authorizationNumber: null,
            rejections: rejections,
            durMessages: null);

        PharmacyPosClaimState detail = await wf.GetPosClaimAsync(claimId);

        // Assert
        Assert.That(detail.Status, Is.EqualTo(PosClaimStatus.Rejected));
        Assert.That(detail.Rejections, Has.Count.EqualTo(2));
        Assert.That(detail.Rejections[0].Code, Is.EqualTo("79"));
        Assert.That(detail.Rejections[1].Code, Is.EqualTo("76"));
    }

    [Test]
    public async Task AdjudicateClaim_WithDurMessages()
    {
        // Arrange
        string patientId = $"POS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string claimId = await wf.SubmitPosClaimAsync(
            "RX-005", NcpdpTransactionType.B1,
            "999999", "PROC01", "D0",
            null, null, null,
            "INS-001", "TRICARE",
            "12345678901", "Warfarin 5mg", 30m, 30,
            DateTime.UtcNow.Date,
            25.00m, 2.00m, 35.00m,
            null, null, null, null, null);

        List<DurMessage> durMessages = new()
        {
            new DurMessage
            {
                ReasonCode = "DD",
                Level = DurConflictLevel.Critical,
                ConflictingDrugNdc = "98765432100",
                Message = "Drug-Drug interaction with Aspirin"
            }
        };

        // Act
        await wf.AdjudicatePosClaimAsync(claimId,
            PosClaimStatus.Paid,
            insurancePaidAmount: 20.00m,
            patientResponsibility: 7.00m,
            copayAmount: 7.00m,
            coinsuranceAmount: null,
            deductibleAmount: null,
            authorizationNumber: "AUTH-DUR",
            rejections: null,
            durMessages: durMessages);

        PharmacyPosClaimState detail = await wf.GetPosClaimAsync(claimId);

        // Assert
        Assert.That(detail.DurMessages, Has.Count.EqualTo(1));
        Assert.That(detail.DurMessages[0].ReasonCode, Is.EqualTo("DD"));
        Assert.That(detail.DurMessages[0].Level, Is.EqualTo(DurConflictLevel.Critical));
    }

    // ── Reverse ─────────────────────────────────────────────────────────────

    [Test]
    public async Task ReverseClaim_SyncsIndex()
    {
        // Arrange
        string patientId = $"POS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string claimId = await wf.SubmitPosClaimAsync(
            "RX-006", NcpdpTransactionType.B1,
            "999999", "PROC01", "D0",
            null, null, null,
            "INS-001", "TRICARE",
            "12345678901", "Omeprazole 20mg", 30m, 30,
            DateTime.UtcNow.Date,
            30.00m, 2.00m, 40.00m,
            null, null, null, null, null);

        await wf.AdjudicatePosClaimAsync(claimId,
            PosClaimStatus.Paid, 28.00m, 5.00m, 5.00m, null, null, "AUTH-REV", null, null);

        // Act
        await wf.ReversePosClaimAsync(claimId);
        List<PosClaimIndexEntry> claims = await wf.GetPosClaimsAsync();

        // Assert
        PosClaimIndexEntry entry = claims.First(c => c.ClaimId == claimId);
        Assert.That(entry.Status, Is.EqualTo(PosClaimStatus.Reversed));
    }

    // ── Detail ──────────────────────────────────────────────────────────────

    [Test]
    public async Task GetClaimDetail_ReturnsFullState()
    {
        // Arrange
        string patientId = $"POS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string claimId = await wf.SubmitPosClaimAsync(
            "RX-007", NcpdpTransactionType.B1,
            "610014", "PROC", "D0",
            "GRP-300", "CARD-1111", "1",
            "INS-003", "Medicare Part D",
            "55555555555", "Amlodipine 5mg", 90m, 90,
            DateTime.UtcNow.Date,
            10.00m, 1.00m, 15.00m,
            "NCPDP-002", "Brown, RPh",
            "9876543210", "White, MD",
            null);

        // Act
        PharmacyPosClaimState detail = await wf.GetPosClaimAsync(claimId);

        // Assert
        Assert.That(detail.ClaimId, Is.EqualTo(claimId));
        Assert.That(detail.PatientId, Is.EqualTo(patientId));
        Assert.That(detail.PrescriptionId, Is.EqualTo("RX-007"));
        Assert.That(detail.Bin, Is.EqualTo("610014"));
        Assert.That(detail.GroupNumber, Is.EqualTo("GRP-300"));
        Assert.That(detail.QuantityDispensed, Is.EqualTo(90m));
        Assert.That(detail.DaysSupply, Is.EqualTo(90));
    }

    // ── Filter by status ────────────────────────────────────────────────────

    [Test]
    public async Task GetClaimsByStatus_FiltersCorrectly()
    {
        // Arrange
        string patientId = $"POS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string claim1 = await wf.SubmitPosClaimAsync(
            "RX-008", NcpdpTransactionType.B1,
            "999999", "PROC01", "D0",
            null, null, null, null, null,
            "12345678901", "Drug A", 30m, 30,
            DateTime.UtcNow.Date,
            50.00m, 2.00m, 60.00m,
            null, null, null, null, null);

        string claim2 = await wf.SubmitPosClaimAsync(
            "RX-009", NcpdpTransactionType.B1,
            "999999", "PROC01", "D0",
            null, null, null, null, null,
            "98765432100", "Drug B", 30m, 30,
            DateTime.UtcNow.Date,
            75.00m, 2.00m, 85.00m,
            null, null, null, null, null);

        await wf.AdjudicatePosClaimAsync(claim1,
            PosClaimStatus.Paid, 45.00m, 7.00m, 7.00m, null, null, "AUTH-F1", null, null);
        await wf.AdjudicatePosClaimAsync(claim2,
            PosClaimStatus.Rejected, null, null, null, null, null, null,
            new List<PosRejection> { new() { Code = "79", Description = "Refill Too Soon" } }, null);

        // Act
        List<PosClaimIndexEntry> paid = await wf.GetPosClaimsByStatusAsync(PosClaimStatus.Paid);
        List<PosClaimIndexEntry> rejected = await wf.GetPosClaimsByStatusAsync(PosClaimStatus.Rejected);

        // Assert
        Assert.That(paid, Has.Count.EqualTo(1));
        Assert.That(rejected, Has.Count.EqualTo(1));
    }

    // ── Eligibility ─────────────────────────────────────────────────────────

    [Test]
    public async Task EligibilityCheck_E1_CreatesWithoutPrescription()
    {
        // Arrange
        string patientId = $"POS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        string claimId = await wf.SubmitPosClaimAsync(
            null, NcpdpTransactionType.E1,
            "610014", "PROC", "D0",
            "GRP-400", "CARD-2222", "1",
            "INS-004", "Medicaid",
            null, null, null, null,
            DateTime.UtcNow.Date,
            null, null, null,
            null, null, null, null, null);

        PharmacyPosClaimState detail = await wf.GetPosClaimAsync(claimId);

        // Assert
        Assert.That(detail.TransactionType, Is.EqualTo(NcpdpTransactionType.E1));
        Assert.That(detail.PrescriptionId, Is.Null);
        Assert.That(detail.Ndc, Is.Null);
    }

    // ── Multiple patients ───────────────────────────────────────────────────

    [Test]
    public async Task MultiplePatients_IndependentClaims()
    {
        // Arrange
        string patient1 = $"POS-PAT-{Guid.NewGuid()}";
        string patient2 = $"POS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf1 = Workflow(patient1);
        IPatientWorkflowGrain wf2 = Workflow(patient2);

        // Act
        await wf1.SubmitPosClaimAsync(
            "RX-010", NcpdpTransactionType.B1,
            "999999", "PROC01", "D0",
            null, null, null, null, null,
            "12345678901", "Drug X", 30m, 30,
            DateTime.UtcNow.Date,
            50.00m, 2.00m, 60.00m,
            null, null, null, null, null);

        await wf2.SubmitPosClaimAsync(
            "RX-011", NcpdpTransactionType.B1,
            "999999", "PROC01", "D0",
            null, null, null, null, null,
            "98765432100", "Drug Y", 60m, 30,
            DateTime.UtcNow.Date,
            100.00m, 2.00m, 120.00m,
            null, null, null, null, null);

        List<PosClaimIndexEntry> claims1 = await wf1.GetPosClaimsAsync();
        List<PosClaimIndexEntry> claims2 = await wf2.GetPosClaimsAsync();

        // Assert
        Assert.That(claims1, Has.Count.EqualTo(1));
        Assert.That(claims2, Has.Count.EqualTo(1));
        Assert.That(claims1[0].DrugName, Is.EqualTo("Drug X"));
        Assert.That(claims2[0].DrugName, Is.EqualTo("Drug Y"));
    }

    // ── Full lifecycle ──────────────────────────────────────────────────────

    [Test]
    public async Task FullLifecycle_SubmitAdjudicateReverse()
    {
        // Arrange
        string patientId = $"POS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Step 1: Submit B1 billing
        string claimId = await wf.SubmitPosClaimAsync(
            "RX-012", NcpdpTransactionType.B1,
            "999999", "PROC01", "D0",
            "GRP-500", "CARD-3333", "1",
            "INS-005", "TRICARE",
            "12345678901", "Gabapentin 300mg", 90m, 30,
            DateTime.UtcNow.Date,
            80.00m, 2.50m, 95.00m,
            null, null, null, null, null);

        // Step 2: Adjudicate as Paid
        await wf.AdjudicatePosClaimAsync(claimId,
            PosClaimStatus.Paid, 75.00m, 10.00m, 10.00m, null, null, "AUTH-LIFE", null, null);

        PharmacyPosClaimState afterPaid = await wf.GetPosClaimAsync(claimId);
        Assert.That(afterPaid.Status, Is.EqualTo(PosClaimStatus.Paid));

        // Step 3: Submit B2 reversal claim
        string reversalClaimId = await wf.SubmitPosClaimAsync(
            "RX-012", NcpdpTransactionType.B2,
            "999999", "PROC01", "D0",
            "GRP-500", "CARD-3333", "1",
            "INS-005", "TRICARE",
            "12345678901", "Gabapentin 300mg", 90m, 30,
            DateTime.UtcNow.Date,
            80.00m, 2.50m, 95.00m,
            null, null, null, null,
            claimId);

        // Step 4: Reverse the original claim
        await wf.ReversePosClaimAsync(claimId);

        PharmacyPosClaimState afterReverse = await wf.GetPosClaimAsync(claimId);
        Assert.That(afterReverse.Status, Is.EqualTo(PosClaimStatus.Reversed));

        // Verify index has both claims
        List<PosClaimIndexEntry> allClaims = await wf.GetPosClaimsAsync();
        Assert.That(allClaims, Has.Count.EqualTo(2));
    }

    // ── Feature flag disabled ───────────────────────────────────────────────

    [Test]
    public async Task FeatureDisabled_SubmitThrowsException()
    {
        // Arrange — disable the feature
        await GetSiteParams().DisableFeatureAsync("PHARMACY_POS");

        string patientId = $"POS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await wf.SubmitPosClaimAsync(
                "RX-013", NcpdpTransactionType.B1,
                "999999", "PROC01", "D0",
                null, null, null, null, null,
                "12345678901", "Test Drug", 30m, 30,
                DateTime.UtcNow.Date,
                50.00m, 2.00m, 60.00m,
                null, null, null, null, null);
        });

        // Re-enable for subsequent tests
        await GetSiteParams().EnableFeatureAsync("PHARMACY_POS");
    }

    [Test]
    public async Task FeatureDisabled_GetClaimsReturnsEmpty()
    {
        // Arrange — disable the feature
        await GetSiteParams().DisableFeatureAsync("PHARMACY_POS");

        string patientId = $"POS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        List<PosClaimIndexEntry> claims = await wf.GetPosClaimsAsync();

        // Assert
        Assert.That(claims, Is.Empty);

        // Re-enable for subsequent tests
        await GetSiteParams().EnableFeatureAsync("PHARMACY_POS");
    }
}
