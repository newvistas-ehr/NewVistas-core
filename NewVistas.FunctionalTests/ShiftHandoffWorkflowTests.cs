// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

[TestFixture]
public class ShiftHandoffWorkflowTests
{
    private TestCluster _cluster = null!;
    [OneTimeSetUp] public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private INursingShiftHandoffGrain NewHandoff()
        => _cluster.GrainFactory.GetGrain<INursingShiftHandoffGrain>($"NURS-HANDOFF:{Guid.NewGuid()}");
    private IShiftHandoffIndexGrain GetIndex(string pid)
        => _cluster.GrainFactory.GetGrain<IShiftHandoffIndexGrain>($"NURS-HANDOFF-IDX:{pid}");
    private IPatientWorkflowGrain WF(string pid)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid);

    private SbarPatientSummary TestSbar() => new()
    {
        Situation = "72M, POD1 R TKA. Stable vitals. Pain controlled at 4/10.",
        Background = "PMH: HTN, DM2, OA bilateral knees. Surgery yesterday without complications.",
        Assessment = "Ambulated 20ft with PT. Wound dressing clean/dry/intact. Alert and oriented x4.",
        Recommendation = "Continue PCA. PT eval in AM. May advance diet. Monitor wound q shift."
    };

    [Test]
    public async Task CreateHandoff_SetsDraftStatus()
    {
        INursingShiftHandoffGrain grain = NewHandoff();
        await grain.CreateAsync("P1", NursingShift.Day, DateTime.UtcNow.Date, "RN-OUT", "Nurse Outgoing",
            "WARD-1", "Med-Surg 3A", "301-A", TestSbar(), null, null, null);
        NursingShiftHandoffState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(ShiftHandoffStatus.Draft));
        Assert.That(state.Sbar, Is.Not.Null);
    }

    [Test]
    public async Task CreateHandoff_StoresSbarAndLocation()
    {
        INursingShiftHandoffGrain grain = NewHandoff();
        await grain.CreateAsync("P2", NursingShift.Evening, DateTime.UtcNow.Date, "RN-1", "Nurse",
            null, "ICU-1", "ICU-5", TestSbar(), null, new List<string> { "Fall risk", "Contact precautions" }, "Monitor closely");
        NursingShiftHandoffState state = await grain.GetAsync();
        Assert.That(state.Sbar!.Situation, Does.Contain("TKA"));
        Assert.That(state.BedNumber, Is.EqualTo("ICU-5"));
        Assert.That(state.SafetyConcerns, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task CompleteHandoff_SetsCompletedStatus()
    {
        INursingShiftHandoffGrain grain = NewHandoff();
        await grain.CreateAsync("P3", NursingShift.Night, DateTime.UtcNow.Date, "RN-1", "Nurse", null, null, null, TestSbar(), null, null, null);
        await grain.CompleteAsync();
        NursingShiftHandoffState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(ShiftHandoffStatus.Completed));
        Assert.That(state.CompletedDate, Is.Not.Null);
    }

    [Test]
    public async Task AcknowledgeHandoff_SetsAcknowledgedWithIncoming()
    {
        INursingShiftHandoffGrain grain = NewHandoff();
        await grain.CreateAsync("P4", NursingShift.Day, DateTime.UtcNow.Date, "RN-OUT", "Outgoing", null, null, null, TestSbar(), null, null, null);
        await grain.CompleteAsync();
        await grain.AcknowledgeAsync("RN-IN", "Incoming Nurse");
        NursingShiftHandoffState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(ShiftHandoffStatus.Acknowledged));
        Assert.That(state.IncomingNurseName, Is.EqualTo("Incoming Nurse"));
        Assert.That(state.AcknowledgedDate, Is.Not.Null);
    }

    [Test]
    public async Task Index_AddOrUpdate_AppearsInGetAll()
    {
        string pid = $"PAT-HO-IDX-{Guid.NewGuid()}";
        IShiftHandoffIndexGrain idx = GetIndex(pid);
        string id = Guid.NewGuid().ToString();
        await idx.AddOrUpdateAsync(new ShiftHandoffIndexEntry { HandoffId = id, Shift = NursingShift.Day, ShiftDate = DateTime.UtcNow.Date, OutgoingNurseName = "Nurse", Status = ShiftHandoffStatus.Draft });
        List<ShiftHandoffIndexEntry> all = await idx.GetAllAsync();
        Assert.That(all.Any(e => e.HandoffId == id), Is.True);
    }

    [Test]
    public async Task Index_GetLatest_ReturnsNewest()
    {
        string pid = $"PAT-HO-LAT-{Guid.NewGuid()}";
        IShiftHandoffIndexGrain idx = GetIndex(pid);
        await idx.AddOrUpdateAsync(new ShiftHandoffIndexEntry { HandoffId = "OLD", Shift = NursingShift.Day, ShiftDate = DateTime.UtcNow.AddDays(-1).Date, OutgoingNurseName = "A", Status = ShiftHandoffStatus.Acknowledged });
        await idx.AddOrUpdateAsync(new ShiftHandoffIndexEntry { HandoffId = "NEW", Shift = NursingShift.Evening, ShiftDate = DateTime.UtcNow.Date, OutgoingNurseName = "B", Status = ShiftHandoffStatus.Draft });
        ShiftHandoffIndexEntry? latest = await idx.GetLatestAsync();
        Assert.That(latest, Is.Not.Null);
        Assert.That(latest!.HandoffId, Is.EqualTo("NEW"));
    }

    // ─── Workflow ────────────────────────────────────────────────────────────

    [Test]
    public async Task Workflow_CreateHandoff_ReturnsIdWithSnapshot()
    {
        string pid = $"PAT-HO-WF-{Guid.NewGuid()}";
        string id = await WF(pid).CreateShiftHandoffAsync(
            NursingShift.Day, DateTime.UtcNow.Date, "RN-1", "Nurse Smith",
            null, "WARD-MED-3A", "301-B", TestSbar(), null, null);
        Assert.That(id, Is.Not.Null.And.Not.Empty);
        NursingShiftHandoffState state = await WF(pid).GetShiftHandoffAsync(id);
        Assert.That(state.Sbar, Is.Not.Null);
        Assert.That(state.ClinicalSnapshot, Is.Not.Null);
    }

    [Test]
    public async Task Workflow_CreateHandoff_UpdatesIndex()
    {
        string pid = $"PAT-HO-WF2-{Guid.NewGuid()}";
        await WF(pid).CreateShiftHandoffAsync(NursingShift.Evening, DateTime.UtcNow.Date, "RN-1", "Nurse", null, null, null, TestSbar(), null, null);
        List<ShiftHandoffIndexEntry> list = await WF(pid).GetShiftHandoffsAsync();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Shift, Is.EqualTo(NursingShift.Evening));
    }

    [Test]
    public async Task Workflow_CompleteHandoff_UpdatesIndexStatus()
    {
        string pid = $"PAT-HO-WF3-{Guid.NewGuid()}";
        string id = await WF(pid).CreateShiftHandoffAsync(NursingShift.Night, DateTime.UtcNow.Date, "RN-1", "Nurse", null, null, null, TestSbar(), null, null);
        await WF(pid).CompleteShiftHandoffAsync(id);
        List<ShiftHandoffIndexEntry> list = await WF(pid).GetShiftHandoffsAsync();
        Assert.That(list[0].Status, Is.EqualTo(ShiftHandoffStatus.Completed));
    }

    [Test]
    public async Task Workflow_AcknowledgeHandoff_UpdatesIndexWithIncoming()
    {
        string pid = $"PAT-HO-WF4-{Guid.NewGuid()}";
        string id = await WF(pid).CreateShiftHandoffAsync(NursingShift.Day, DateTime.UtcNow.Date, "RN-OUT", "Outgoing", null, null, null, TestSbar(), null, null);
        await WF(pid).CompleteShiftHandoffAsync(id);
        await WF(pid).AcknowledgeShiftHandoffAsync(id, "RN-IN", "Incoming");
        List<ShiftHandoffIndexEntry> list = await WF(pid).GetShiftHandoffsAsync();
        Assert.That(list[0].Status, Is.EqualTo(ShiftHandoffStatus.Acknowledged));
        Assert.That(list[0].IncomingNurseName, Is.EqualTo("Incoming"));
    }

    [Test]
    public async Task Workflow_MultipleHandoffs_AllTracked()
    {
        string pid = $"PAT-HO-WF5-{Guid.NewGuid()}";
        await WF(pid).CreateShiftHandoffAsync(NursingShift.Day, DateTime.UtcNow.Date, "RN-1", "Day Nurse", null, null, null, TestSbar(), null, null);
        await WF(pid).CreateShiftHandoffAsync(NursingShift.Evening, DateTime.UtcNow.Date, "RN-2", "Eve Nurse", null, null, null, TestSbar(), null, null);
        await WF(pid).CreateShiftHandoffAsync(NursingShift.Night, DateTime.UtcNow.Date, "RN-3", "Night Nurse", null, null, null, TestSbar(), null, null);
        List<ShiftHandoffIndexEntry> list = await WF(pid).GetShiftHandoffsAsync();
        Assert.That(list, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task Workflow_HandoffWithSafetyConcerns()
    {
        string pid = $"PAT-HO-WF6-{Guid.NewGuid()}";
        string id = await WF(pid).CreateShiftHandoffAsync(NursingShift.Day, DateTime.UtcNow.Date, "RN-1", "Nurse",
            null, null, null, TestSbar(), new List<string> { "HIGH FALL RISK", "CONTACT PRECAUTIONS", "NPO after midnight" }, null);
        NursingShiftHandoffState state = await WF(pid).GetShiftHandoffAsync(id);
        Assert.That(state.SafetyConcerns, Has.Count.EqualTo(3));
        Assert.That(state.SafetyConcerns, Does.Contain("HIGH FALL RISK"));
    }

    [Test]
    public async Task Workflow_ClinicalSnapshot_PopulatesAcuity()
    {
        string pid = $"PAT-HO-WF7-{Guid.NewGuid()}";
        await WF(pid).RecordNursingAcuityAsync(AcuityLevel.AboveAverageCare, 3, "RN-1", "Nurse", "DAY", null);
        string id = await WF(pid).CreateShiftHandoffAsync(NursingShift.Day, DateTime.UtcNow.Date, "RN-1", "Nurse", null, null, null, TestSbar(), null, null);
        NursingShiftHandoffState state = await WF(pid).GetShiftHandoffAsync(id);
        Assert.That(state.ClinicalSnapshot, Is.Not.Null);
        Assert.That(state.ClinicalSnapshot!.AcuityLevel, Is.EqualTo("AboveAverageCare"));
    }
}
