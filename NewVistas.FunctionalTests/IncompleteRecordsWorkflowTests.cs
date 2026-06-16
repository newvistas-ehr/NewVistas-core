// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NUnit.Framework;
using Orleans.Hosting;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

[TestFixture]
public class IncompleteRecordsWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task DeficiencyGrain_CanCreate()
    {
        string defId = $"DGPT-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IIncompleteRecordGrain>($"DGPT:{defId}");
        await grain.CreateDeficiencyAsync("PAT-001", "DOE,JOHN", "PROV-001", "DR. SMITH",
            "UNSIGNED_NOTE", "Progress note unsigned", "TIU-123", null, DateTime.UtcNow.AddDays(-5), 30);

        IncompleteRecordState state = await grain.GetDeficiencyAsync();
        Assert.That(state.Status, Is.EqualTo("OPEN"));
        Assert.That(state.DeficiencyType, Is.EqualTo("UNSIGNED_NOTE"));
        Assert.That(state.DaysOutstanding, Is.GreaterThanOrEqualTo(4));
    }

    [Test]
    public async Task DeficiencyGrain_BecomesDelinquentWhenOverdue()
    {
        string defId = $"DGPT-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IIncompleteRecordGrain>($"DGPT:{defId}");
        // Discharge 35 days ago, delinquent after 30
        await grain.CreateDeficiencyAsync("PAT-002", "SMITH,JANE", "PROV-001", "DR. SMITH",
            "MISSING_DISCHARGE_SUMMARY", "No discharge summary", null, "ADM-100",
            DateTime.UtcNow.AddDays(-35), 30);

        Assert.That((await grain.GetDeficiencyAsync()).Status, Is.EqualTo("DELINQUENT"));
    }

    [Test]
    public async Task DeficiencyGrain_NotDelinquentWhenWithinLimit()
    {
        string defId = $"DGPT-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IIncompleteRecordGrain>($"DGPT:{defId}");
        await grain.CreateDeficiencyAsync("PAT-003", "W,J", "PROV-001", "DR. S",
            "UNSIGNED_NOTE", "Note", null, null, DateTime.UtcNow.AddDays(-10), 30);

        Assert.That((await grain.GetDeficiencyAsync()).Status, Is.EqualTo("OPEN"));
    }

    [Test]
    public async Task DeficiencyGrain_CanComplete()
    {
        string defId = $"DGPT-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IIncompleteRecordGrain>($"DGPT:{defId}");
        await grain.CreateDeficiencyAsync("PAT-001", "DOE,JOHN", "PROV-001", "DR. SMITH",
            "UNSIGNED_NOTE", "Note", null, null, null, 30);
        await grain.CompleteAsync("PROV-001");

        IncompleteRecordState state = await grain.GetDeficiencyAsync();
        Assert.That(state.Status, Is.EqualTo("COMPLETED"));
        Assert.That(state.CompletedDate, Is.Not.Null);
    }

    [Test]
    public async Task DeficiencyGrain_CanWaive()
    {
        string defId = $"DGPT-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IIncompleteRecordGrain>($"DGPT:{defId}");
        await grain.CreateDeficiencyAsync("PAT-001", "DOE,JOHN", "PROV-001", "DR. SMITH",
            "MISSING_HP", "H&P", null, null, null, 30);
        await grain.WaiveAsync("Patient transferred to external facility", "COS-001");

        IncompleteRecordState state = await grain.GetDeficiencyAsync();
        Assert.That(state.Status, Is.EqualTo("WAIVED"));
        Assert.That(state.WaiverReason, Is.EqualTo("Patient transferred to external facility"));
    }

    [Test]
    public async Task DeficiencyGrain_UpdateDaysOutstanding()
    {
        string defId = $"DGPT-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IIncompleteRecordGrain>($"DGPT:{defId}");
        await grain.CreateDeficiencyAsync("PAT-001", "DOE,JOHN", "PROV-001", "DR. SMITH",
            "UNSIGNED_NOTE", "Note", null, null, DateTime.UtcNow.AddDays(-5), 30);
        await grain.UpdateDaysOutstandingAsync();

        Assert.That((await grain.GetDeficiencyAsync()).DaysOutstanding, Is.GreaterThanOrEqualTo(4));
    }

    // ─── Index ───────────────────────────────────────────────────────────────

    [Test]
    public async Task Index_CanTrackDeficiencies()
    {
        var index = _cluster.GrainFactory.GetGrain<IIncompleteRecordIndexGrain>($"DGPT-INDEX:PROV-{Guid.NewGuid():N}");
        await index.AddOrUpdateAsync(new IncompleteRecordEntry { DeficiencyId = "D1", Status = "OPEN", DeficiencyType = "UNSIGNED_NOTE", PatientName = "A" });
        await index.AddOrUpdateAsync(new IncompleteRecordEntry { DeficiencyId = "D2", Status = "DELINQUENT", DeficiencyType = "MISSING_HP", PatientName = "B", IsDelinquent = true });
        await index.AddOrUpdateAsync(new IncompleteRecordEntry { DeficiencyId = "D3", Status = "COMPLETED", DeficiencyType = "UNSIGNED_NOTE", PatientName = "C" });

        Assert.That(await index.GetAllDeficienciesAsync(), Has.Count.EqualTo(3));
        Assert.That(await index.GetOpenDeficienciesAsync(), Has.Count.EqualTo(2)); // OPEN + DELINQUENT
        Assert.That(await index.GetDelinquentDeficienciesAsync(), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Index_CanUpdate()
    {
        var index = _cluster.GrainFactory.GetGrain<IIncompleteRecordIndexGrain>($"DGPT-INDEX:PROV-{Guid.NewGuid():N}");
        await index.AddOrUpdateAsync(new IncompleteRecordEntry { DeficiencyId = "DU", Status = "OPEN", PatientName = "A" });
        await index.AddOrUpdateAsync(new IncompleteRecordEntry { DeficiencyId = "DU", Status = "COMPLETED", PatientName = "A" });

        List<IncompleteRecordEntry> all = await index.GetAllDeficienciesAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo("COMPLETED"));
    }

    // ─── Full Workflow ───────────────────────────────────────────────────────

    [Test]
    public async Task FullWorkflow_CreateToComplete()
    {
        string defId = $"DGPT-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IIncompleteRecordGrain>($"DGPT:{defId}");
        await grain.CreateDeficiencyAsync("PAT-FW", "FULL,WORKFLOW", "PROV-FW", "DR. FW",
            "MISSING_DISCHARGE_SUMMARY", "DC summary needed", null, "ADM-FW",
            DateTime.UtcNow.AddDays(-10), 30);

        Assert.That((await grain.GetDeficiencyAsync()).Status, Is.EqualTo("OPEN"));

        await grain.CompleteAsync("PROV-FW");
        IncompleteRecordState final = await grain.GetDeficiencyAsync();
        Assert.That(final.Status, Is.EqualTo("COMPLETED"));
        Assert.That(final.CompletedBy, Is.EqualTo("PROV-FW"));
    }
}
