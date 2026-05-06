// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Lab Tech workflows — Worklist, Accessioning, QC, Specimen Rejection, Delta Checks.
/// VistA LR package — LRACE.m, LRVER.m.
/// </summary>
[TestFixture]
public class LabTechWorkflowTests
{
    private TestCluster _cluster = null!;
    [OneTimeSetUp] public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private ILabAccessionGrain NewAccession()
        => _cluster.GrainFactory.GetGrain<ILabAccessionGrain>($"LAB-ACCESSION:{DateTime.UtcNow:yyyyMMdd}-CHEM-{Guid.NewGuid().ToString("N")[..6].ToUpper()}");
    private ILabQcGrain NewQc(string inst, string loinc)
        => _cluster.GrainFactory.GetGrain<ILabQcGrain>($"LAB-QC:{inst}:{loinc}");
    private ILabWorklistGrain NewWorklist()
        => _cluster.GrainFactory.GetGrain<ILabWorklistGrain>($"LAB-WORKLIST:{Guid.NewGuid()}");
    private IPatientWorkflowGrain WF(string pid)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid);

    // ─── Accessioning ────────────────────────────────────────────────────────

    [Test]
    public async Task Accession_Create_SetsAccessionedStatus()
    {
        ILabAccessionGrain acc = NewAccession();
        await acc.CreateAsync("PAT-LAB-001", new List<string> { "LT-001", "LT-002" },
            "Blood", "LAVENDER", DateTime.UtcNow.AddMinutes(-30), "HEMATOLOGY", "TECH-1", "Tech Smith", null);
        LabAccessionState state = await acc.GetAsync();
        Assert.That(state.Status, Is.EqualTo(SpecimenStatus.Accessioned));
        Assert.That(state.LabTestIds, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Accession_MarkInProcess_UpdatesStatus()
    {
        ILabAccessionGrain acc = NewAccession();
        await acc.CreateAsync("PAT-LAB-002", new List<string> { "LT-003" }, "Urine", null, DateTime.UtcNow, "CHEMISTRY", null, null, null);
        await acc.MarkInProcessAsync();
        Assert.That((await acc.GetAsync()).Status, Is.EqualTo(SpecimenStatus.InProcess));
    }

    [Test]
    public async Task Accession_MarkCompleted_UpdatesStatus()
    {
        ILabAccessionGrain acc = NewAccession();
        await acc.CreateAsync("PAT-LAB-003", new List<string> { "LT-004" }, "Blood", "GRAY", DateTime.UtcNow, "CHEMISTRY", null, null, null);
        await acc.MarkInProcessAsync();
        await acc.MarkCompletedAsync();
        Assert.That((await acc.GetAsync()).Status, Is.EqualTo(SpecimenStatus.Completed));
    }

    // ─── Specimen Rejection ──────────────────────────────────────────────────

    [Test]
    public async Task Reject_Hemolyzed_SetsRejectedStatus()
    {
        ILabAccessionGrain acc = NewAccession();
        await acc.CreateAsync("PAT-LAB-004", new List<string> { "LT-005" }, "Blood", "LAVENDER", DateTime.UtcNow, "HEMATOLOGY", null, null, null);
        await acc.RejectAsync(SpecimenRejectReason.Hemolyzed, "Grossly hemolyzed specimen", "TECH-1", true);
        LabAccessionState state = await acc.GetAsync();
        Assert.That(state.Status, Is.EqualTo(SpecimenStatus.Rejected));
        Assert.That(state.IsRejected, Is.True);
        Assert.That(state.RejectReason, Is.EqualTo(SpecimenRejectReason.Hemolyzed));
        Assert.That(state.RecollectOrdered, Is.True);
    }

    [Test]
    public async Task Reject_QNS_SetsReasonAndNotes()
    {
        ILabAccessionGrain acc = NewAccession();
        await acc.CreateAsync("PAT-LAB-005", new List<string> { "LT-006" }, "CSF", null, DateTime.UtcNow, "CHEMISTRY", null, null, null);
        await acc.RejectAsync(SpecimenRejectReason.QuantityNotSufficient, "Only 0.5mL received, need 2mL", "TECH-2", false);
        LabAccessionState state = await acc.GetAsync();
        Assert.That(state.RejectReason, Is.EqualTo(SpecimenRejectReason.QuantityNotSufficient));
        Assert.That(state.RejectNotes, Does.Contain("0.5mL"));
        Assert.That(state.RecollectOrdered, Is.False);
    }

    // ─── QC / Quality Control ────────────────────────────────────────────────

    [Test]
    public async Task Qc_PassingResult_SetsPassStatus()
    {
        ILabQcGrain qc = NewQc("INST-001", "718-7");
        LabQcResult result = await qc.RecordQcRunAsync("Level 1 Normal", "LOT-2026-A", 14.0m, 14.2m, 0.5m, "TECH-1", "Tech", null);
        Assert.That(result.Status, Is.EqualTo(QcResultStatus.Pass));
        Assert.That(result.WestgardViolation, Is.Null);
    }

    [Test]
    public async Task Qc_1_2s_Warning_SetsWarningStatus()
    {
        ILabQcGrain qc = NewQc("INST-002", "718-7");
        // z-score = |14.0 - 13.0| / 0.5 = 2.0 → 1-2s warning
        LabQcResult result = await qc.RecordQcRunAsync("Level 1", "LOT-001", 14.0m, 13.0m, 0.5m, "TECH-1", "Tech", null);
        Assert.That(result.Status, Is.EqualTo(QcResultStatus.Warning));
        Assert.That(result.WestgardViolation, Is.EqualTo("1-2s"));
    }

    [Test]
    public async Task Qc_1_3s_Fail_BlocksTesting()
    {
        ILabQcGrain qc = NewQc("INST-003", "718-7");
        // z-score = |16.0 - 14.0| / 0.5 = 4.0 → 1-3s fail
        LabQcResult result = await qc.RecordQcRunAsync("Level 1", "LOT-001", 16.0m, 14.0m, 0.5m, "TECH-1", "Tech", null);
        Assert.That(result.Status, Is.EqualTo(QcResultStatus.Fail));
        Assert.That(result.WestgardViolation, Is.EqualTo("1-3s"));
        bool allowed = await qc.IsTestingAllowedAsync();
        Assert.That(allowed, Is.False);
    }

    [Test]
    public async Task Qc_UnblockTesting_AllowsTestingAgain()
    {
        ILabQcGrain qc = NewQc("INST-004", "718-7");
        await qc.RecordQcRunAsync("Level 1", "LOT-001", 16.0m, 14.0m, 0.5m, "TECH-1", "Tech", null); // fail
        await qc.UnblockTestingAsync();
        bool allowed = await qc.IsTestingAllowedAsync();
        Assert.That(allowed, Is.True);
    }

    [Test]
    public async Task Qc_ConsecutivePassCount_Increments()
    {
        ILabQcGrain qc = NewQc("INST-005", "4544-3");
        await qc.RecordQcRunAsync("Level 1", "LOT-001", 40.0m, 40.1m, 1.0m, "TECH-1", "Tech", null);
        await qc.RecordQcRunAsync("Level 2", "LOT-001", 60.0m, 60.2m, 1.5m, "TECH-1", "Tech", null);
        LabQcState state = await qc.GetAsync();
        Assert.That(state.ConsecutivePassCount, Is.EqualTo(2));
        Assert.That(state.IsCurrentlyPassing, Is.True);
    }

    [Test]
    public async Task Qc_GetRecentResults_ReturnsHistory()
    {
        ILabQcGrain qc = NewQc("INST-006", "2345-7");
        await qc.RecordQcRunAsync("L1", "LOT", 5.0m, 5.0m, 0.2m, "T1", "Tech", null);
        await qc.RecordQcRunAsync("L2", "LOT", 10.0m, 10.0m, 0.5m, "T1", "Tech", null);
        List<LabQcResult> recent = await qc.GetRecentResultsAsync(5);
        Assert.That(recent, Has.Count.EqualTo(2));
    }

    // ─── Lab Worklist ────────────────────────────────────────────────────────

    [Test]
    public async Task Worklist_AddItem_AppearsInList()
    {
        ILabWorklistGrain wl = NewWorklist();
        await wl.AddItemAsync(new LabWorklistItem { LabTestId = "LT-W1", PatientId = "P1", TestName = "CBC", Status = "Ordered", WorklistCategory = LabWorklistCategory.PendingCollection, OrderedDate = DateTime.UtcNow });
        LabWorklistState state = await wl.GetAsync();
        Assert.That(state.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Worklist_GetByCategory_FiltersCorrectly()
    {
        ILabWorklistGrain wl = NewWorklist();
        await wl.AddItemAsync(new LabWorklistItem { LabTestId = "LT-C1", PatientId = "P1", TestName = "CBC", WorklistCategory = LabWorklistCategory.PendingCollection, OrderedDate = DateTime.UtcNow });
        await wl.AddItemAsync(new LabWorklistItem { LabTestId = "LT-V1", PatientId = "P2", TestName = "BMP", WorklistCategory = LabWorklistCategory.PendingVerification, OrderedDate = DateTime.UtcNow });
        List<LabWorklistItem> pending = await wl.GetByCategoryAsync(LabWorklistCategory.PendingCollection);
        Assert.That(pending, Has.Count.EqualTo(1));
        Assert.That(pending[0].TestName, Is.EqualTo("CBC"));
    }

    [Test]
    public async Task Worklist_GetCritical_ReturnsCriticalOnly()
    {
        ILabWorklistGrain wl = NewWorklist();
        await wl.AddItemAsync(new LabWorklistItem { LabTestId = "LT-N1", TestName = "Normal", IsCritical = false, OrderedDate = DateTime.UtcNow });
        await wl.AddItemAsync(new LabWorklistItem { LabTestId = "LT-C1", TestName = "Critical K+", IsCritical = true, OrderedDate = DateTime.UtcNow });
        List<LabWorklistItem> critical = await wl.GetCriticalAsync();
        Assert.That(critical, Has.Count.EqualTo(1));
        Assert.That(critical[0].TestName, Is.EqualTo("Critical K+"));
    }

    // ─── Workflow Integration ────────────────────────────────────────────────

    [Test]
    public async Task Workflow_AccessionSpecimen_ReturnsAccessionNumber()
    {
        string pid = $"PAT-LABWF-{Guid.NewGuid()}";
        string labTestId = await WF(pid).OrderLabTestAsync("60-001", "CBC", "718-7", null, null, null, "Blood", "HEMATOLOGY");
        string accNum = await WF(pid).AccessionSpecimenAsync(
            new List<string> { labTestId }, "Blood", "LAVENDER", DateTime.UtcNow, "HEMATOLOGY", "TECH-1", "Tech Smith", null);
        Assert.That(accNum, Is.Not.Null.And.Not.Empty);
        LabAccessionState state = await WF(pid).GetAccessionAsync(accNum);
        Assert.That(state.Status, Is.EqualTo(SpecimenStatus.Accessioned));
    }

    [Test]
    public async Task Workflow_RejectSpecimen_UpdatesIndex()
    {
        string pid = $"PAT-LABWF2-{Guid.NewGuid()}";
        string ltId = await WF(pid).OrderLabTestAsync("60-002", "BMP", "2345-7", null, null, null, "Blood", "CHEMISTRY");
        string accNum = await WF(pid).AccessionSpecimenAsync(
            new List<string> { ltId }, "Blood", "GRAY", DateTime.UtcNow, "CHEMISTRY", "TECH-1", "Tech", null);
        await WF(pid).RejectSpecimenAsync(accNum, SpecimenRejectReason.Clotted, "Clotted specimen", "TECH-1", true);
        List<LabAccessionIndexEntry> accs = await WF(pid).GetAccessionsAsync();
        Assert.That(accs[0].IsRejected, Is.True);
    }

    [Test]
    public async Task Workflow_RecordQcRun_ReturnsResult()
    {
        string pid = $"PAT-LABWF3-{Guid.NewGuid()}";
        LabQcResult result = await WF(pid).RecordLabQcRunAsync(
            "BECKMAN-AU5800", "718-7", "WBC", "Level 1 Normal", "LOT-2026",
            7.5m, 7.4m, 0.3m, "TECH-1", "Tech", null);
        Assert.That(result.Status, Is.EqualTo(QcResultStatus.Pass));
    }

    [Test]
    public async Task Workflow_IsLabTestingAllowed_ReturnsTrueWhenPassing()
    {
        string pid = $"PAT-LABWF4-{Guid.NewGuid()}";
        await WF(pid).RecordLabQcRunAsync("INST-QC-TEST", "718-7", "WBC", "L1", "LOT", 14.0m, 14.0m, 0.5m, "T1", "Tech", null);
        bool allowed = await WF(pid).IsLabTestingAllowedAsync("INST-QC-TEST", "718-7");
        Assert.That(allowed, Is.True);
    }

    [Test]
    public async Task Workflow_DeltaCheck_NoPrevious_Passes()
    {
        string pid = $"PAT-LABWF5-{Guid.NewGuid()}";
        DeltaCheckResult result = await WF(pid).PerformDeltaCheckAsync("LT-DC-001", "718-7", "WBC", 7.5m, DateTime.UtcNow, null);
        Assert.That(result.Passed, Is.True);
        Assert.That(result.PreviousValue, Is.Null);
    }

    [Test]
    public async Task Workflow_RefreshWorklist_ReturnsState()
    {
        string pid = $"PAT-LABWF6-{Guid.NewGuid()}";
        await WF(pid).OrderLabTestAsync("60-010", "LFT", "1742-6", null, null, null, "Blood", "CHEMISTRY");
        LabWorklistState wl = await WF(pid).RefreshLabWorklistAsync("LAB-MAIN");
        Assert.That(wl, Is.Not.Null);
    }
}
