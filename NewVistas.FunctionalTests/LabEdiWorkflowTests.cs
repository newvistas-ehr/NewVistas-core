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
/// Functional tests for Lab EDI (Electronic Data Interchange) — external reference
/// lab ordering, result receipt, configuration, queue management, and index.
/// </summary>
[TestFixture]
public class LabEdiWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ILabEdiOrderGrain GetOrder(string id)
        => _cluster.GrainFactory.GetGrain<ILabEdiOrderGrain>($"LAB-EDI-ORD:{id}");

    private ILabEdiResultGrain GetResult(string id)
        => _cluster.GrainFactory.GetGrain<ILabEdiResultGrain>($"LAB-EDI-RES:{id}");

    private ILabEdiConfigGrain GetConfig(string id)
        => _cluster.GrainFactory.GetGrain<ILabEdiConfigGrain>($"LAB-EDI-CFG:{id}");

    private ILabEdiQueueGrain GetQueue(string id)
        => _cluster.GrainFactory.GetGrain<ILabEdiQueueGrain>($"LAB-EDI-Q:{id}");

    private ILabEdiIndexGrain GetIndex()
        => _cluster.GrainFactory.GetGrain<ILabEdiIndexGrain>("LAB-EDI-INDEX");

    private List<LabEdiTestRequest> MakeTestRequests(int count)
    {
        List<LabEdiTestRequest> tests = new();
        for (int i = 0; i < count; i++)
        {
            tests.Add(new LabEdiTestRequest
            {
                TestCode = $"TC-{i + 1}",
                TestName = $"Test {i + 1}",
                LoincCode = $"1234-{i + 1}",
                SpecimenRequirements = "BLOOD"
            });
        }
        return tests;
    }

    private List<LabEdiResultItem> MakeResultItems(int count)
    {
        List<LabEdiResultItem> items = new();
        for (int i = 0; i < count; i++)
        {
            items.Add(new LabEdiResultItem
            {
                TestCode = $"TC-{i + 1}",
                TestName = $"Test {i + 1}",
                LoincCode = $"1234-{i + 1}",
                Value = $"{(i + 1) * 10}",
                Units = "mg/dL",
                ReferenceRange = "5-50",
                AbnormalFlag = "N",
                ResultStatus = "F"
            });
        }
        return items;
    }

    // --- Order Tests ---

    [Test]
    public async Task LabEdiOrder_CreateOrder_PersistsPatient()
    {
        string orderId = $"ORD-{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        ILabEdiOrderGrain grain = GetOrder(orderId);

        await grain.CreateOrderAsync(patientId, "SMITH,JOHN A", "Quest Diagnostics", "QUEST-001",
            "PROV-001", "Dr. Smith", MakeTestRequests(2),
            "Routine screening", "BLOOD", "SPEC-001", DateTime.UtcNow, "ROUTINE");

        LabEdiOrderState state = await grain.GetOrderAsync();
        Assert.That(state.PatientId, Is.EqualTo(patientId));
    }

    [Test]
    public async Task LabEdiOrder_CreateOrder_PersistsTests()
    {
        string orderId = $"ORD-{Guid.NewGuid()}";
        ILabEdiOrderGrain grain = GetOrder(orderId);

        await grain.CreateOrderAsync($"PATIENT-{Guid.NewGuid()}", "DOE,JANE B", "LabCorp", "LABCORP-001",
            "PROV-002", "Dr. Jones", MakeTestRequests(3),
            "Follow-up", "SERUM", "SPEC-002", DateTime.UtcNow, "STAT");

        LabEdiOrderState state = await grain.GetOrderAsync();
        Assert.That(state.TestsRequested, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task LabEdiOrder_CreateOrder_DefaultStatusDraft()
    {
        string orderId = $"ORD-{Guid.NewGuid()}";
        ILabEdiOrderGrain grain = GetOrder(orderId);

        await grain.CreateOrderAsync($"PATIENT-{Guid.NewGuid()}", "BROWN,ALICE D", "ARUP", "ARUP-001",
            "PROV-003", "Dr. Brown", MakeTestRequests(1),
            null, "URINE", null, DateTime.UtcNow, "ROUTINE");

        LabEdiOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Status, Is.EqualTo(LabEdiOrderStatus.Draft));
    }

    [Test]
    public async Task LabEdiOrder_MarkTransmitted_UpdatesStatus()
    {
        string orderId = $"ORD-{Guid.NewGuid()}";
        ILabEdiOrderGrain grain = GetOrder(orderId);
        await grain.CreateOrderAsync($"PATIENT-{Guid.NewGuid()}", "WILSON,MARY E", "Quest Diagnostics", "QUEST-001",
            "PROV-001", "Dr. Smith", MakeTestRequests(2),
            null, "BLOOD", null, DateTime.UtcNow, "ROUTINE");

        await grain.MarkTransmittedAsync($"HL7-{Guid.NewGuid()}", DateTime.UtcNow);

        LabEdiOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Status, Is.EqualTo(LabEdiOrderStatus.Transmitted));
    }

    [Test]
    public async Task LabEdiOrder_MarkAcknowledged_UpdatesStatus()
    {
        string orderId = $"ORD-{Guid.NewGuid()}";
        ILabEdiOrderGrain grain = GetOrder(orderId);
        await grain.CreateOrderAsync($"PATIENT-{Guid.NewGuid()}", "TAYLOR,JAMES F", "LabCorp", "LABCORP-001",
            "PROV-002", "Dr. Jones", MakeTestRequests(1),
            null, "BLOOD", null, DateTime.UtcNow, "ROUTINE");
        await grain.MarkTransmittedAsync($"HL7-{Guid.NewGuid()}", DateTime.UtcNow);

        await grain.MarkAcknowledgedAsync("AA", DateTime.UtcNow);

        LabEdiOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Status, Is.EqualTo(LabEdiOrderStatus.Acknowledged));
    }

    [Test]
    public async Task LabEdiOrder_Cancel_UpdatesStatus()
    {
        string orderId = $"ORD-{Guid.NewGuid()}";
        ILabEdiOrderGrain grain = GetOrder(orderId);
        await grain.CreateOrderAsync($"PATIENT-{Guid.NewGuid()}", "GARCIA,MARIA G", "ARUP", "ARUP-001",
            "PROV-003", "Dr. Brown", MakeTestRequests(2),
            null, "BLOOD", null, DateTime.UtcNow, "ROUTINE");

        await grain.CancelOrderAsync("Patient refused", "USER-001");

        LabEdiOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Status, Is.EqualTo(LabEdiOrderStatus.Cancelled));
        Assert.That(state.CancelledReason, Is.EqualTo("Patient refused"));
    }

    [Test]
    public async Task LabEdiOrder_MarkRejected_UpdatesStatus()
    {
        string orderId = $"ORD-{Guid.NewGuid()}";
        ILabEdiOrderGrain grain = GetOrder(orderId);
        await grain.CreateOrderAsync($"PATIENT-{Guid.NewGuid()}", "MARTINEZ,CARLOS H", "Quest Diagnostics", "QUEST-001",
            "PROV-001", "Dr. Smith", MakeTestRequests(1),
            null, "BLOOD", null, DateTime.UtcNow, "STAT");
        await grain.MarkTransmittedAsync($"HL7-{Guid.NewGuid()}", DateTime.UtcNow);

        await grain.MarkRejectedAsync("Specimen hemolyzed", DateTime.UtcNow);

        LabEdiOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Status, Is.EqualTo(LabEdiOrderStatus.Rejected));
    }

    // --- Result Tests ---

    [Test]
    public async Task LabEdiResult_RecordResult_PersistsFields()
    {
        string resultId = $"RES-{Guid.NewGuid()}";
        string orderId = $"ORD-{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        ILabEdiResultGrain grain = GetResult(resultId);
        List<LabEdiResultItem> items = MakeResultItems(3);

        await grain.RecordResultAsync(orderId, patientId, "Quest Diagnostics", "QUEST-001",
            $"HL7-{Guid.NewGuid()}", items, DateTime.UtcNow, "11D1234567");

        LabEdiResultState state = await grain.GetResultAsync();
        Assert.That(state.OrderId, Is.EqualTo(orderId));
        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.Results, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task LabEdiResult_ReviewResult_UpdatesStatus()
    {
        string resultId = $"RES-{Guid.NewGuid()}";
        ILabEdiResultGrain grain = GetResult(resultId);
        await grain.RecordResultAsync($"ORD-{Guid.NewGuid()}", $"PATIENT-{Guid.NewGuid()}",
            "LabCorp", "LABCORP-001", $"HL7-{Guid.NewGuid()}",
            MakeResultItems(2), DateTime.UtcNow, "22D7654321");

        await grain.ReviewResultAsync("USER-LAB-001", "Dr. Lab Tech", DateTime.UtcNow, "Results look normal");

        LabEdiResultState state = await grain.GetResultAsync();
        Assert.That(state.Status, Is.EqualTo(LabEdiResultStatus.Reviewed));
    }

    [Test]
    public async Task LabEdiResult_AcceptResult_UpdatesStatus()
    {
        string resultId = $"RES-{Guid.NewGuid()}";
        ILabEdiResultGrain grain = GetResult(resultId);
        await grain.RecordResultAsync($"ORD-{Guid.NewGuid()}", $"PATIENT-{Guid.NewGuid()}",
            "ARUP", "ARUP-001", $"HL7-{Guid.NewGuid()}",
            MakeResultItems(1), DateTime.UtcNow, "33D1112233");
        await grain.ReviewResultAsync("USER-LAB-002", "Dr. Reviewer", DateTime.UtcNow, null);

        await grain.AcceptResultAsync("USER-LAB-002");

        LabEdiResultState state = await grain.GetResultAsync();
        Assert.That(state.Status, Is.EqualTo(LabEdiResultStatus.Accepted));
    }

    [Test]
    public async Task LabEdiResult_RejectResult_UpdatesStatus()
    {
        string resultId = $"RES-{Guid.NewGuid()}";
        ILabEdiResultGrain grain = GetResult(resultId);
        await grain.RecordResultAsync($"ORD-{Guid.NewGuid()}", $"PATIENT-{Guid.NewGuid()}",
            "Quest Diagnostics", "QUEST-001", $"HL7-{Guid.NewGuid()}",
            MakeResultItems(2), DateTime.UtcNow, "44D4455667");
        await grain.ReviewResultAsync("USER-LAB-003", "Dr. Pathologist", DateTime.UtcNow, "Suspicious values");

        await grain.RejectResultAsync("USER-LAB-003", "Results appear contaminated");

        LabEdiResultState state = await grain.GetResultAsync();
        Assert.That(state.Status, Is.EqualTo(LabEdiResultStatus.Rejected));
        Assert.That(state.RejectedReason, Is.EqualTo("Results appear contaminated"));
    }

    // --- Config Tests ---

    [Test]
    public async Task LabEdiConfig_Configure_PersistsFields()
    {
        string configId = $"CFG-{Guid.NewGuid()}";
        ILabEdiConfigGrain grain = GetConfig(configId);

        await grain.ConfigureAsync("Quest Diagnostics", "11D1234567", "TCP/MLLP",
            "edi.quest.com", 5050, null, "2.5.1",
            "VAMC-500", "VA Palo Alto HCS", true, false);

        LabEdiConfigState state = await grain.GetConfigAsync();
        Assert.That(state.LabName, Is.EqualTo("Quest Diagnostics"));
        Assert.That(state.CliaNumber, Is.EqualTo("11D1234567"));
        Assert.That(state.ConnectionType, Is.EqualTo("TCP/MLLP"));
    }

    [Test]
    public async Task LabEdiConfig_ActivateDeactivate_TogglesStatus()
    {
        string configId = $"CFG-{Guid.NewGuid()}";
        ILabEdiConfigGrain grain = GetConfig(configId);
        await grain.ConfigureAsync("LabCorp", "22D7654321", "SFTP",
            null, null, "/sftp/labcorp/outbound", "2.3.1",
            "VAMC-612", "VA Portland HCS", false, true);

        await grain.DeactivateAsync();
        bool afterDeactivate = await grain.IsActiveAsync();
        Assert.That(afterDeactivate, Is.False);

        await grain.ActivateAsync();
        bool afterActivate = await grain.IsActiveAsync();
        Assert.That(afterActivate, Is.True);
    }

    [Test]
    public async Task LabEdiConfig_UpdateTestMapping_PersistsMapping()
    {
        string configId = $"CFG-{Guid.NewGuid()}";
        ILabEdiConfigGrain grain = GetConfig(configId);
        await grain.ConfigureAsync("ARUP Laboratories", "33D1112233", "HTTPS",
            "api.arup.com", 443, null, "2.5.1",
            "VAMC-660", "VA Salt Lake City HCS", true, true);

        await grain.UpdateTestMappingAsync("LOCAL-CBC", "ARUP-CBC-001", "58410-2");

        LabEdiConfigState state = await grain.GetConfigAsync();
        Assert.That(state.TestMappings.ContainsKey("LOCAL-CBC"), Is.True);
    }

    // --- Queue Tests ---

    [Test]
    public async Task LabEdiQueue_EnqueueOrder_AppearsInPending()
    {
        string queueId = $"Q-{Guid.NewGuid()}";
        ILabEdiQueueGrain grain = GetQueue(queueId);
        string orderId = $"ORD-{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";

        await grain.EnqueueOrderAsync(orderId, patientId, "SMITH,JOHN A", DateTime.UtcNow);

        List<LabEdiQueueEntry> pending = await grain.GetPendingOrdersAsync();
        Assert.That(pending, Has.Count.EqualTo(1));
    }

    // --- Index Tests ---

    [Test]
    public async Task LabEdiIndex_SeedDemoData_Creates3Labs()
    {
        ILabEdiIndexGrain index = GetIndex();

        await index.SeedDemoDataAsync();

        List<LabEdiLabSummary> labs = await index.GetReferenceLabsAsync();
        Assert.That(labs, Has.Count.EqualTo(3));
        Assert.That(labs.Any(l => l.LabName.Contains("Quest")), Is.True);
        Assert.That(labs.Any(l => l.LabName.Contains("LabCorp")), Is.True);
        Assert.That(labs.Any(l => l.LabName.Contains("ARUP")), Is.True);
    }
}
