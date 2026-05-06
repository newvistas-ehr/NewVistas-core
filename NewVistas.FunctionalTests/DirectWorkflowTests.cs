// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Direct Project end-to-end workflows.
/// §170.315(h)(1) — Direct Project secure transport for C-CDA exchange.
/// </summary>
[TestFixture]
public class DirectWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task FullLifecycle_RegisterAddress_SendCcda_ReceiveMdn()
    {
        string patientId = $"DIRECT-FUNC-{Guid.NewGuid():N}";

        // 1. Set up patient with clinical data
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await workflow.UpdateDemographicsAsync("Jones, Mary", "F", DateTime.UtcNow.AddYears(-62), null);
        await workflow.AddProblemAsync("Essential Hypertension", "I10", "Active", "Chronic",
            DateTime.UtcNow.AddYears(-5), "DR001", "Dr. Smith", null, null, false, null);
        await workflow.AddProblemAsync("Type 2 Diabetes", "E11.65", "Active", "Chronic",
            DateTime.UtcNow.AddYears(-3), "DR001", "Dr. Smith", null, null, false, null);

        // 2. Register sender Direct address
        string senderAddr = $"dr.smith{Guid.NewGuid():N}@direct.newvistas.health";
        IDirectAddressGrain addrGrain = _cluster.GrainFactory.GetGrain<IDirectAddressGrain>($"DIRECT-ADDR:{senderAddr}");
        await addrGrain.SaveAddressAsync(new DirectAddressState
        {
            DirectAddress = senderAddr,
            DisplayName = "Dr. John Smith",
            OwnerType = "provider",
            OwnerId = "DR001",
            OrganizationName = "NewVistas Medical Center",
            HispDomain = "direct.newvistas.health",
            IsActive = true,
            CertificateThumbprint = "SENDER-CERT-THUMB",
            CertificateSubject = $"CN={senderAddr}",
            CertificateExpiration = DateTime.UtcNow.AddYears(2)
        });

        // 3. Generate C-CDA
        IDirectCcdaGeneratorGrain gen = _cluster.GrainFactory.GetGrain<IDirectCcdaGeneratorGrain>(
            $"DIRECT-CCDA-GEN:{patientId}");
        string ccda = await gen.GenerateCcdAsync("Referral");

        Assert.That(ccda, Does.Contain("Referral Summary"));
        Assert.That(ccda, Does.Contain("Jones"));
        Assert.That(ccda, Does.Contain("I10"));
        Assert.That(ccda, Does.Contain("E11.65"));

        // 4. Create and send message
        string recipientAddr = "dr.recipient@direct.external.org";
        string messageId = $"DIRECT-MSG:{Guid.NewGuid():N}";
        IDirectMessageGrain msgGrain = _cluster.GrainFactory.GetGrain<IDirectMessageGrain>(messageId);
        await msgGrain.CreateMessageAsync("outbound",
            senderAddr, recipientAddr,
            "Referral: Jones, Mary — Hypertension + Diabetes",
            patientId, "Jones, Mary", "Referral", ccda, "DR001");

        await msgGrain.MarkSendingAsync();
        await msgGrain.MarkSentAsync(true, true, "SENDER-CERT-THUMB", "RECIPIENT-CERT-THUMB");

        // 5. Add to index
        IDirectMessageIndexGrain msgIndex = _cluster.GrainFactory.GetGrain<IDirectMessageIndexGrain>("DIRECT-MSG-INDEX");
        await msgIndex.AddMessageAsync(new DirectMessageSummary
        {
            MessageId = messageId, Direction = "outbound",
            FromAddress = senderAddr, ToAddress = recipientAddr,
            Subject = "Referral: Jones, Mary", PatientId = patientId,
            DocumentType = "Referral", Status = "sent",
            CreatedDate = DateTime.UtcNow, MdnStatus = "none"
        });

        // Verify pending delivery
        List<DirectMessageSummary> pending = await msgIndex.GetPendingDeliveryAsync();
        Assert.That(pending.Any(m => m.MessageId == messageId), Is.True);

        // 6. Receive MDN — delivery confirmed
        await msgGrain.RecordMdnAsync("processed",
            "Final-Recipient: rfc822;dr.recipient@direct.external.org\r\nDisposition: automatic-action/MDN-sent-automatically;processed");
        await msgIndex.UpdateMdnStatusAsync(messageId, "processed");

        // 7. Verify delivered
        DirectMessageState finalMsg = await msgGrain.GetMessageAsync();
        Assert.That(finalMsg.Status, Is.EqualTo("delivered"));
        Assert.That(finalMsg.MdnStatus, Is.EqualTo("processed"));
        Assert.That(finalMsg.IsEncrypted, Is.True);
        Assert.That(finalMsg.IsSigned, Is.True);
        Assert.That(finalMsg.CcdaContent, Does.Contain("ClinicalDocument"));

        pending = await msgIndex.GetPendingDeliveryAsync();
        Assert.That(pending.Any(m => m.MessageId == messageId), Is.False);
    }

    [Test]
    public async Task InboundMessage_ReceiveAndTrack()
    {
        string messageId = $"DIRECT-MSG:{Guid.NewGuid():N}";
        IDirectMessageGrain msgGrain = _cluster.GrainFactory.GetGrain<IDirectMessageGrain>(messageId);

        await msgGrain.CreateMessageAsync("inbound",
            "external.dr@direct.partner.org", "local.dr@direct.newvistas.health",
            "Discharge Summary: Patient Doe", "PAT-INBOUND", null,
            "Discharge", "<ClinicalDocument xmlns='urn:hl7-org:v3'><title>Discharge Summary</title></ClinicalDocument>",
            null);

        IDirectMessageIndexGrain msgIndex = _cluster.GrainFactory.GetGrain<IDirectMessageIndexGrain>("DIRECT-MSG-INDEX");
        await msgIndex.AddMessageAsync(new DirectMessageSummary
        {
            MessageId = messageId, Direction = "inbound",
            FromAddress = "external.dr@direct.partner.org",
            ToAddress = "local.dr@direct.newvistas.health",
            Subject = "Discharge Summary: Patient Doe",
            PatientId = "PAT-INBOUND", DocumentType = "Discharge",
            Status = "received", CreatedDate = DateTime.UtcNow, MdnStatus = "none"
        });

        // Verify inbound listing
        List<DirectMessageSummary> inbound = await msgIndex.GetInboundMessagesAsync();
        Assert.That(inbound.Any(m => m.MessageId == messageId), Is.True);

        DirectMessageState msg = await msgGrain.GetMessageAsync();
        Assert.That(msg.Direction, Is.EqualTo("inbound"));
        Assert.That(msg.Status, Is.EqualTo("received"));
        Assert.That(msg.CcdaContent, Does.Contain("Discharge Summary"));
    }

    [Test]
    public async Task MessageIndex_PatientFilter()
    {
        IDirectMessageIndexGrain index = _cluster.GrainFactory.GetGrain<IDirectMessageIndexGrain>(
            $"DIRECT-MSG-INDEX-PAT-{Guid.NewGuid():N}");

        string pat1 = $"PAT-{Guid.NewGuid():N}";
        string pat2 = $"PAT-{Guid.NewGuid():N}";

        await index.AddMessageAsync(new DirectMessageSummary
        {
            MessageId = "M1", PatientId = pat1, Direction = "outbound",
            Status = "sent", CreatedDate = DateTime.UtcNow, MdnStatus = "none"
        });
        await index.AddMessageAsync(new DirectMessageSummary
        {
            MessageId = "M2", PatientId = pat2, Direction = "inbound",
            Status = "received", CreatedDate = DateTime.UtcNow, MdnStatus = "none"
        });
        await index.AddMessageAsync(new DirectMessageSummary
        {
            MessageId = "M3", PatientId = pat1, Direction = "inbound",
            Status = "received", CreatedDate = DateTime.UtcNow, MdnStatus = "none"
        });

        List<DirectMessageSummary> pat1Msgs = await index.GetMessagesByPatientAsync(pat1);
        Assert.That(pat1Msgs, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task CcdaGenerator_ReferralVsCcd_DifferentTitles()
    {
        string patientId = $"DIRECT-FUNC-{Guid.NewGuid():N}";

        IDirectCcdaGeneratorGrain gen = _cluster.GrainFactory.GetGrain<IDirectCcdaGeneratorGrain>(
            $"DIRECT-CCDA-GEN:{patientId}");

        string ccd = await gen.GenerateCcdAsync("CCD");
        Assert.That(ccd, Does.Contain("Continuity of Care Document"));

        string referral = await gen.GenerateCcdAsync("Referral");
        Assert.That(referral, Does.Contain("Referral Summary"));

        string discharge = await gen.GenerateCcdAsync("Discharge");
        Assert.That(discharge, Does.Contain("Discharge Summary"));
    }

    [Test]
    public async Task AddressRegistry_MultipleProvidersAndOrganizations()
    {
        IDirectAddressIndexGrain index = _cluster.GrainFactory.GetGrain<IDirectAddressIndexGrain>(
            $"DIRECT-ADDR-INDEX-REG-{Guid.NewGuid():N}");

        await index.AddAddressAsync(new DirectAddressSummary
        {
            DirectAddress = "dr.a@direct.test", DisplayName = "Dr. A",
            OwnerType = "provider", IsActive = true, OrganizationName = "Clinic A"
        });
        await index.AddAddressAsync(new DirectAddressSummary
        {
            DirectAddress = "info@direct.test", DisplayName = "Clinic B",
            OwnerType = "organization", IsActive = true, OrganizationName = "Clinic B"
        });
        await index.AddAddressAsync(new DirectAddressSummary
        {
            DirectAddress = "dr.c@direct.test", DisplayName = "Dr. C",
            OwnerType = "provider", IsActive = false, OrganizationName = "Clinic A",
            CertificateExpiration = DateTime.UtcNow.AddDays(-30)
        });

        List<DirectAddressSummary> all = await index.GetAllAddressesAsync();
        Assert.That(all, Has.Count.EqualTo(3));

        List<DirectAddressSummary> active = await index.GetActiveAddressesAsync();
        Assert.That(active, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task FailedDelivery_RecordedInMessageAndIndex()
    {
        string messageId = $"DIRECT-MSG:{Guid.NewGuid():N}";
        IDirectMessageGrain msgGrain = _cluster.GrainFactory.GetGrain<IDirectMessageGrain>(messageId);

        await msgGrain.CreateMessageAsync("outbound",
            "a@test", "b@test", "Test", "PAT-FAIL", null, "CCD", "<doc/>", null);
        await msgGrain.MarkSentAsync(true, true, null, null);

        IDirectMessageIndexGrain index = _cluster.GrainFactory.GetGrain<IDirectMessageIndexGrain>(
            $"DIRECT-MSG-INDEX-FAIL-{Guid.NewGuid():N}");
        await index.AddMessageAsync(new DirectMessageSummary
        {
            MessageId = messageId, Direction = "outbound", Status = "sent",
            CreatedDate = DateTime.UtcNow, MdnStatus = "none"
        });

        // MDN denied
        await msgGrain.RecordMdnAsync("denied", "Recipient mailbox full");
        await index.UpdateMdnStatusAsync(messageId, "denied");

        DirectMessageState msg = await msgGrain.GetMessageAsync();
        Assert.That(msg.Status, Is.EqualTo("failed"));
        Assert.That(msg.MdnStatus, Is.EqualTo("denied"));

        List<DirectMessageSummary> all = await index.GetAllMessagesAsync();
        Assert.That(all[0].Status, Is.EqualTo("failed"));
    }
}
