// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for EPCS (E-Prescribing for Controlled Substances) — 21 CFR Part 1311.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/> with
/// Site Flavor Architecture (Option 4 — Composition) feature gate.
/// </summary>
[TestFixture]
public class EpcsWorkflowTests
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
        await GetSiteParams().EnableFeatureAsync("EPCS");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static EpcsPharmacyDestination DefaultPharmacy() => new()
    {
        NcpdpId = "1234567",
        PharmacyName = "Main Street Pharmacy",
        Address = "123 Main St",
        Phone = "555-1234",
    };

    private static EpcsSignatureRecord DefaultSignature() => new()
    {
        PrescriptionHash = "A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2",
        CertificateThumbprint = "AABBCCDD11223344AABBCCDD11223344AABBCCDD",
        SigningTime = DateTime.UtcNow,
        TwoFactorMethod = EpcsTwoFactorMethod.HardwareToken,
        TwoFactorVerificationTime = DateTime.UtcNow,
        IsValid = true,
    };

    private async Task<string> CreateDefaultEpcs(IPatientWorkflowGrain wf) =>
        await wf.CreateEpcsPrescriptionAsync(
            null,
            EpcsScriptTransactionType.NewRx,
            "Oxycodone 5mg", "12345-6789-01", "II",
            60m, 30, 0,
            "Take 1 tablet every 6 hours as needed for pain",
            "M54.5",
            "1234567890", "AB1234567", "Dr. Smith",
            null, DefaultPharmacy());

    // ── Create ──────────────────────────────────────────────────────────────

    [Test]
    public async Task CreateEpcs_ReturnsId_AndAppearsInIndex()
    {
        string patientId = $"EPCS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string epcsId = await CreateDefaultEpcs(wf);

        Assert.That(epcsId, Does.StartWith("EPCS-RX:"));

        List<EpcsPrescriptionIndexEntry> all = await wf.GetEpcsPrescriptionsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].EpcsId, Is.EqualTo(epcsId));
        Assert.That(all[0].Status, Is.EqualTo(EpcsTransmissionStatus.Draft));
        Assert.That(all[0].DrugName, Is.EqualTo("Oxycodone 5mg"));
    }

    [Test]
    public async Task CreateEpcs_ScheduleII_NoRefills()
    {
        string patientId = $"EPCS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string epcsId = await wf.CreateEpcsPrescriptionAsync(
            null,
            EpcsScriptTransactionType.NewRx,
            "Adderall 20mg", null, "II",
            30m, 30, 0,
            "Take 1 capsule daily in the morning", "F90.0",
            "1234567890", "AB1234567", "Dr. Smith",
            null, DefaultPharmacy());

        EpcsPrescriptionState state = await wf.GetEpcsPrescriptionAsync(epcsId);
        Assert.That(state.DeaSchedule, Is.EqualTo("II"));
        Assert.That(state.RefillsAuthorized, Is.EqualTo(0));
        Assert.That(state.Quantity, Is.EqualTo(30m));
    }

    // ── Sign ────────────────────────────────────────────────────────────────

    [Test]
    public async Task SignEpcs_WithTwoFactor_SyncsIndex()
    {
        string patientId = $"EPCS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string epcsId = await CreateDefaultEpcs(wf);

        await wf.SignEpcsPrescriptionAsync(epcsId, DefaultSignature());

        List<EpcsPrescriptionIndexEntry> all = await wf.GetEpcsPrescriptionsAsync();
        Assert.That(all[0].Status, Is.EqualTo(EpcsTransmissionStatus.Signed));
        Assert.That(all[0].IsSigned, Is.True);

        EpcsPrescriptionState state = await wf.GetEpcsPrescriptionAsync(epcsId);
        Assert.That(state.Signature, Is.Not.Null);
        Assert.That(state.Signature!.TwoFactorMethod, Is.EqualTo(EpcsTwoFactorMethod.HardwareToken));
    }

    // ── Transmit ────────────────────────────────────────────────────────────

    [Test]
    public async Task TransmitEpcs_SetsTransmittedStatus_SyncsIndex()
    {
        string patientId = $"EPCS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string epcsId = await CreateDefaultEpcs(wf);
        await wf.SignEpcsPrescriptionAsync(epcsId, DefaultSignature());

        await wf.TransmitEpcsPrescriptionAsync(epcsId, "MSG-TRANSMIT-001");

        List<EpcsPrescriptionIndexEntry> all = await wf.GetEpcsPrescriptionsAsync();
        Assert.That(all[0].Status, Is.EqualTo(EpcsTransmissionStatus.Transmitted));

        EpcsPrescriptionState state = await wf.GetEpcsPrescriptionAsync(epcsId);
        Assert.That(state.TransmittedDate, Is.Not.Null);
        Assert.That(state.TransmissionMessageId, Is.EqualTo("MSG-TRANSMIT-001"));
    }

    // ── Acknowledge ─────────────────────────────────────────────────────────

    [Test]
    public async Task AcknowledgeEpcs_SetsAcknowledged()
    {
        string patientId = $"EPCS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string epcsId = await CreateDefaultEpcs(wf);
        await wf.SignEpcsPrescriptionAsync(epcsId, DefaultSignature());
        await wf.TransmitEpcsPrescriptionAsync(epcsId, "MSG-ACK-001");

        await wf.AcknowledgeEpcsPrescriptionAsync(epcsId);

        List<EpcsPrescriptionIndexEntry> all = await wf.GetEpcsPrescriptionsAsync();
        Assert.That(all[0].Status, Is.EqualTo(EpcsTransmissionStatus.Acknowledged));

        EpcsPrescriptionState state = await wf.GetEpcsPrescriptionAsync(epcsId);
        Assert.That(state.AcknowledgedDate, Is.Not.Null);
    }

    // ── Cancel ──────────────────────────────────────────────────────────────

    [Test]
    public async Task CancelEpcs_SetsStatusAndAddsAudit()
    {
        string patientId = $"EPCS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string epcsId = await CreateDefaultEpcs(wf);

        await wf.CancelEpcsPrescriptionAsync(epcsId, "USER-001", "Patient requested cancellation");

        List<EpcsPrescriptionIndexEntry> all = await wf.GetEpcsPrescriptionsAsync();
        Assert.That(all[0].Status, Is.EqualTo(EpcsTransmissionStatus.Cancelled));

        EpcsPrescriptionState state = await wf.GetEpcsPrescriptionAsync(epcsId);
        Assert.That(state.Status, Is.EqualTo(EpcsTransmissionStatus.Cancelled));
        Assert.That(state.AuditTrail.Last().Action, Is.EqualTo("CANCELLED"));
        Assert.That(state.AuditTrail.Last().UserId, Is.EqualTo("USER-001"));
    }

    // ── Detail retrieval ────────────────────────────────────────────────────

    [Test]
    public async Task GetEpcsDetail_ReturnsFullState()
    {
        string patientId = $"EPCS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string epcsId = await wf.CreateEpcsPrescriptionAsync(
            "RX-LINK-001",
            EpcsScriptTransactionType.NewRx,
            "Hydrocodone 10mg/325mg", "98765-4321-01", "II",
            120m, 30, 0,
            "Take 1 tablet every 4-6 hours", "M54.5",
            "9876543210", "CD9876543", "Dr. Jones",
            "CRED-001", DefaultPharmacy());

        EpcsPrescriptionState state = await wf.GetEpcsPrescriptionAsync(epcsId);
        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.PrescriptionId, Is.EqualTo("RX-LINK-001"));
        Assert.That(state.DrugName, Is.EqualTo("Hydrocodone 10mg/325mg"));
        Assert.That(state.Ndc, Is.EqualTo("98765-4321-01"));
        Assert.That(state.PrescriberDea, Is.EqualTo("CD9876543"));
        Assert.That(state.DestinationPharmacy, Is.Not.Null);
    }

    // ── Filter by status ────────────────────────────────────────────────────

    [Test]
    public async Task GetEpcsByStatus_FiltersCorrectly()
    {
        string patientId = $"EPCS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string epcsId1 = await CreateDefaultEpcs(wf);
        string epcsId2 = await CreateDefaultEpcs(wf);

        // Sign only the first one
        await wf.SignEpcsPrescriptionAsync(epcsId1, DefaultSignature());

        List<EpcsPrescriptionIndexEntry> drafts =
            await wf.GetEpcsPrescriptionsByStatusAsync(EpcsTransmissionStatus.Draft);
        Assert.That(drafts, Has.Count.EqualTo(1));
        Assert.That(drafts[0].EpcsId, Is.EqualTo(epcsId2));

        List<EpcsPrescriptionIndexEntry> signed =
            await wf.GetEpcsPrescriptionsByStatusAsync(EpcsTransmissionStatus.Signed);
        Assert.That(signed, Has.Count.EqualTo(1));
        Assert.That(signed[0].EpcsId, Is.EqualTo(epcsId1));
    }

    // ── Full Lifecycle ──────────────────────────────────────────────────────

    [Test]
    public async Task FullLifecycle_CreateSignTransmitAcknowledge()
    {
        string patientId = $"EPCS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // 1. Create
        string epcsId = await CreateDefaultEpcs(wf);

        // 2. Sign
        await wf.SignEpcsPrescriptionAsync(epcsId, DefaultSignature());

        // 3. Transmit
        await wf.TransmitEpcsPrescriptionAsync(epcsId, "MSG-FULL-001");

        // 4. Acknowledge
        await wf.AcknowledgeEpcsPrescriptionAsync(epcsId);

        // Verify final state
        EpcsPrescriptionState state = await wf.GetEpcsPrescriptionAsync(epcsId);
        Assert.That(state.Status, Is.EqualTo(EpcsTransmissionStatus.Acknowledged));
        Assert.That(state.AuditTrail.Count, Is.GreaterThanOrEqualTo(4));
        Assert.That(state.AuditTrail[0].Action, Is.EqualTo("CREATED"));
        Assert.That(state.AuditTrail[1].Action, Is.EqualTo("SIGNED"));
        Assert.That(state.AuditTrail[2].Action, Is.EqualTo("TRANSMITTED"));
        Assert.That(state.AuditTrail[3].Action, Is.EqualTo("ACKNOWLEDGED"));

        // Verify index reflects final state
        List<EpcsPrescriptionIndexEntry> all = await wf.GetEpcsPrescriptionsAsync();
        Assert.That(all[0].Status, Is.EqualTo(EpcsTransmissionStatus.Acknowledged));
        Assert.That(all[0].IsSigned, Is.True);
    }

    // ── CancelRx Transaction ────────────────────────────────────────────────

    [Test]
    public async Task CancelRxTransaction_CreatesCorrectType()
    {
        string patientId = $"EPCS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string epcsId = await wf.CreateEpcsPrescriptionAsync(
            null,
            EpcsScriptTransactionType.CancelRx,
            "Oxycodone 5mg", null, "II",
            60m, 30, 0,
            null, null,
            "1234567890", "AB1234567", "Dr. Smith",
            null, DefaultPharmacy());

        EpcsPrescriptionState state = await wf.GetEpcsPrescriptionAsync(epcsId);
        Assert.That(state.TransactionType, Is.EqualTo(EpcsScriptTransactionType.CancelRx));

        List<EpcsPrescriptionIndexEntry> all = await wf.GetEpcsPrescriptionsAsync();
        Assert.That(all[0].TransactionType, Is.EqualTo(EpcsScriptTransactionType.CancelRx));
    }

    // ── Multiple patients ───────────────────────────────────────────────────

    [Test]
    public async Task MultiplePatients_IndependentRecords()
    {
        string patientId1 = $"EPCS-PAT-{Guid.NewGuid()}";
        string patientId2 = $"EPCS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf1 = Workflow(patientId1);
        IPatientWorkflowGrain wf2 = Workflow(patientId2);

        await CreateDefaultEpcs(wf1);
        await CreateDefaultEpcs(wf2);
        await CreateDefaultEpcs(wf2);

        List<EpcsPrescriptionIndexEntry> list1 = await wf1.GetEpcsPrescriptionsAsync();
        List<EpcsPrescriptionIndexEntry> list2 = await wf2.GetEpcsPrescriptionsAsync();

        Assert.That(list1, Has.Count.EqualTo(1));
        Assert.That(list2, Has.Count.EqualTo(2));
    }

    // ── Feature flag disabled ───────────────────────────────────────────────

    [Test]
    public async Task FeatureDisabled_CreateThrowsException()
    {
        // Arrange — disable the feature
        await GetSiteParams().DisableFeatureAsync("EPCS");

        string patientId = $"EPCS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await wf.CreateEpcsPrescriptionAsync(
                null,
                EpcsScriptTransactionType.NewRx,
                "Oxycodone 5mg", null, "II",
                60m, 30, 0,
                null, null,
                "1234567890", "AB1234567", "Dr. Smith",
                null, null);
        });

        // Re-enable for subsequent tests
        await GetSiteParams().EnableFeatureAsync("EPCS");
    }

    [Test]
    public async Task FeatureDisabled_GetReturnsEmpty()
    {
        // Arrange — disable the feature
        await GetSiteParams().DisableFeatureAsync("EPCS");

        string patientId = $"EPCS-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        List<EpcsPrescriptionIndexEntry> prescriptions = await wf.GetEpcsPrescriptionsAsync();

        // Assert
        Assert.That(prescriptions, Is.Empty);

        // Re-enable for subsequent tests
        await GetSiteParams().EnableFeatureAsync("EPCS");
    }
}
