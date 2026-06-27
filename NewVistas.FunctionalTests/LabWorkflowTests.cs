// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.FunctionalTests;

[TestFixture]
public class LabWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ILabTestGrain GetLabTestGrain(string id) =>
        _cluster.GrainFactory.GetGrain<ILabTestGrain>(id);

    private IPatientWorkflowGrain GetWorkflow(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPatientLabSummary GetSummaryGrain(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientLabSummary>($"PatientLabSummary/{patientId}");

    private IPatientLabBatch GetBatchGrain(string key) =>
        _cluster.GrainFactory.GetGrain<IPatientLabBatch>(key);

    private IPatientLabIndex GetIndexGrain(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientLabIndex>($"PatientLabIndex/{patientId}");

    // ── 1 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task LabTestGrain_OrderTest_PersistsCore()
    {
        // Arrange
        string labTestId = $"LAB-{Guid.NewGuid()}";
        ILabTestGrain grain = GetLabTestGrain(labTestId);

        // Act
        await grain.OrderLabTestAsync(
            "PAT-001", "LR-2160-0", "Creatinine", "2160-0",
            null, "PROV-001", "Dr. Smith", "Serum", "CHEMISTRY");

        // Assert
        LabTestState state = await grain.GetLabTestAsync();
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.TestName, Is.EqualTo("Creatinine"));
        Assert.That(state.TestCode, Is.EqualTo("2160-0"));
        Assert.That(state.Category, Is.EqualTo("CHEMISTRY"));
        Assert.That(state.SpecimenType, Is.EqualTo("Serum"));
        Assert.That(state.Status, Is.EqualTo("Ordered"));
    }

    // ── 2 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task LabTestGrain_CollectSpecimen_UpdatesStatusAndDate()
    {
        // Arrange
        string labTestId = $"LAB-{Guid.NewGuid()}";
        ILabTestGrain grain = GetLabTestGrain(labTestId);
        await grain.OrderLabTestAsync(
            "PAT-001", "LR-718-7", "HGB", "718-7",
            null, "PROV-001", "Dr. Smith", "Blood", "HEMATOLOGY");

        // Act
        DateTime collectionTime = new DateTime(2025, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        await grain.CollectSpecimenAsync(collectionTime, "LAVENDER", "Hematology Lab");

        // Assert
        LabTestState state = await grain.GetLabTestAsync();
        Assert.That(state.Status, Is.EqualTo("Collected"));
        Assert.That(state.CollectionDateTime, Is.EqualTo(collectionTime));
        Assert.That(state.CollectionSample, Is.EqualTo("LAVENDER"));
        Assert.That(state.PerformingLab, Is.EqualTo("Hematology Lab"));
    }

    // ── 3 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task LabTestGrain_RecordResult_PopulatesResultFields()
    {
        // Arrange
        string labTestId = $"LAB-{Guid.NewGuid()}";
        ILabTestGrain grain = GetLabTestGrain(labTestId);
        await grain.OrderLabTestAsync(
            "PAT-001", "LR-6690-2", "WBC", "6690-2",
            null, "PROV-001", "Dr. Smith", "Blood", "HEMATOLOGY");
        await grain.CollectSpecimenAsync(DateTime.UtcNow.AddHours(-2), "LAVENDER", "Hematology Lab");

        // Act
        DateTime resultTime = DateTime.UtcNow.AddHours(-1);
        await grain.RecordResultAsync(resultTime, "7.5", "K/cmm", "4.5", "11.0", null);

        // Assert
        LabTestState state = await grain.GetLabTestAsync();
        Assert.That(state.ResultValue, Is.EqualTo("7.5"));
        Assert.That(state.ResultUnit, Is.EqualTo("K/cmm"));
        Assert.That(state.ReferenceRangeLow, Is.EqualTo("4.5"));
        Assert.That(state.ReferenceRangeHigh, Is.EqualTo("11.0"));
        Assert.That(state.Status, Is.EqualTo("Pending")); // Verify step promotes to Completed
        Assert.That(await grain.IsAbnormalAsync(), Is.False);
    }

    // ── 4 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task LabTestGrain_VerifyResult_SetsVerificationFields()
    {
        // Arrange
        string labTestId = $"LAB-{Guid.NewGuid()}";
        ILabTestGrain grain = GetLabTestGrain(labTestId);
        await grain.OrderLabTestAsync(
            "PAT-001", "LR-2160-0", "Creatinine", "2160-0",
            null, "PROV-001", "Dr. Smith", "Serum", "CHEMISTRY");
        await grain.CollectSpecimenAsync(DateTime.UtcNow.AddHours(-3), null, null);
        await grain.RecordResultAsync(DateTime.UtcNow.AddHours(-1), "1.1", "mg/dL", "0.7", "1.3", null);

        // Act
        DateTime verifiedAt = DateTime.UtcNow;
        await grain.VerifyResultAsync("PATH-001", "Dr. Pathologist", verifiedAt);

        // Assert
        LabTestState state = await grain.GetLabTestAsync();
        Assert.That(state.VerifyingProviderId, Is.EqualTo("PATH-001"));
        Assert.That(state.VerifyingProviderName, Is.EqualTo("Dr. Pathologist"));
        Assert.That(state.VerifiedDateTime, Is.EqualTo(verifiedAt).Within(TimeSpan.FromSeconds(1)));
    }

    // ── 5 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task LabTestGrain_CancelTest_UpdatesStatus()
    {
        // Arrange
        string labTestId = $"LAB-{Guid.NewGuid()}";
        ILabTestGrain grain = GetLabTestGrain(labTestId);
        await grain.OrderLabTestAsync(
            "PAT-001", "LR-777-3", "PLT", "777-3",
            null, null, null, "Blood", "HEMATOLOGY");

        // Act
        await grain.CancelLabTestAsync();

        // Assert
        LabTestState state = await grain.GetLabTestAsync();
        Assert.That(state.Status, Is.EqualTo("Cancelled"));
    }

    // ── 6 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task LabTestGrain_MarkAsCritical_SetsCriticalFlag()
    {
        // Arrange
        string labTestId = $"LAB-{Guid.NewGuid()}";
        ILabTestGrain grain = GetLabTestGrain(labTestId);
        await grain.OrderLabTestAsync(
            "PAT-001", "LR-2823-3", "Potassium", "2823-3",
            null, "PROV-001", "Dr. Smith", "Serum", "CHEMISTRY");
        await grain.CollectSpecimenAsync(DateTime.UtcNow.AddHours(-2), null, null);
        await grain.RecordResultAsync(DateTime.UtcNow.AddHours(-1), "6.8", "mEq/L", "3.5", "5.0", "CH");

        // Act
        await grain.MarkAsCriticalAsync();

        // Assert
        LabTestState state = await grain.GetLabTestAsync();
        Assert.That(state.IsCritical, Is.True);
        Assert.That(await grain.IsCriticalAsync(), Is.True);
    }

    // ── 7 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task PatientLabSummaryGrain_RecordResult_UpdatesSummary()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientLabSummary summaryGrain = GetSummaryGrain(patientId);

        var evt = new LabResultEvent
        {
            PatientIcn = patientId,
            ResultId = $"R-{Guid.NewGuid()}",
            LoincCode = "2160-0",
            TestName = "Creatinine",
            Value = "1.2",
            Units = "mg/dL",
            ReferenceRange = "0.7-1.3",
            AbnormalFlag = LabAbnormalFlag.Normal,
            ResultDate = DateTimeOffset.UtcNow,
            FacilityCode = "688"
        };

        // Act
        await summaryGrain.RecordNewResult(evt);

        // Assert
        LabTestSummaryEntry? current = await summaryGrain.GetCurrentResult("2160-0");
        Assert.That(current, Is.Not.Null);
        Assert.That(current!.Value, Is.EqualTo("1.2"));
        Assert.That(current.TestName, Is.EqualTo("Creatinine"));
        Assert.That(current.AbnormalFlag, Is.EqualTo(LabAbnormalFlag.Normal));
    }

    // ── 8 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task PatientLabSummaryGrain_AbnormalResults_FiltersCorrectly()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientLabSummary summaryGrain = GetSummaryGrain(patientId);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await summaryGrain.RecordNewResult(new LabResultEvent
        {
            PatientIcn = patientId, ResultId = "R1", LoincCode = "2160-0",
            TestName = "Creatinine", Value = "1.0", Units = "mg/dL",
            AbnormalFlag = LabAbnormalFlag.Normal, ResultDate = now, FacilityCode = "688"
        });
        await summaryGrain.RecordNewResult(new LabResultEvent
        {
            PatientIcn = patientId, ResultId = "R2", LoincCode = "1742-6",
            TestName = "ALT", Value = "85", Units = "IU/L",
            AbnormalFlag = LabAbnormalFlag.High, ResultDate = now, FacilityCode = "688"
        });
        await summaryGrain.RecordNewResult(new LabResultEvent
        {
            PatientIcn = patientId, ResultId = "R3", LoincCode = "2823-3",
            TestName = "Potassium", Value = "6.5", Units = "mEq/L",
            AbnormalFlag = LabAbnormalFlag.CriticalHigh, ResultDate = now, FacilityCode = "688"
        });

        // Act
        IReadOnlyList<LabTestSummaryEntry> abnormal = await summaryGrain.GetAbnormalResults();

        // Assert — Normal creatinine excluded; ALT (High) and K (CriticalHigh) included
        Assert.That(abnormal, Has.Count.EqualTo(2));
        Assert.That(abnormal.Select(a => a.LoincCode), Is.EquivalentTo(new[] { "1742-6", "2823-3" }));
    }

    // ── 9 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task PatientLabBatchGrain_Partitioning_H1VsH2()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        var janDate = new DateTimeOffset(2025, 3, 15, 0, 0, 0, TimeSpan.Zero);
        var sepDate = new DateTimeOffset(2025, 9, 10, 0, 0, 0, TimeSpan.Zero);

        string h1Key = LabBatchKeyHelper.GetBatchKey(patientId, janDate);
        string h2Key = LabBatchKeyHelper.GetBatchKey(patientId, sepDate);
        IPatientLabBatch h1Batch = GetBatchGrain(h1Key);
        IPatientLabBatch h2Batch = GetBatchGrain(h2Key);

        // Act
        await h1Batch.AddResult(new LabResultDetail
        {
            ResultId = "H1-R1", LoincCode = "2160-0", TestName = "Creatinine",
            Value = "1.0", Units = "mg/dL", AbnormalFlag = LabAbnormalFlag.Normal,
            ResultDate = janDate, FacilityCode = "688", Specimen = "Serum"
        });
        await h2Batch.AddResult(new LabResultDetail
        {
            ResultId = "H2-R1", LoincCode = "2160-0", TestName = "Creatinine",
            Value = "1.2", Units = "mg/dL", AbnormalFlag = LabAbnormalFlag.Normal,
            ResultDate = sepDate, FacilityCode = "688", Specimen = "Serum"
        });

        // Assert — results land in separate partitions
        Assert.That(h1Key, Does.Contain("2025-H1"));
        Assert.That(h2Key, Does.Contain("2025-H2"));
        List<LabResultDetail> h1Results = (await h1Batch.GetAllResults()).ToList();
        List<LabResultDetail> h2Results = (await h2Batch.GetAllResults()).ToList();
        Assert.That(h1Results, Has.Count.EqualTo(1));
        Assert.That(h2Results, Has.Count.EqualTo(1));
        Assert.That(h1Results[0].ResultId, Is.EqualTo("H1-R1"));
        Assert.That(h2Results[0].ResultId, Is.EqualTo("H2-R1"));
    }

    // ── 10 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task PatientLabIndexGrain_RebuildIndex_FindsAllTypes()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string batchKey = LabBatchKeyHelper.GetBatchKey(patientId, now);
        IPatientLabBatch batch = GetBatchGrain(batchKey);

        await batch.AddResult(new LabResultDetail
        {
            ResultId = "R1", LoincCode = "2160-0", TestName = "Creatinine",
            Value = "1.0", Units = "mg/dL", AbnormalFlag = LabAbnormalFlag.Normal,
            ResultDate = now, FacilityCode = "688", Specimen = "Serum"
        });
        await batch.AddResult(new LabResultDetail
        {
            ResultId = "R2", LoincCode = "718-7", TestName = "HGB",
            Value = "14.0", Units = "g/dL", AbnormalFlag = LabAbnormalFlag.Normal,
            ResultDate = now.AddHours(-1), FacilityCode = "688", Specimen = "Blood"
        });
        await batch.AddResult(new LabResultDetail
        {
            ResultId = "R3", LoincCode = "2823-3", TestName = "Potassium",
            Value = "4.2", Units = "mEq/L", AbnormalFlag = LabAbnormalFlag.Normal,
            ResultDate = now.AddHours(-2), FacilityCode = "688", Specimen = "Serum"
        });

        // Act
        IPatientLabIndex index = GetIndexGrain(patientId);
        await index.RebuildIndex();

        // Assert
        IReadOnlyCollection<string> types = await index.GetDistinctTestTypes();
        Assert.That(types, Has.Count.EqualTo(3));
        Assert.That(types, Contains.Item("2160-0"));
        Assert.That(types, Contains.Item("718-7"));
        Assert.That(types, Contains.Item("2823-3"));
    }

    // ── 11 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task LabResultIngestionGrain_IngestResult_WritesBatchAndSummary()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        ILabResultIngestion ingestion = _cluster.GrainFactory.GetGrain<ILabResultIngestion>(patientId);

        var result = new LabResultDetail
        {
            ResultId = $"R-{Guid.NewGuid()}",
            LoincCode = "3094-0",
            TestName = "BUN",
            Value = "18",
            Units = "mg/dL",
            ReferenceRange = "7-20",
            AbnormalFlag = LabAbnormalFlag.Normal,
            ResultDate = DateTimeOffset.UtcNow,
            FacilityCode = "688",
            FacilityName = "Washington DC VAMC",
            Specimen = "Serum"
        };

        // Act
        await ingestion.IngestResult(result);

        // Assert — summary grain updated
        IPatientLabSummary summary = GetSummaryGrain(patientId);
        LabTestSummaryEntry? current = await summary.GetCurrentResult("3094-0");
        Assert.That(current, Is.Not.Null);
        Assert.That(current!.Value, Is.EqualTo("18"));

        // Assert — batch grain updated
        string batchKey = LabBatchKeyHelper.GetBatchKey(patientId, result.ResultDate);
        IPatientLabBatch batch = GetBatchGrain(batchKey);
        List<LabResultDetail> batchResults = (await batch.GetAllResults()).ToList();
        Assert.That(batchResults, Has.Count.EqualTo(1));
        Assert.That(batchResults[0].LoincCode, Is.EqualTo("3094-0"));
    }

    // ── 12 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task WorkflowGrain_OrderLabTest_CreatesGrainAndReturnsId()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        // Act
        string labTestId = await wf.OrderLabTestAsync(
            "LR-6690-2", "WBC", "6690-2",
            null, "PROV-001", "Dr. Smith", "Blood", "HEMATOLOGY");

        // Assert
        Assert.That(labTestId, Is.Not.Null.And.Not.Empty);
        LabTestState state = await wf.GetLabTestAsync(labTestId);
        Assert.That(state.TestName, Is.EqualTo("WBC"));
        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.Status, Is.EqualTo("Ordered"));
    }

    // ── Lab/order unification ────────────────────────────────────────────────
    [Test]
    public async Task WorkflowGrain_PlaceLabOrder_AppearsAsUnifiedOrder_AndCompletesOnVerify()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        // Act — place a lab order through the interactive CPOE path.
        string labTestId = await wf.PlaceLabOrderAsync(
            "LR-CBC", "CBC", "58410-2", "PROV-001", "Dr. Smith", "Blood", "HEMATOLOGY");

        // Assert — the lab now shows in the unified order list (filter 2 = Current) as a
        // Laboratory order linked to its lab test. (This was the bug: lab orders placed via
        // the Labs page never reached the order index, so the Orders page showed nothing.)
        List<OrderSummary> current = await wf.GetOrdersByFilterAsync(2);
        Assert.That(current, Has.Count.EqualTo(1));
        Assert.That(current[0].OrderType, Is.EqualTo("Laboratory"));
        Assert.That(current[0].OrderText, Is.EqualTo("CBC"));

        LabTestState lab = await wf.GetLabTestAsync(labTestId);
        Assert.That(lab.OrderId, Is.EqualTo(current[0].OrderId), "lab test links back to its order");

        // Act — record then verify the result.
        await wf.RecordLabResultAsync(labTestId, DateTime.UtcNow, "5.2", "K/uL", "4.5", "11.0", null);
        await wf.VerifyLabResultAsync(labTestId, "PROV-002", "Dr. Verifier", DateTime.UtcNow);

        // Assert — verifying completes the order: it leaves the active view and lands under
        // Completed/Expired (filter 4).
        List<OrderSummary> afterVerify = await wf.GetOrdersByFilterAsync(2);
        Assert.That(afterVerify, Is.Empty, "verified lab order should leave the active (Current) view");
        List<OrderSummary> completed = await wf.GetOrdersByFilterAsync(4);
        Assert.That(completed.Any(o => o.OrderId == current[0].OrderId), Is.True,
            "completed lab order should appear under Completed/Expired");
    }

    [Test]
    public async Task WorkflowGrain_OrderLabTest_DoesNotCreateCpoeOrder()
    {
        // The bulk/import path (OrderLabTestAsync) records a lab in isolation and must NOT spawn
        // a CPOE order — otherwise importing historical results would flood the order list.
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        await wf.OrderLabTestAsync("LR-718-7", "HGB", "718-7", null, null, null, "Blood", "HEMATOLOGY");

        List<OrderSummary> allOrders = await wf.GetOrdersByFilterAsync(1); // All
        Assert.That(allOrders, Is.Empty);
    }

    // ── 13 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task WorkflowGrain_GetLabResults_IncludesOrderedTests()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        string id1 = await wf.OrderLabTestAsync("LR-2160-0", "Creatinine", "2160-0",
            null, null, null, "Serum", "CHEMISTRY");
        string id2 = await wf.OrderLabTestAsync("LR-718-7", "HGB", "718-7",
            null, null, null, "Blood", "HEMATOLOGY");

        // Act
        List<LabResultSummary> results = await wf.GetLabResultsAsync();

        // Assert
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.Select(r => r.TestName), Is.EquivalentTo(new[] { "Creatinine", "HGB" }));
    }

    // ── 14 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task WorkflowGrain_IngestLabResult_UpdatesSummary()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        // Act
        await wf.IngestLabResultAsync(
            $"R-{Guid.NewGuid()}", "1920-8", "AST", "45", "IU/L",
            "0-40", LabAbnormalFlag.High, DateTimeOffset.UtcNow,
            "688", "Washington DC VAMC", "Serum", "LFT");

        // Assert
        List<LabTestSummaryEntry> summary = await wf.GetLabSummaryAsync();
        Assert.That(summary, Has.Count.GreaterThanOrEqualTo(1));
        LabTestSummaryEntry? ast = summary.FirstOrDefault(s => s.LoincCode == "1920-8");
        Assert.That(ast, Is.Not.Null);
        Assert.That(ast!.Value, Is.EqualTo("45"));
        Assert.That(ast.AbnormalFlag, Is.EqualTo(LabAbnormalFlag.High));
    }

    // ── 15 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task WorkflowGrain_GetAbnormalResults_FiltersCorrectly()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        await wf.IngestLabResultAsync(
            $"R-{Guid.NewGuid()}", "2160-0", "Creatinine", "1.0", "mg/dL",
            "0.7-1.3", LabAbnormalFlag.Normal, DateTimeOffset.UtcNow,
            "688", null, null, null);
        await wf.IngestLabResultAsync(
            $"R-{Guid.NewGuid()}", "1742-6", "ALT", "78", "IU/L",
            "0-40", LabAbnormalFlag.High, DateTimeOffset.UtcNow,
            "688", null, null, null);
        await wf.IngestLabResultAsync(
            $"R-{Guid.NewGuid()}", "2823-3", "Potassium", "6.5", "mEq/L",
            "3.5-5.0", LabAbnormalFlag.CriticalHigh, DateTimeOffset.UtcNow,
            "688", null, null, null);

        // Act
        List<LabTestSummaryEntry> abnormal = await wf.GetAbnormalLabResultsAsync();

        // Assert — creatinine excluded, ALT and K included
        Assert.That(abnormal, Has.Count.EqualTo(2));
        Assert.That(abnormal.Select(a => a.LoincCode), Is.EquivalentTo(new[] { "1742-6", "2823-3" }));
    }

    // ── 16 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task LabWorkflow_EndToEnd_OrderCollectRecordVerify()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        // Act — Order
        string labTestId = await wf.OrderLabTestAsync(
            "LR-2160-0", "Creatinine", "2160-0",
            null, "PROV-001", "Dr. Smith", "Serum", "CHEMISTRY");

        LabTestState afterOrder = await wf.GetLabTestAsync(labTestId);
        Assert.That(afterOrder.Status, Is.EqualTo("Ordered"));

        // Act — Collect
        DateTime collectTime = DateTime.UtcNow.AddHours(-3);
        await wf.CollectSpecimenAsync(labTestId, collectTime, "MARBLED TOP", "Chemistry Lab");

        LabTestState afterCollect = await wf.GetLabTestAsync(labTestId);
        Assert.That(afterCollect.Status, Is.EqualTo("Collected"));
        Assert.That(afterCollect.CollectionDateTime, Is.EqualTo(collectTime).Within(TimeSpan.FromSeconds(1)));

        // Act — Record result
        DateTime resultTime = DateTime.UtcNow.AddHours(-1);
        await wf.RecordLabResultAsync(labTestId, resultTime, "1.4", "mg/dL", "0.7", "1.3", "H");

        LabTestState afterResult = await wf.GetLabTestAsync(labTestId);
        Assert.That(afterResult.Status, Is.EqualTo("Pending")); // Verify step promotes to Completed
        Assert.That(afterResult.ResultValue, Is.EqualTo("1.4"));
        Assert.That(afterResult.AbnormalFlag, Is.EqualTo("H"));

        // Act — Verify
        DateTime verifyTime = DateTime.UtcNow;
        await wf.VerifyLabResultAsync(labTestId, "PATH-001", "Dr. Pathologist", verifyTime);

        LabTestState afterVerify = await wf.GetLabTestAsync(labTestId);
        Assert.That(afterVerify.VerifyingProviderId, Is.EqualTo("PATH-001"));
        Assert.That(afterVerify.VerifyingProviderName, Is.EqualTo("Dr. Pathologist"));

        // Assert — result appears in GetLabResultsAsync
        List<LabResultSummary> allResults = await wf.GetLabResultsAsync();
        LabResultSummary? found = allResults.FirstOrDefault(r => r.LabTestId == labTestId);
        Assert.That(found, Is.Not.Null);
        Assert.That(found!.ResultValue, Is.EqualTo("1.4"));
        Assert.That(found.Flag, Is.EqualTo("H"));
    }
}
