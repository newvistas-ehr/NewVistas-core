// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers.HL7v2;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA Automated Lab Instruments (LA package).
/// Tests instrument configuration (#62.4), HL7v2 message parsing,
/// message queue processing (#62.49), auto-verify rules, and shipping manifests.
/// </summary>
[TestFixture]
public class LabInstrumentWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Helper Accessors ─────────────────────────────────────────

    private IAutoInstrumentGrain GetInstrument(string id) =>
        _cluster.GrainFactory.GetGrain<IAutoInstrumentGrain>(id);

    private IInstrumentIndexGrain GetIndex() =>
        _cluster.GrainFactory.GetGrain<IInstrumentIndexGrain>("LA-INST-INDEX");

    private IInstrumentMessageQueueGrain GetQueue(string instrumentId) =>
        _cluster.GrainFactory.GetGrain<IInstrumentMessageQueueGrain>(
            instrumentId.Replace("LA-INST:", "LA-MQ:"));

    private IAutoVerifyRulesGrain GetAutoVerify(string instrumentId) =>
        _cluster.GrainFactory.GetGrain<IAutoVerifyRulesGrain>(
            instrumentId.Replace("LA-INST:", "LA-AUTOVERIFY:"));

    private IShippingManifestGrain GetManifest(string id) =>
        _cluster.GrainFactory.GetGrain<IShippingManifestGrain>(id);

    private IShippingConfigGrain GetShipConfig(string id) =>
        _cluster.GrainFactory.GetGrain<IShippingConfigGrain>(id);

    // ─── Test 1: Instrument Configuration ─────────────────────────

    [Test]
    public async Task AutoInstrumentGrain_CanConfigureAndRetrieve()
    {
        string id = $"LA-INST:{Guid.NewGuid()}";
        IAutoInstrumentGrain grain = GetInstrument(id);

        await grain.UpdateConfigAsync(
            "Test Analyzer", InstrumentType.UNIVERSAL,
            "2.5.1", "192.168.1.100", 5555, 30,
            true, true, "TestCorp", "Model-X", "SN-12345",
            "CHEMISTRY", "Room 101", "TESTAPP", "TESTFAC");

        AutoInstrumentState config = await grain.GetConfigAsync();

        Assert.That(config.Name, Is.EqualTo("Test Analyzer"));
        Assert.That(config.InstrumentType, Is.EqualTo(InstrumentType.UNIVERSAL));
        Assert.That(config.TcpHost, Is.EqualTo("192.168.1.100"));
        Assert.That(config.TcpPort, Is.EqualTo(5555));
        Assert.That(config.AutoDownload, Is.True);
        Assert.That(config.AutoRelease, Is.True);
        Assert.That(config.Manufacturer, Is.EqualTo("TestCorp"));
    }

    // ─── Test 2: Instrument Enable/Disable ────────────────────────

    [Test]
    public async Task AutoInstrumentGrain_EnableDisableStatusTransitions()
    {
        string id = $"LA-INST:{Guid.NewGuid()}";
        IAutoInstrumentGrain grain = GetInstrument(id);

        await grain.UpdateConfigAsync("Status Test", InstrumentType.UNIVERSAL,
            null, null, null, null, false, false, null, null, null, null, null, null, null);

        AutoInstrumentState state = await grain.GetConfigAsync();
        Assert.That(state.Status, Is.EqualTo(InstrumentStatus.INACTIVE));

        await grain.EnableAsync();
        state = await grain.GetConfigAsync();
        Assert.That(state.Status, Is.EqualTo(InstrumentStatus.ACTIVE));

        await grain.SetMaintenanceModeAsync();
        state = await grain.GetConfigAsync();
        Assert.That(state.Status, Is.EqualTo(InstrumentStatus.MAINTENANCE));

        await grain.DisableAsync();
        state = await grain.GetConfigAsync();
        Assert.That(state.Status, Is.EqualTo(InstrumentStatus.INACTIVE));
    }

    // ─── Test 3: Test Mappings ────────────────────────────────────

    [Test]
    public async Task AutoInstrumentGrain_TestMappingsCRUD()
    {
        string id = $"LA-INST:{Guid.NewGuid()}";
        IAutoInstrumentGrain grain = GetInstrument(id);

        await grain.AddTestMappingAsync(new InstrumentTestMapping
        {
            ExternalTestCode = "GLU", InternalLoincCode = "2345-7",
            TestName = "Glucose", Specimen = "Serum", ResultUnits = "mg/dL"
        });

        await grain.AddTestMappingAsync(new InstrumentTestMapping
        {
            ExternalTestCode = "BUN", InternalLoincCode = "3094-0",
            TestName = "BUN", Specimen = "Serum", ResultUnits = "mg/dL"
        });

        List<InstrumentTestMapping> mappings = await grain.GetTestMappingsAsync();
        Assert.That(mappings, Has.Count.EqualTo(2));

        // Resolve mapping
        InstrumentTestMapping? resolved = await grain.ResolveTestMappingAsync("GLU");
        Assert.That(resolved, Is.Not.Null);
        Assert.That(resolved!.InternalLoincCode, Is.EqualTo("2345-7"));

        // Remove mapping
        await grain.RemoveTestMappingAsync("BUN");
        mappings = await grain.GetTestMappingsAsync();
        Assert.That(mappings, Has.Count.EqualTo(1));
    }

    // ─── Test 4: Instrument Index Self-Seeding ────────────────────

    [Test]
    public async Task InstrumentIndexGrain_SelfSeedsWithDemoInstruments()
    {
        List<InstrumentEntry> instruments = await GetIndex().GetAllInstrumentsAsync();

        Assert.That(instruments, Has.Count.GreaterThanOrEqualTo(6));
        Assert.That(instruments.Any(i => i.Name == "Beckman AU5800"), Is.True);
        Assert.That(instruments.Any(i => i.Name == "Sysmex XN-1000"), Is.True);
        Assert.That(instruments.Any(i => i.Name == "Abbott i-STAT 1"), Is.True);
    }

    // ─── Test 5: Instrument Index Search ──────────────────────────

    [Test]
    public async Task InstrumentIndexGrain_SearchByNameAndSection()
    {
        await GetIndex().GetAllInstrumentsAsync(); // Ensure seeded

        List<InstrumentEntry> results = await GetIndex().SearchInstrumentsAsync("Beckman");
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(results[0].Name, Does.Contain("Beckman"));

        results = await GetIndex().SearchInstrumentsAsync("HEMATOLOGY");
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
    }

    // ─── Test 6: HL7v2 Parser — ORU^R01 ──────────────────────────

    [Test]
    public void HL7Parser_ParsesORU_R01Message()
    {
        string oru = BuildSampleORU("PATIENT-001", "GLU", "Glucose", "95", "mg/dL", "70-100", "N");

        HL7Message msg = HL7Parser.Parse(oru);

        Assert.That(msg.MessageType, Is.EqualTo("ORU"));
        Assert.That(msg.TriggerEvent, Is.EqualTo("R01"));
        Assert.That(msg.SendingApplication, Is.EqualTo("ANALYZER"));
        Assert.That(msg.VersionId, Is.EqualTo("2.5.1"));

        HL7PatientId? patient = HL7Parser.ExtractPatientId(msg);
        Assert.That(patient, Is.Not.Null);
        Assert.That(patient!.PatientId, Is.EqualTo("PATIENT-001"));

        List<HL7ObservationResult> results = HL7Parser.ExtractObservationResults(msg);
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].ObservationValue, Is.EqualTo("95"));
        Assert.That(results[0].Units, Is.EqualTo("mg/dL"));
        Assert.That(results[0].AbnormalFlags, Is.EqualTo("N"));
    }

    // ─── Test 7: HL7v2 Parser — Multi-OBX ────────────────────────

    [Test]
    public void HL7Parser_ParsesMultipleOBXSegments()
    {
        string oru = BuildMultiOBXMessage("PATIENT-002");

        HL7Message msg = HL7Parser.Parse(oru);

        List<HL7ObservationResult> results = HL7Parser.ExtractObservationResults(msg);
        Assert.That(results, Has.Count.EqualTo(3));

        List<HL7ObservationRequest> requests = HL7Parser.ExtractObservationRequests(msg);
        Assert.That(requests, Has.Count.EqualTo(1));
        Assert.That(requests[0].UniversalServiceText, Is.EqualTo("CBC"));
    }

    // ─── Test 8: HL7v2 Builder — ACK ─────────────────────────────

    [Test]
    public void HL7Builder_BuildsValidACKMessage()
    {
        string ack = HL7Builder.BuildACK("CTRL-123", "AA");

        HL7Message parsed = HL7Parser.Parse(ack);

        Assert.That(parsed.MessageType, Is.EqualTo("ACK"));
        Assert.That(parsed.GetSegment("MSA"), Is.Not.Null);
        Assert.That(parsed.GetSegment("MSA")!.GetField(1), Is.EqualTo("AA"));
        Assert.That(parsed.GetSegment("MSA")!.GetField(2), Is.EqualTo("CTRL-123"));
    }

    // ─── Test 9: HL7v2 MLLP Framing ──────────────────────────────

    [Test]
    public void HL7Parser_StripsMllpFraming()
    {
        string oru = BuildSampleORU("PAT-003", "K", "Potassium", "4.2", "mmol/L", "3.5-5.1", "N");
        string mllp = HL7Builder.WrapMllp(oru);

        // Verify framing chars present
        Assert.That(mllp[0], Is.EqualTo('\x0B'));
        Assert.That(mllp[^2], Is.EqualTo('\x1C'));

        // Parser should handle MLLP-framed message
        HL7Message parsed = HL7Parser.Parse(mllp);
        Assert.That(parsed.MessageType, Is.EqualTo("ORU"));
    }

    // ─── Test 10: Message Queue — Enqueue and Process ─────────────

    [Test]
    public async Task MessageQueueGrain_EnqueueAndProcessORU()
    {
        string instId = $"LA-INST:TEST-MQ-{Guid.NewGuid():N}"[..30];
        string mqKey = instId.Replace("LA-INST:", "LA-MQ:");
        IInstrumentMessageQueueGrain queue = _cluster.GrainFactory
            .GetGrain<IInstrumentMessageQueueGrain>(mqKey);

        string oru = BuildSampleORU("PATIENT-MQ-001", "GLU", "Glucose", "95", "mg/dL", "70-100", "N");
        string messageId = await queue.EnqueueMessageAsync(oru);

        Assert.That(messageId, Does.StartWith("MSG-"));

        int depth = await queue.GetQueueDepthAsync();
        Assert.That(depth, Is.EqualTo(1));

        bool processed = await queue.ProcessNextAsync();
        Assert.That(processed, Is.True);

        depth = await queue.GetQueueDepthAsync();
        Assert.That(depth, Is.EqualTo(0));

        InstrumentMessageQueueState stats = await queue.GetStatsAsync();
        Assert.That(stats.ProcessedCount, Is.EqualTo(1));
    }

    // ─── Test 11: Message Queue — ProcessAll ──────────────────────

    [Test]
    public async Task MessageQueueGrain_ProcessAllMultipleMessages()
    {
        string mqKey = $"LA-MQ:BATCH-{Guid.NewGuid():N}"[..25];
        IInstrumentMessageQueueGrain queue = _cluster.GrainFactory
            .GetGrain<IInstrumentMessageQueueGrain>(mqKey);

        await queue.EnqueueMessageAsync(BuildSampleORU("PAT-B1", "GLU", "Glucose", "90", "mg/dL", "70-100", "N"));
        await queue.EnqueueMessageAsync(BuildSampleORU("PAT-B2", "BUN", "BUN", "15", "mg/dL", "7-20", "N"));

        int count = await queue.ProcessAllAsync();

        Assert.That(count, Is.EqualTo(2));

        List<MessageLogEntry> log = await queue.GetRecentLogAsync();
        Assert.That(log, Has.Count.EqualTo(2));
        Assert.That(log.All(e => e.Status == MessageProcessingStatus.COMPLETED), Is.True);
    }

    // ─── Test 12: Auto-Verify — Accept Normal Result ──────────────

    [Test]
    public async Task AutoVerifyRules_AcceptsNormalResult()
    {
        string instId = $"LA-AUTOVERIFY:AV-{Guid.NewGuid():N}"[..30];
        IAutoVerifyRulesGrain rules = _cluster.GrainFactory
            .GetGrain<IAutoVerifyRulesGrain>(instId);

        await rules.UpdateGlobalRulesAsync(true, 60, false, false);
        await rules.AddTestRuleAsync(new TestVerifyRule
        {
            LoincCode = "2345-7", TestName = "Glucose",
            NormalLow = 70, NormalHigh = 100,
            CriticalLow = 40, CriticalHigh = 500,
            AutoVerifyEnabled = true
        });

        AutoVerifyDecision decision = await rules.EvaluateResultAsync("2345-7", "85", null);
        Assert.That(decision, Is.EqualTo(AutoVerifyDecision.ACCEPT));
    }

    // ─── Test 13: Auto-Verify — Review Out-of-Range ───────────────

    [Test]
    public async Task AutoVerifyRules_ReviewsOutOfRangeResult()
    {
        string instId = $"LA-AUTOVERIFY:AV-{Guid.NewGuid():N}"[..30];
        IAutoVerifyRulesGrain rules = _cluster.GrainFactory
            .GetGrain<IAutoVerifyRulesGrain>(instId);

        await rules.UpdateGlobalRulesAsync(true, 60, false, false);
        await rules.AddTestRuleAsync(new TestVerifyRule
        {
            LoincCode = "2345-7", TestName = "Glucose",
            NormalLow = 70, NormalHigh = 100,
            CriticalLow = 40, CriticalHigh = 500,
            AutoVerifyEnabled = true
        });

        AutoVerifyDecision decision = await rules.EvaluateResultAsync("2345-7", "120", null);
        Assert.That(decision, Is.EqualTo(AutoVerifyDecision.REVIEW));
    }

    // ─── Test 14: Auto-Verify — Critical Alert ────────────────────

    [Test]
    public async Task AutoVerifyRules_CriticalAlertForExtremeResult()
    {
        string instId = $"LA-AUTOVERIFY:AV-{Guid.NewGuid():N}"[..30];
        IAutoVerifyRulesGrain rules = _cluster.GrainFactory
            .GetGrain<IAutoVerifyRulesGrain>(instId);

        await rules.UpdateGlobalRulesAsync(true, 60, false, false);
        await rules.AddTestRuleAsync(new TestVerifyRule
        {
            LoincCode = "2823-3", TestName = "Potassium",
            NormalLow = 3.5, NormalHigh = 5.1,
            CriticalLow = 2.5, CriticalHigh = 6.5,
            AutoVerifyEnabled = true
        });

        AutoVerifyDecision decision = await rules.EvaluateResultAsync("2823-3", "7.2", null);
        Assert.That(decision, Is.EqualTo(AutoVerifyDecision.CRITICAL_ALERT));

        decision = await rules.EvaluateResultAsync("2823-3", "2.0", null);
        Assert.That(decision, Is.EqualTo(AutoVerifyDecision.CRITICAL_ALERT));
    }

    // ─── Test 15: Shipping Manifest Lifecycle ─────────────────────

    [Test]
    public async Task ShippingManifest_FullLifecycle()
    {
        string manifestId = $"LA-SHIP:{Guid.NewGuid()}";
        IShippingManifestGrain manifest = GetManifest(manifestId);

        await manifest.CreateManifestAsync(
            "CONFIG-1", "Quest Diagnostics", "FedEx Priority",
            "Refrigerated", "Styrofoam Box", "Lab Tech 1");

        ShippingManifestState state = await manifest.GetManifestAsync();
        Assert.That(state.Status, Is.EqualTo(ShippingManifestStatus.OPEN));
        Assert.That(state.DestinationLab, Is.EqualTo("Quest Diagnostics"));

        // Add specimens
        await manifest.AddSpecimenAsync(new ManifestSpecimen
        {
            SpecimenId = "SPEC-001", PatientId = "PAT-001",
            TestName = "TSH", LoincCode = "3016-3", SpecimenType = "Serum"
        });
        await manifest.AddSpecimenAsync(new ManifestSpecimen
        {
            SpecimenId = "SPEC-002", PatientId = "PAT-002",
            TestName = "Free T4", LoincCode = "3024-7", SpecimenType = "Serum"
        });

        state = await manifest.GetManifestAsync();
        Assert.That(state.Specimens, Has.Count.EqualTo(2));

        // Remove one specimen
        await manifest.RemoveSpecimenAsync("SPEC-002");
        state = await manifest.GetManifestAsync();
        Assert.That(state.Specimens.First(s => s.SpecimenId == "SPEC-002").IsRemoved, Is.True);

        // Ship
        await manifest.ShipManifestAsync("FEDEX-123456");
        state = await manifest.GetManifestAsync();
        Assert.That(state.Status, Is.EqualTo(ShippingManifestStatus.SHIPPED));
        Assert.That(state.TrackingNumber, Is.EqualTo("FEDEX-123456"));

        // Receive
        await manifest.MarkReceivedAsync();
        state = await manifest.GetManifestAsync();
        Assert.That(state.Status, Is.EqualTo(ShippingManifestStatus.RECEIVED));
    }

    // ─── Test 16: Shipping Config ─────────────────────────────────

    [Test]
    public async Task ShippingConfigGrain_ConfiguresReferenceLab()
    {
        string configId = $"LA-SHIPCFG:{Guid.NewGuid()}";
        IShippingConfigGrain config = GetShipConfig(configId);

        await config.UpdateConfigAsync(
            "Quest Diagnostics", "500 Plaza Dr, Secaucus NJ",
            "800-222-0446", "FedEx Priority", "Refrigerated",
            "Styrofoam Box", true,
            new List<string> { "3016-3", "3024-7", "5196-1" });

        ShippingConfigState state = await config.GetConfigAsync();
        Assert.That(state.LabName, Is.EqualTo("Quest Diagnostics"));
        Assert.That(state.IsActive, Is.True);
        Assert.That(state.AcceptedTests, Has.Count.EqualTo(3));
        Assert.That(state.AcceptedTests, Contains.Item("3016-3"));
    }

    // ─── HL7 Test Message Builders ────────────────────────────────

    private static string BuildSampleORU(string patientId, string testCode,
        string testName, string value, string units, string refRange, string flag)
    {
        string ts = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        return string.Join("\r",
            $"MSH|^~\\&|ANALYZER|MAIN_LAB|NEWVISTAS|LAB|{ts}||ORU^R01|CTRL-{Guid.NewGuid():N}|P|2.5.1",
            $"PID|1||{patientId}||DOE^JOHN||19500101|M",
            $"OBR|1|ORD-001||{testCode}^{testName}^LN|||{ts}",
            $"OBX|1|NM|{testCode}^{testName}^LN||{value}|{units}|{refRange}|{flag}|||F|||{ts}");
    }

    private static string BuildMultiOBXMessage(string patientId)
    {
        string ts = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        return string.Join("\r",
            $"MSH|^~\\&|ANALYZER|MAIN_LAB|NEWVISTAS|LAB|{ts}||ORU^R01|CTRL-{Guid.NewGuid():N}|P|2.5.1",
            $"PID|1||{patientId}||DOE^JANE||19600601|F",
            $"OBR|1|ORD-002||CBC^CBC^LN|||{ts}",
            $"OBX|1|NM|6690-2^WBC^LN||7.5|K/cmm|4.5-11.0|N|||F|||{ts}",
            $"OBX|2|NM|718-7^HGB^LN||14.2|g/dL|12.0-17.5|N|||F|||{ts}",
            $"OBX|3|NM|777-3^PLT^LN||250|K/cmm|150-400|N|||F|||{ts}");
    }
}
