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
public class DrgGrouperWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task DrgGrain_AssignsHeartFailureWithMCC()
    {
        var grain = _cluster.GrainFactory.GetGrain<IDrgAssignmentGrain>($"DRG:ADM-{Guid.NewGuid():N}");
        await grain.AssignDrgAsync("PAT-001", "DOE,JOHN", "I50.9", "Heart failure",
            new List<string> { "E11.9", "I10", "N18.3" }, new List<string>(), 72, "M", "HOME", 5);

        DrgAssignmentState state = await grain.GetAssignmentAsync();
        Assert.That(state.DrgCode, Is.EqualTo("291"));
        Assert.That(state.CcMccLevel, Is.EqualTo("MCC"));
        Assert.That(state.DrgType, Is.EqualTo("MEDICAL"));
        Assert.That(state.RelativeWeight, Is.GreaterThan(1.0m));
    }

    [Test]
    public async Task DrgGrain_AssignsHeartFailureWithCC()
    {
        var grain = _cluster.GrainFactory.GetGrain<IDrgAssignmentGrain>($"DRG:ADM-{Guid.NewGuid():N}");
        await grain.AssignDrgAsync("PAT-002", "SMITH,J", "I50.9", "Heart failure",
            new List<string> { "I10" }, new List<string>(), 65, "F", "HOME", 4);

        Assert.That((await grain.GetAssignmentAsync()).DrgCode, Is.EqualTo("292"));
    }

    [Test]
    public async Task DrgGrain_AssignsHeartFailureWithoutCcMcc()
    {
        var grain = _cluster.GrainFactory.GetGrain<IDrgAssignmentGrain>($"DRG:ADM-{Guid.NewGuid():N}");
        await grain.AssignDrgAsync("PAT-003", "W,A", "I50.9", "Heart failure",
            new List<string>(), new List<string>(), 60, "M", "HOME", 3);

        Assert.That((await grain.GetAssignmentAsync()).DrgCode, Is.EqualTo("293"));
    }

    [Test]
    public async Task DrgGrain_AssignsPneumonia()
    {
        var grain = _cluster.GrainFactory.GetGrain<IDrgAssignmentGrain>($"DRG:ADM-{Guid.NewGuid():N}");
        await grain.AssignDrgAsync("PAT-004", "B,P", "J18.9", "Pneumonia",
            new List<string> { "J44.1", "E11.9", "I10" }, new List<string>(), 68, "F", "HOME", 4);

        DrgAssignmentState state = await grain.GetAssignmentAsync();
        Assert.That(state.DrgCode, Is.EqualTo("177"));
        Assert.That(state.MdcCode, Is.EqualTo("04"));
    }

    [Test]
    public async Task DrgGrain_AssignsHipReplacement()
    {
        var grain = _cluster.GrainFactory.GetGrain<IDrgAssignmentGrain>($"DRG:ADM-{Guid.NewGuid():N}");
        await grain.AssignDrgAsync("PAT-005", "C,D", "M17.11", "OA right knee",
            new List<string>(), new List<string> { "0SRC0JZ" }, 65, "M", "HOME", 2);

        DrgAssignmentState state = await grain.GetAssignmentAsync();
        Assert.That(state.DrgCode, Is.EqualTo("470"));
        Assert.That(state.DrgType, Is.EqualTo("SURGICAL"));
    }

    [Test]
    public async Task DrgGrain_AssignsSepsis()
    {
        var grain = _cluster.GrainFactory.GetGrain<IDrgAssignmentGrain>($"DRG:ADM-{Guid.NewGuid():N}");
        await grain.AssignDrgAsync("PAT-006", "E,F", "A41.9", "Sepsis",
            new List<string> { "R65.20" }, new List<string>(), 78, "F", "SNF", 8);

        Assert.That((await grain.GetAssignmentAsync()).DrgCode, Is.EqualTo("871"));
    }

    [Test]
    public async Task DrgGrain_DetectsOutlier()
    {
        var grain = _cluster.GrainFactory.GetGrain<IDrgAssignmentGrain>($"DRG:ADM-{Guid.NewGuid():N}");
        // Heart failure geometric mean ~4.6, so LOS=15 > 9.2 should be outlier
        await grain.AssignDrgAsync("PAT-007", "G,H", "I50.9", "HF",
            new List<string> { "E11.9", "I10", "N18.3" }, new List<string>(), 85, "M", "HOME", 15);

        Assert.That((await grain.GetAssignmentAsync()).IsOutlier, Is.True);
    }

    [Test]
    public async Task DrgGrain_CanReview()
    {
        var grain = _cluster.GrainFactory.GetGrain<IDrgAssignmentGrain>($"DRG:ADM-{Guid.NewGuid():N}");
        await grain.AssignDrgAsync("PAT-008", "I,J", "J44.1", "COPD",
            new List<string> { "I10" }, new List<string>(), 70, "M", "HOME", 3);
        await grain.ReviewAsync("CODER-001");

        Assert.That((await grain.GetAssignmentAsync()).Status, Is.EqualTo("REVIEWED"));
    }

    [Test]
    public async Task DrgGrain_CanFinalize()
    {
        var grain = _cluster.GrainFactory.GetGrain<IDrgAssignmentGrain>($"DRG:ADM-{Guid.NewGuid():N}");
        await grain.AssignDrgAsync("PAT-009", "K,L", "I50.9", "HF",
            new List<string>(), new List<string>(), 60, "F", "HOME", 3);
        await grain.ReviewAsync("CODER-001");
        await grain.FinalizeAsync();

        Assert.That((await grain.GetAssignmentAsync()).Status, Is.EqualTo("FINAL"));
    }

    [Test]
    public async Task DrgGrain_CanReassign()
    {
        var grain = _cluster.GrainFactory.GetGrain<IDrgAssignmentGrain>($"DRG:ADM-{Guid.NewGuid():N}");
        await grain.AssignDrgAsync("PAT-010", "M,N", "I50.9", "HF",
            new List<string>(), new List<string>(), 60, "M", "HOME", 3);
        await grain.ReassignDrgAsync("292", "Heart Failure w CC (manually reassigned)", 0.9901m, "Coder correction");

        DrgAssignmentState state = await grain.GetAssignmentAsync();
        Assert.That(state.DrgCode, Is.EqualTo("292"));
        Assert.That(state.Status, Is.EqualTo("ASSIGNED")); // Reset to ASSIGNED after reassign
    }

    // ─── Index ───────────────────────────────────────────────────────────────

    [Test]
    public async Task Index_CanTrackAndFilter()
    {
        var index = _cluster.GrainFactory.GetGrain<IDrgIndexGrain>($"DRG-INDEX:FAC-{Guid.NewGuid():N}");
        await index.AddOrUpdateAsync(new DrgAssignmentEntry { AdmissionId = "A1", DrgCode = "291", Status = "ASSIGNED" });
        await index.AddOrUpdateAsync(new DrgAssignmentEntry { AdmissionId = "A2", DrgCode = "470", Status = "FINAL" });

        Assert.That(await index.GetAllAssignmentsAsync(), Has.Count.EqualTo(2));
        Assert.That(await index.GetAssignmentsByStatusAsync("FINAL"), Has.Count.EqualTo(1));
    }

    // ─── Full Lifecycle ──────────────────────────────────────────────────────

    [Test]
    public async Task FullLifecycle_AssignReviewFinalize()
    {
        var grain = _cluster.GrainFactory.GetGrain<IDrgAssignmentGrain>($"DRG:ADM-{Guid.NewGuid():N}");
        await grain.AssignDrgAsync("PAT-LC", "LC,TEST", "I50.9", "Heart failure",
            new List<string> { "E11.9", "I10" }, new List<string>(), 70, "M", "HOME", 4);

        DrgAssignmentState s1 = await grain.GetAssignmentAsync();
        Assert.That(s1.Status, Is.EqualTo("ASSIGNED"));
        Assert.That(s1.DrgCode, Is.Not.Empty);

        await grain.ReviewAsync("CODER-002");
        Assert.That((await grain.GetAssignmentAsync()).Status, Is.EqualTo("REVIEWED"));

        await grain.FinalizeAsync();
        Assert.That((await grain.GetAssignmentAsync()).Status, Is.EqualTo("FINAL"));
    }
}
