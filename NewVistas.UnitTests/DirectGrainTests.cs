// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for Direct Project grains.
/// §170.315(h)(1) — Direct Project secure transport for C-CDA exchange.
/// </summary>
[TestFixture]
public class DirectGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Address CRUD ─────────────────────────────────────────────────────────

    [Test]
    public async Task AddressGrain_CanSaveAndRetrieve()
    {
        string addr = $"dr.test{Guid.NewGuid():N}@direct.newvistas.health";
        IDirectAddressGrain grain = _cluster.GrainFactory.GetGrain<IDirectAddressGrain>($"DIRECT-ADDR:{addr}");

        await grain.SaveAddressAsync(new DirectAddressState
        {
            DirectAddress = addr,
            DisplayName = "Dr. Test Provider",
            OwnerType = "provider",
            OwnerId = "USR-001",
            OrganizationName = "NewVistas Health",
            HispDomain = "direct.newvistas.health",
            IsActive = true,
            CertificateThumbprint = "ABC123DEF456",
            CertificateSubject = "CN=dr.test@direct.newvistas.health",
            CertificateExpiration = DateTime.UtcNow.AddYears(2)
        });

        DirectAddressState result = await grain.GetAddressAsync();
        Assert.That(result.DirectAddress, Is.EqualTo(addr));
        Assert.That(result.DisplayName, Is.EqualTo("Dr. Test Provider"));
        Assert.That(result.OwnerType, Is.EqualTo("provider"));
        Assert.That(result.HispDomain, Is.EqualTo("direct.newvistas.health"));
        Assert.That(result.CertificateThumbprint, Is.EqualTo("ABC123DEF456"));
    }

    [Test]
    public async Task AddressGrain_CanSetActive()
    {
        string addr = $"dr.active{Guid.NewGuid():N}@direct.newvistas.health";
        IDirectAddressGrain grain = _cluster.GrainFactory.GetGrain<IDirectAddressGrain>($"DIRECT-ADDR:{addr}");

        await grain.SaveAddressAsync(new DirectAddressState
        {
            DirectAddress = addr, DisplayName = "Test", IsActive = true
        });

        await grain.SetActiveAsync(false);
        DirectAddressState result = await grain.GetAddressAsync();
        Assert.That(result.IsActive, Is.False);
    }

    [Test]
    public async Task AddressGrain_CanUpdateCertificate()
    {
        string addr = $"dr.cert{Guid.NewGuid():N}@direct.newvistas.health";
        IDirectAddressGrain grain = _cluster.GrainFactory.GetGrain<IDirectAddressGrain>($"DIRECT-ADDR:{addr}");

        await grain.SaveAddressAsync(new DirectAddressState
        {
            DirectAddress = addr, DisplayName = "Cert Test"
        });

        DateTime expiry = DateTime.UtcNow.AddYears(3);
        await grain.UpdateCertificateAsync("NEWTHUMB789", "CN=new.cert", expiry);

        DirectAddressState result = await grain.GetAddressAsync();
        Assert.That(result.CertificateThumbprint, Is.EqualTo("NEWTHUMB789"));
        Assert.That(result.CertificateSubject, Is.EqualTo("CN=new.cert"));
    }

    // ─── Address Index ────────────────────────────────────────────────────────

    [Test]
    public async Task AddressIndex_CanAddAndList()
    {
        IDirectAddressIndexGrain index = _cluster.GrainFactory.GetGrain<IDirectAddressIndexGrain>(
            $"DIRECT-ADDR-INDEX-{Guid.NewGuid():N}");

        await index.AddAddressAsync(new DirectAddressSummary
        {
            DirectAddress = "a@direct.test", DisplayName = "A", IsActive = true
        });
        await index.AddAddressAsync(new DirectAddressSummary
        {
            DirectAddress = "b@direct.test", DisplayName = "B", IsActive = false
        });

        List<DirectAddressSummary> all = await index.GetAllAddressesAsync();
        Assert.That(all, Has.Count.EqualTo(2));

        List<DirectAddressSummary> active = await index.GetActiveAddressesAsync();
        Assert.That(active, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task AddressIndex_CanRemove()
    {
        IDirectAddressIndexGrain index = _cluster.GrainFactory.GetGrain<IDirectAddressIndexGrain>(
            $"DIRECT-ADDR-INDEX-{Guid.NewGuid():N}");

        await index.AddAddressAsync(new DirectAddressSummary
        {
            DirectAddress = "del@direct.test", DisplayName = "Delete Me", IsActive = true
        });
        await index.RemoveAddressAsync("del@direct.test");

        List<DirectAddressSummary> all = await index.GetAllAddressesAsync();
        Assert.That(all, Has.Count.EqualTo(0));
    }

    // ─── Message Lifecycle ────────────────────────────────────────────────────

    [Test]
    public async Task MessageGrain_CanCreateAndRetrieve()
    {
        string msgId = $"DIRECT-MSG:{Guid.NewGuid():N}";
        IDirectMessageGrain grain = _cluster.GrainFactory.GetGrain<IDirectMessageGrain>(msgId);

        await grain.CreateMessageAsync("outbound",
            "sender@direct.test", "recipient@direct.ext",
            "Referral for Patient Smith", "PAT-001", "Smith, John",
            "Referral", "<ClinicalDocument>...</ClinicalDocument>", "DR-SENDER");

        DirectMessageState result = await grain.GetMessageAsync();
        Assert.That(result.Direction, Is.EqualTo("outbound"));
        Assert.That(result.FromAddress, Is.EqualTo("sender@direct.test"));
        Assert.That(result.ToAddress, Is.EqualTo("recipient@direct.ext"));
        Assert.That(result.Status, Is.EqualTo("draft"));
        Assert.That(result.DocumentType, Is.EqualTo("Referral"));
        Assert.That(result.PatientName, Is.EqualTo("Smith, John"));
    }

    [Test]
    public async Task MessageGrain_FullOutboundLifecycle()
    {
        string msgId = $"DIRECT-MSG:{Guid.NewGuid():N}";
        IDirectMessageGrain grain = _cluster.GrainFactory.GetGrain<IDirectMessageGrain>(msgId);

        await grain.CreateMessageAsync("outbound",
            "a@direct.test", "b@direct.ext",
            "CCD", "PAT-002", null, "CCD", "<doc/>", null);

        // Draft → Sending
        await grain.MarkSendingAsync();
        DirectMessageState state = await grain.GetMessageAsync();
        Assert.That(state.Status, Is.EqualTo("sending"));

        // Sending → Sent (with S/MIME)
        await grain.MarkSentAsync(true, true, "SIGN-THUMB", "ENCRYPT-THUMB");
        state = await grain.GetMessageAsync();
        Assert.That(state.Status, Is.EqualTo("sent"));
        Assert.That(state.IsEncrypted, Is.True);
        Assert.That(state.IsSigned, Is.True);
        Assert.That(state.SentDate, Is.Not.Null);

        // Sent → Delivered
        await grain.MarkDeliveredAsync();
        state = await grain.GetMessageAsync();
        Assert.That(state.Status, Is.EqualTo("delivered"));
        Assert.That(state.DeliveredDate, Is.Not.Null);
        Assert.That(state.MdnStatus, Is.EqualTo("processed"));
    }

    [Test]
    public async Task MessageGrain_CanMarkFailed()
    {
        string msgId = $"DIRECT-MSG:{Guid.NewGuid():N}";
        IDirectMessageGrain grain = _cluster.GrainFactory.GetGrain<IDirectMessageGrain>(msgId);

        await grain.CreateMessageAsync("outbound",
            "a@test", "b@test", "Test", "PAT-X", null, "CCD", "<doc/>", null);
        await grain.MarkSentAsync(true, true, null, null);
        await grain.MarkFailedAsync("Recipient certificate expired");

        DirectMessageState state = await grain.GetMessageAsync();
        Assert.That(state.Status, Is.EqualTo("failed"));
        Assert.That(state.FailureReason, Does.Contain("certificate expired"));
    }

    [Test]
    public async Task MessageGrain_MdnProcessed_MarksDelivered()
    {
        string msgId = $"DIRECT-MSG:{Guid.NewGuid():N}";
        IDirectMessageGrain grain = _cluster.GrainFactory.GetGrain<IDirectMessageGrain>(msgId);

        await grain.CreateMessageAsync("outbound",
            "a@test", "b@test", "Test", "PAT-MDN", null, "CCD", "<doc/>", null);
        await grain.MarkSentAsync(true, true, null, null);
        await grain.RecordMdnAsync("processed", "MDN: automatic-action/MDN-sent-automatically;processed");

        DirectMessageState state = await grain.GetMessageAsync();
        Assert.That(state.MdnStatus, Is.EqualTo("processed"));
        Assert.That(state.Status, Is.EqualTo("delivered"));
        Assert.That(state.MdnDate, Is.Not.Null);
    }

    [Test]
    public async Task MessageGrain_MdnFailed_MarksFailed()
    {
        string msgId = $"DIRECT-MSG:{Guid.NewGuid():N}";
        IDirectMessageGrain grain = _cluster.GrainFactory.GetGrain<IDirectMessageGrain>(msgId);

        await grain.CreateMessageAsync("outbound",
            "a@test", "b@test", "Test", "PAT-MDN2", null, "CCD", "<doc/>", null);
        await grain.MarkSentAsync(true, true, null, null);
        await grain.RecordMdnAsync("failed", "MDN: automatic-action/MDN-sent-automatically;failed");

        DirectMessageState state = await grain.GetMessageAsync();
        Assert.That(state.MdnStatus, Is.EqualTo("failed"));
        Assert.That(state.Status, Is.EqualTo("failed"));
    }

    // ─── Message Index ────────────────────────────────────────────────────────

    [Test]
    public async Task MessageIndex_FiltersInboundOutbound()
    {
        IDirectMessageIndexGrain index = _cluster.GrainFactory.GetGrain<IDirectMessageIndexGrain>(
            $"DIRECT-MSG-INDEX-{Guid.NewGuid():N}");

        await index.AddMessageAsync(new DirectMessageSummary
        {
            MessageId = "MSG-OUT1", Direction = "outbound", Status = "sent",
            CreatedDate = DateTime.UtcNow, MdnStatus = "none"
        });
        await index.AddMessageAsync(new DirectMessageSummary
        {
            MessageId = "MSG-IN1", Direction = "inbound", Status = "received",
            CreatedDate = DateTime.UtcNow, MdnStatus = "none"
        });
        await index.AddMessageAsync(new DirectMessageSummary
        {
            MessageId = "MSG-OUT2", Direction = "outbound", Status = "delivered",
            CreatedDate = DateTime.UtcNow, MdnStatus = "processed"
        });

        List<DirectMessageSummary> outbound = await index.GetOutboundMessagesAsync();
        Assert.That(outbound, Has.Count.EqualTo(2));

        List<DirectMessageSummary> inbound = await index.GetInboundMessagesAsync();
        Assert.That(inbound, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task MessageIndex_FiltersPendingDelivery()
    {
        IDirectMessageIndexGrain index = _cluster.GrainFactory.GetGrain<IDirectMessageIndexGrain>(
            $"DIRECT-MSG-INDEX-{Guid.NewGuid():N}");

        await index.AddMessageAsync(new DirectMessageSummary
        {
            MessageId = "MSG-P1", Direction = "outbound", Status = "sent",
            CreatedDate = DateTime.UtcNow, MdnStatus = "none"
        });
        await index.AddMessageAsync(new DirectMessageSummary
        {
            MessageId = "MSG-P2", Direction = "outbound", Status = "sent",
            CreatedDate = DateTime.UtcNow, MdnStatus = "processed"
        });

        List<DirectMessageSummary> pending = await index.GetPendingDeliveryAsync();
        Assert.That(pending, Has.Count.EqualTo(1));
        Assert.That(pending[0].MessageId, Is.EqualTo("MSG-P1"));
    }

    [Test]
    public async Task MessageIndex_UpdatesMdnStatus()
    {
        IDirectMessageIndexGrain index = _cluster.GrainFactory.GetGrain<IDirectMessageIndexGrain>(
            $"DIRECT-MSG-INDEX-{Guid.NewGuid():N}");

        await index.AddMessageAsync(new DirectMessageSummary
        {
            MessageId = "MSG-MDN", Direction = "outbound", Status = "sent",
            CreatedDate = DateTime.UtcNow, MdnStatus = "none"
        });

        await index.UpdateMdnStatusAsync("MSG-MDN", "processed");

        List<DirectMessageSummary> pending = await index.GetPendingDeliveryAsync();
        Assert.That(pending, Has.Count.EqualTo(0));

        List<DirectMessageSummary> all = await index.GetAllMessagesAsync();
        Assert.That(all[0].Status, Is.EqualTo("delivered"));
    }

    // ─── C-CDA Generator ─────────────────────────────────────────────────────

    [Test]
    public async Task CcdaGenerator_ProducesValidXml()
    {
        string patientId = $"DIRECT-CCDA-{Guid.NewGuid():N}";

        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await workflow.UpdateDemographicsAsync("Smith, John", "M", DateTime.UtcNow.AddYears(-55), null);
        await workflow.AddProblemAsync("Type 2 Diabetes", "E11.9", "Active", "Chronic",
            DateTime.UtcNow.AddYears(-5), null, null, null, null, false, null);

        IDirectCcdaGeneratorGrain gen = _cluster.GrainFactory.GetGrain<IDirectCcdaGeneratorGrain>(
            $"DIRECT-CCDA-GEN:{patientId}");
        string ccda = await gen.GenerateCcdAsync("CCD");

        Assert.That(ccda, Does.Contain("ClinicalDocument"));
        Assert.That(ccda, Does.Contain("2.16.840.1.113883.10.20.22.1.2")); // CCD template
        Assert.That(ccda, Does.Contain("Smith"));
        Assert.That(ccda, Does.Contain("Type 2 Diabetes"));
        Assert.That(ccda, Does.Contain("E11.9"));
        Assert.That(ccda, Does.Contain("Continuity of Care Document"));
    }

    [Test]
    public async Task CcdaGenerator_IncludesAllSections()
    {
        string patientId = $"DIRECT-CCDA-{Guid.NewGuid():N}";

        IDirectCcdaGeneratorGrain gen = _cluster.GrainFactory.GetGrain<IDirectCcdaGeneratorGrain>(
            $"DIRECT-CCDA-GEN:{patientId}");
        string ccda = await gen.GenerateCcdAsync("CCD");

        // All required C-CDA sections present
        Assert.That(ccda, Does.Contain("48765-2")); // Allergies LOINC
        Assert.That(ccda, Does.Contain("10160-0")); // Medications LOINC
        Assert.That(ccda, Does.Contain("11450-4")); // Problems LOINC
        Assert.That(ccda, Does.Contain("8716-3"));  // Vitals LOINC
        Assert.That(ccda, Does.Contain("30954-2")); // Results LOINC
    }
}
