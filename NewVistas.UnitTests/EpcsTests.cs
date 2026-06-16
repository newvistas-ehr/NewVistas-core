// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

// ── EpcsPrescriptionGrain Tests ─────────────────────────────────────────────

[TestFixture]
public class EpcsPrescriptionGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IEpcsPrescriptionGrain GetRxGrain() =>
        _cluster.GrainFactory.GetGrain<IEpcsPrescriptionGrain>($"EPCS-RX:{Guid.NewGuid()}");

    private IEpcsPrescriptionIndexGrain GetRxIndexGrain() =>
        _cluster.GrainFactory.GetGrain<IEpcsPrescriptionIndexGrain>($"EPCS-RX-IDX:PATIENT-{Guid.NewGuid()}");

    private IEpcsProviderCredentialGrain GetProviderGrain() =>
        _cluster.GrainFactory.GetGrain<IEpcsProviderCredentialGrain>($"EPCS-PROVIDER:{Guid.NewGuid()}");

    private IEpcsProviderIndexGrain GetProviderIndexGrain() =>
        _cluster.GrainFactory.GetGrain<IEpcsProviderIndexGrain>("EPCS-PROVIDER-IDX");

    private static EpcsPharmacyDestination DefaultPharmacy() => new()
    {
        NcpdpId = "1234567",
        PharmacyName = "Main Street Pharmacy",
        Address = "123 Main St",
        Phone = "555-1234",
    };

    private static Task CreateDefaultRx(IEpcsPrescriptionGrain grain) =>
        grain.CreateAsync(
            "PAT-001", null,
            EpcsScriptTransactionType.NewRx,
            "Oxycodone 5mg", "12345-6789-01", "II",
            60m, 30, 0,
            "Take 1 tablet by mouth every 6 hours as needed for pain",
            "M54.5",
            "1234567890", "AB1234567", "Dr. Smith",
            null, DefaultPharmacy());

    private static EpcsSignatureRecord DefaultSignature() => new()
    {
        PrescriptionHash = "A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2",
        CertificateThumbprint = "AABBCCDD11223344AABBCCDD11223344AABBCCDD",
        SigningTime = DateTime.UtcNow,
        TwoFactorMethod = EpcsTwoFactorMethod.HardwareToken,
        TwoFactorVerificationTime = DateTime.UtcNow,
        IsValid = true,
    };

    // ── Create ──────────────────────────────────────────────────────────────

    [Test]
    public async Task PrescriptionGrain_Create_PersistsAllFields()
    {
        IEpcsPrescriptionGrain grain = GetRxGrain();
        EpcsPharmacyDestination pharmacy = DefaultPharmacy();

        await grain.CreateAsync(
            "PAT-001", null,
            EpcsScriptTransactionType.NewRx,
            "Oxycodone 5mg", "12345-6789-01", "II",
            60m, 30, 0,
            "Take 1 tablet every 6 hours", "M54.5",
            "1234567890", "AB1234567", "Dr. Smith",
            null, pharmacy);

        EpcsPrescriptionState state = await grain.GetAsync();
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.TransactionType, Is.EqualTo(EpcsScriptTransactionType.NewRx));
        Assert.That(state.Status, Is.EqualTo(EpcsTransmissionStatus.Draft));
        Assert.That(state.DrugName, Is.EqualTo("Oxycodone 5mg"));
        Assert.That(state.DeaSchedule, Is.EqualTo("II"));
        Assert.That(state.Quantity, Is.EqualTo(60m));
        Assert.That(state.DaysSupply, Is.EqualTo(30));
        Assert.That(state.RefillsAuthorized, Is.EqualTo(0));
        Assert.That(state.PrescriberName, Is.EqualTo("Dr. Smith"));
        Assert.That(state.DestinationPharmacy, Is.Not.Null);
        Assert.That(state.DestinationPharmacy!.NcpdpId, Is.EqualTo("1234567"));
    }

    [Test]
    public async Task PrescriptionGrain_Create_AddsAuditEntry()
    {
        IEpcsPrescriptionGrain grain = GetRxGrain();
        await CreateDefaultRx(grain);

        EpcsPrescriptionState state = await grain.GetAsync();
        Assert.That(state.AuditTrail, Has.Count.EqualTo(1));
        Assert.That(state.AuditTrail[0].Action, Is.EqualTo("CREATED"));
    }

    // ── Sign ────────────────────────────────────────────────────────────────

    [Test]
    public async Task PrescriptionGrain_Sign_SetsSignedStatus()
    {
        IEpcsPrescriptionGrain grain = GetRxGrain();
        await CreateDefaultRx(grain);

        EpcsSignatureRecord sig = DefaultSignature();
        await grain.SignAsync(sig);

        EpcsPrescriptionState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EpcsTransmissionStatus.Signed));
        Assert.That(state.Signature, Is.Not.Null);
        Assert.That(state.Signature!.TwoFactorMethod, Is.EqualTo(EpcsTwoFactorMethod.HardwareToken));
        Assert.That(state.Signature.CertificateThumbprint, Is.EqualTo(sig.CertificateThumbprint));
        Assert.That(state.Signature.PrescriptionHash, Is.EqualTo(sig.PrescriptionHash));
    }

    [Test]
    public async Task PrescriptionGrain_Sign_AddsAuditEntry()
    {
        IEpcsPrescriptionGrain grain = GetRxGrain();
        await CreateDefaultRx(grain);
        await grain.SignAsync(DefaultSignature());

        EpcsPrescriptionState state = await grain.GetAsync();
        Assert.That(state.AuditTrail, Has.Count.EqualTo(2));
        Assert.That(state.AuditTrail[1].Action, Is.EqualTo("SIGNED"));
        Assert.That(state.AuditTrail[1].Details, Does.Contain("cert"));
    }

    // ── Transmit / Acknowledge / Error / Cancel ─────────────────────────────

    [Test]
    public async Task PrescriptionGrain_MarkTransmitted_SetsStatusAndDate()
    {
        IEpcsPrescriptionGrain grain = GetRxGrain();
        await CreateDefaultRx(grain);
        await grain.SignAsync(DefaultSignature());

        await grain.MarkTransmittedAsync("MSG-12345");

        EpcsPrescriptionState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EpcsTransmissionStatus.Transmitted));
        Assert.That(state.TransmittedDate, Is.Not.Null);
        Assert.That(state.TransmissionMessageId, Is.EqualTo("MSG-12345"));
    }

    [Test]
    public async Task PrescriptionGrain_MarkAcknowledged_SetsStatusAndDate()
    {
        IEpcsPrescriptionGrain grain = GetRxGrain();
        await CreateDefaultRx(grain);
        await grain.SignAsync(DefaultSignature());
        await grain.MarkTransmittedAsync("MSG-12345");

        await grain.MarkAcknowledgedAsync();

        EpcsPrescriptionState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EpcsTransmissionStatus.Acknowledged));
        Assert.That(state.AcknowledgedDate, Is.Not.Null);
    }

    [Test]
    public async Task PrescriptionGrain_MarkError_SetsStatusAndMessage()
    {
        IEpcsPrescriptionGrain grain = GetRxGrain();
        await CreateDefaultRx(grain);

        await grain.MarkErrorAsync("Pharmacy network timeout");

        EpcsPrescriptionState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EpcsTransmissionStatus.Error));
        Assert.That(state.ErrorMessage, Is.EqualTo("Pharmacy network timeout"));
    }

    [Test]
    public async Task PrescriptionGrain_Cancel_SetsStatusAndAddsAudit()
    {
        IEpcsPrescriptionGrain grain = GetRxGrain();
        await CreateDefaultRx(grain);

        await grain.CancelAsync("USER-001", "Patient requested cancellation");

        EpcsPrescriptionState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EpcsTransmissionStatus.Cancelled));
        Assert.That(state.AuditTrail.Last().Action, Is.EqualTo("CANCELLED"));
        Assert.That(state.AuditTrail.Last().UserId, Is.EqualTo("USER-001"));
    }

    // ── Audit Trail ─────────────────────────────────────────────────────────

    [Test]
    public async Task PrescriptionGrain_AddAuditEntry_AppendsToTrail()
    {
        IEpcsPrescriptionGrain grain = GetRxGrain();
        await CreateDefaultRx(grain);

        await grain.AddAuditEntryAsync("VIEWED", "USER-002", "Prescription viewed by pharmacist");

        EpcsPrescriptionState state = await grain.GetAsync();
        Assert.That(state.AuditTrail, Has.Count.EqualTo(2));
        Assert.That(state.AuditTrail[1].Action, Is.EqualTo("VIEWED"));
        Assert.That(state.AuditTrail[1].UserId, Is.EqualTo("USER-002"));
    }

    // ── Full Lifecycle ──────────────────────────────────────────────────────

    [Test]
    public async Task PrescriptionGrain_FullLifecycle_DraftToAcknowledged()
    {
        IEpcsPrescriptionGrain grain = GetRxGrain();

        // 1. Create (Draft)
        await CreateDefaultRx(grain);
        EpcsPrescriptionState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EpcsTransmissionStatus.Draft));

        // 2. Sign
        await grain.SignAsync(DefaultSignature());
        state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EpcsTransmissionStatus.Signed));

        // 3. Transmit
        await grain.MarkTransmittedAsync("MSG-LIFECYCLE-001");
        state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EpcsTransmissionStatus.Transmitted));

        // 4. Acknowledge
        await grain.MarkAcknowledgedAsync();
        state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EpcsTransmissionStatus.Acknowledged));

        // Verify full audit trail
        Assert.That(state.AuditTrail, Has.Count.EqualTo(4));
        Assert.That(state.AuditTrail[0].Action, Is.EqualTo("CREATED"));
        Assert.That(state.AuditTrail[1].Action, Is.EqualTo("SIGNED"));
        Assert.That(state.AuditTrail[2].Action, Is.EqualTo("TRANSMITTED"));
        Assert.That(state.AuditTrail[3].Action, Is.EqualTo("ACKNOWLEDGED"));
    }

    // ── Index Grain Tests ───────────────────────────────────────────────────

    [Test]
    public async Task PrescriptionIndexGrain_AddAndGetAll_ReturnsNewestFirst()
    {
        IEpcsPrescriptionIndexGrain index = GetRxIndexGrain();

        EpcsPrescriptionIndexEntry entry1 = new()
        {
            EpcsId = $"EPCS-RX:{Guid.NewGuid()}", PatientId = "PAT-001",
            TransactionType = EpcsScriptTransactionType.NewRx,
            Status = EpcsTransmissionStatus.Draft,
            DrugName = "Oxycodone 5mg", DeaSchedule = "II",
            PrescriberName = "Dr. Smith", CreatedDate = DateTime.UtcNow.AddHours(-1),
        };
        EpcsPrescriptionIndexEntry entry2 = new()
        {
            EpcsId = $"EPCS-RX:{Guid.NewGuid()}", PatientId = "PAT-001",
            TransactionType = EpcsScriptTransactionType.NewRx,
            Status = EpcsTransmissionStatus.Draft,
            DrugName = "Hydrocodone 10mg", DeaSchedule = "II",
            PrescriberName = "Dr. Jones", CreatedDate = DateTime.UtcNow,
        };

        await index.AddEntryAsync(entry1);
        await index.AddEntryAsync(entry2);

        List<EpcsPrescriptionIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(2));
        // Insert(0, ...) means newest first
        Assert.That(all[0].DrugName, Is.EqualTo("Hydrocodone 10mg"));
    }

    [Test]
    public async Task PrescriptionIndexGrain_GetByStatus_FiltersCorrectly()
    {
        IEpcsPrescriptionIndexGrain index = GetRxIndexGrain();

        await index.AddEntryAsync(new EpcsPrescriptionIndexEntry
        {
            EpcsId = $"EPCS-RX:{Guid.NewGuid()}", PatientId = "PAT-001",
            Status = EpcsTransmissionStatus.Draft,
            DrugName = "Drug A", DeaSchedule = "II", CreatedDate = DateTime.UtcNow,
        });
        await index.AddEntryAsync(new EpcsPrescriptionIndexEntry
        {
            EpcsId = $"EPCS-RX:{Guid.NewGuid()}", PatientId = "PAT-001",
            Status = EpcsTransmissionStatus.Signed,
            DrugName = "Drug B", DeaSchedule = "III", CreatedDate = DateTime.UtcNow,
        });

        List<EpcsPrescriptionIndexEntry> drafts = await index.GetByStatusAsync(EpcsTransmissionStatus.Draft);
        Assert.That(drafts, Has.Count.EqualTo(1));
        Assert.That(drafts[0].DrugName, Is.EqualTo("Drug A"));
    }

    [Test]
    public async Task PrescriptionIndexGrain_UpdateEntry_ChangesStatusAndSigned()
    {
        IEpcsPrescriptionIndexGrain index = GetRxIndexGrain();
        string epcsId = $"EPCS-RX:{Guid.NewGuid()}";

        await index.AddEntryAsync(new EpcsPrescriptionIndexEntry
        {
            EpcsId = epcsId, PatientId = "PAT-001",
            Status = EpcsTransmissionStatus.Draft, IsSigned = false,
            DrugName = "Oxycodone 5mg", DeaSchedule = "II", CreatedDate = DateTime.UtcNow,
        });

        await index.UpdateEntryAsync(epcsId, EpcsTransmissionStatus.Signed, true);

        List<EpcsPrescriptionIndexEntry> all = await index.GetAllAsync();
        EpcsPrescriptionIndexEntry updated = all.First(e => e.EpcsId == epcsId);
        Assert.That(updated.Status, Is.EqualTo(EpcsTransmissionStatus.Signed));
        Assert.That(updated.IsSigned, Is.True);
    }

    // ── Provider Credential Grain Tests ─────────────────────────────────────

    [Test]
    public async Task ProviderGrain_Save_PersistsAllFields()
    {
        IEpcsProviderCredentialGrain grain = GetProviderGrain();

        await grain.SaveAsync(
            "PROV-001", "Dr. Smith",
            "1234567890", "AB1234567",
            IdentityProofingLevel.NistLevel2,
            DateTime.UtcNow,
            new List<EpcsTwoFactorMethod> { EpcsTwoFactorMethod.HardwareToken, EpcsTwoFactorMethod.Biometric },
            "AABBCCDD11223344AABBCCDD11223344AABBCCDD",
            DateTime.UtcNow.AddYears(2));

        EpcsProviderCredentialState state = await grain.GetAsync();
        Assert.That(state.ProviderId, Is.EqualTo("PROV-001"));
        Assert.That(state.ProviderName, Is.EqualTo("Dr. Smith"));
        Assert.That(state.Npi, Is.EqualTo("1234567890"));
        Assert.That(state.DeaNumber, Is.EqualTo("AB1234567"));
        Assert.That(state.IdentityProofingLevel, Is.EqualTo(IdentityProofingLevel.NistLevel2));
        Assert.That(state.ConfiguredTwoFactorMethods, Has.Count.EqualTo(2));
        Assert.That(state.ConfiguredTwoFactorMethods, Contains.Item(EpcsTwoFactorMethod.HardwareToken));
        Assert.That(state.ConfiguredTwoFactorMethods, Contains.Item(EpcsTwoFactorMethod.Biometric));
        Assert.That(state.CertificateThumbprint, Is.EqualTo("AABBCCDD11223344AABBCCDD11223344AABBCCDD"));
    }

    [Test]
    public async Task ProviderGrain_Activate_SetsActiveStatus()
    {
        IEpcsProviderCredentialGrain grain = GetProviderGrain();
        await grain.SaveAsync("PROV-002", "Dr. Jones", null, null,
            IdentityProofingLevel.NistLevel2, DateTime.UtcNow, null, null, null);

        await grain.ActivateAsync();

        EpcsProviderCredentialState state = await grain.GetAsync();
        Assert.That(state.CredentialStatus, Is.EqualTo(EpcsCredentialStatus.Active));
        Assert.That(state.ActivatedDate, Is.Not.Null);
    }

    [Test]
    public async Task ProviderGrain_Suspend_SetsSuspendedStatus()
    {
        IEpcsProviderCredentialGrain grain = GetProviderGrain();
        await grain.SaveAsync("PROV-003", "Dr. Brown", null, null,
            IdentityProofingLevel.NistLevel2, DateTime.UtcNow, null, null, null);
        await grain.ActivateAsync();

        await grain.SuspendAsync();

        EpcsProviderCredentialState state = await grain.GetAsync();
        Assert.That(state.CredentialStatus, Is.EqualTo(EpcsCredentialStatus.Suspended));
    }

    [Test]
    public async Task ProviderGrain_Revoke_SetsRevokedStatus()
    {
        IEpcsProviderCredentialGrain grain = GetProviderGrain();
        await grain.SaveAsync("PROV-004", "Dr. Davis", null, null,
            IdentityProofingLevel.NistLevel2, DateTime.UtcNow, null, null, null);
        await grain.ActivateAsync();

        await grain.RevokeAsync();

        EpcsProviderCredentialState state = await grain.GetAsync();
        Assert.That(state.CredentialStatus, Is.EqualTo(EpcsCredentialStatus.Revoked));
    }

    [Test]
    public async Task ProviderGrain_RecordUsage_UpdatesLastUsedDate()
    {
        IEpcsProviderCredentialGrain grain = GetProviderGrain();
        await grain.SaveAsync("PROV-005", "Dr. Wilson", null, null,
            IdentityProofingLevel.NistLevel2, DateTime.UtcNow, null, null, null);
        await grain.ActivateAsync();

        await grain.RecordUsageAsync();

        EpcsProviderCredentialState state = await grain.GetAsync();
        Assert.That(state.LastUsedDate, Is.Not.Null);
    }

    // ── Provider Index Grain Tests ──────────────────────────────────────────

    [Test]
    public async Task ProviderIndexGrain_Upsert_AddsAndUpdates()
    {
        IEpcsProviderIndexGrain index = _cluster.GrainFactory
            .GetGrain<IEpcsProviderIndexGrain>($"EPCS-PROVIDER-IDX-TEST-{Guid.NewGuid()}");

        string credId = $"EPCS-PROVIDER:{Guid.NewGuid()}";
        await index.UpsertAsync(new EpcsProviderIndexEntry
        {
            CredentialId = credId,
            ProviderId = "PROV-010",
            ProviderName = "Dr. Original",
            DeaNumber = "AB1234567",
            CredentialStatus = EpcsCredentialStatus.Pending,
            IdentityProofingLevel = IdentityProofingLevel.NistLevel2,
        });

        // Update same credential
        await index.UpsertAsync(new EpcsProviderIndexEntry
        {
            CredentialId = credId,
            ProviderId = "PROV-010",
            ProviderName = "Dr. Updated",
            DeaNumber = "AB1234567",
            CredentialStatus = EpcsCredentialStatus.Active,
            IdentityProofingLevel = IdentityProofingLevel.NistLevel2,
        });

        List<EpcsProviderIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].ProviderName, Is.EqualTo("Dr. Updated"));
        Assert.That(all[0].CredentialStatus, Is.EqualTo(EpcsCredentialStatus.Active));
    }

    [Test]
    public async Task ProviderIndexGrain_GetActive_FiltersCorrectly()
    {
        IEpcsProviderIndexGrain index = _cluster.GrainFactory
            .GetGrain<IEpcsProviderIndexGrain>($"EPCS-PROVIDER-IDX-TEST-{Guid.NewGuid()}");

        await index.UpsertAsync(new EpcsProviderIndexEntry
        {
            CredentialId = $"EPCS-PROVIDER:{Guid.NewGuid()}",
            ProviderId = "PROV-020", ProviderName = "Dr. Active",
            CredentialStatus = EpcsCredentialStatus.Active,
            IdentityProofingLevel = IdentityProofingLevel.NistLevel2,
        });
        await index.UpsertAsync(new EpcsProviderIndexEntry
        {
            CredentialId = $"EPCS-PROVIDER:{Guid.NewGuid()}",
            ProviderId = "PROV-021", ProviderName = "Dr. Suspended",
            CredentialStatus = EpcsCredentialStatus.Suspended,
            IdentityProofingLevel = IdentityProofingLevel.NistLevel2,
        });

        List<EpcsProviderIndexEntry> active = await index.GetActiveAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].ProviderName, Is.EqualTo("Dr. Active"));
    }
}
