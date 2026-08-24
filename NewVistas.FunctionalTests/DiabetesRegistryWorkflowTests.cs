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

/// <summary>
/// Functional tests for the diabetes-registry workflow path on
/// <see cref="IPatientWorkflowGrain"/>. Validates the feature gate, the
/// per-patient and index-grain integration, and that the snapshot/pre-
/// visit plan flow data correctly through the workflow grain.
/// </summary>
[TestFixture]
public class DiabetesRegistryWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
        // Default test cluster is a "non-tribal" deployment. Enable the
        // feature once for these tests; per-test fixtures don't toggle it.
        ISiteParametersGrain siteParams =
            _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        await siteParams.EnableFeatureAsync("DIABETES_REGISTRY");
    }

    private IPatientWorkflowGrain Workflow(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IDiabetesRegistryGrain Registry(string icn) =>
        _cluster.GrainFactory.GetGrain<IDiabetesRegistryGrain>($"DM-REG:{icn}");

    [Test]
    public async Task Enroll_AddsToIndex_AndPersistsType()
    {
        string icn = $"DM-PAT-{Guid.NewGuid():N}";
        DateTime enrolled = new(2025, 6, 1);

        await Workflow(icn).EnrollInDiabetesRegistryAsync("TYPE_2", enrolled);

        DiabetesRegistryState state = await Registry(icn).GetAsync();
        Assert.That(state.IsEnrolled, Is.True);
        Assert.That(state.DiabetesType, Is.EqualTo("TYPE_2"));
        Assert.That(state.EnrollmentDate, Is.EqualTo(enrolled));

        IDiabetesRegistryIndexGrain idx =
            _cluster.GrainFactory.GetGrain<IDiabetesRegistryIndexGrain>("DM-REGISTRY-IDX");
        List<string> enrolledIcns = await idx.GetEnrolledIcnsAsync();
        Assert.That(enrolledIcns, Does.Contain(icn));
    }

    [Test]
    public async Task Enroll_IsIdempotent_PreservesOriginalEnrollmentDate()
    {
        string icn = $"DM-PAT-{Guid.NewGuid():N}";
        DateTime first = new(2024, 1, 1);
        DateTime second = new(2026, 1, 1);

        await Workflow(icn).EnrollInDiabetesRegistryAsync("TYPE_2", first);
        await Workflow(icn).EnrollInDiabetesRegistryAsync("TYPE_1", second);     // type updates...

        DiabetesRegistryState state = await Registry(icn).GetAsync();
        Assert.That(state.EnrollmentDate, Is.EqualTo(first), "Enrollment date should not regress.");
        Assert.That(state.DiabetesType, Is.EqualTo("TYPE_1"), "Type should reflect the latest enrollment call.");
    }

    [Test]
    public async Task RecordHbA1c_AppendsToHistory_AndDrivesSnapshot()
    {
        string icn = $"DM-PAT-{Guid.NewGuid():N}";
        await Workflow(icn).EnrollInDiabetesRegistryAsync("TYPE_2", new DateTime(2025, 1, 1));
        await Workflow(icn).RecordDiabetesHbA1cAsync(8.0m, new DateTime(2025, 6, 1));
        await Workflow(icn).RecordDiabetesHbA1cAsync(6.9m, new DateTime(2025, 12, 1));

        DiabetesRegistrySnapshot snap = await Workflow(icn).GetDiabetesRegistrySnapshotAsync();
        Assert.That(snap.LastHbA1cValue, Is.EqualTo(6.9m));
        Assert.That(snap.HbA1cControl, Is.EqualTo(HbA1cControlStatus.Good));
    }

    [Test]
    public async Task RecordExams_DriveSnapshotStatuses()
    {
        string icn = $"DM-PAT-{Guid.NewGuid():N}";
        await Workflow(icn).EnrollInDiabetesRegistryAsync("TYPE_2", new DateTime(2025, 1, 1));

        // Anchored to now, NOT a hardcoded date: this test reads the snapshot, and
        // GetSnapshotAsync computes due/overdue against DateTime.UtcNow. With a fixed
        // visit date the exam ages drifted with real time — the 14-month eye exam below
        // silently crossed the 15-month "overdue" boundary on 2026-07-01 and the test
        // began failing on its own. (The pre-visit-plan test is unaffected: it passes its
        // visit date in explicitly.)
        DateTime visitDate = DateTime.UtcNow;
        await Workflow(icn).RecordDiabetesFootExamAsync(visitDate.AddMonths(-3), "Dr. Begay");
        await Workflow(icn).RecordDiabetesEyeExamAsync(visitDate.AddMonths(-14), "Dr. Yazzie");  // 14mo → Due
        await Workflow(icn).RecordDiabetesAcrAsync(15m, visitDate.AddMonths(-18));               // 18mo → Overdue
        await Workflow(icn).RecordDiabetesEgfrAsync(45m, visitDate.AddMonths(-2));               // Reduced

        DiabetesRegistrySnapshot snap = await Workflow(icn).GetDiabetesRegistrySnapshotAsync();
        Assert.That(snap.FootExamStatus, Is.EqualTo(DueStatus.UpToDate));
        Assert.That(snap.EyeExamStatus, Is.EqualTo(DueStatus.Due));
        Assert.That(snap.AcrStatus, Is.EqualTo(DueStatus.Overdue));
        Assert.That(snap.KidneyFunction, Is.EqualTo(KidneyFunctionStatus.Reduced));
        Assert.That(snap.LastEgfrValue, Is.EqualTo(45m));
    }

    [Test]
    public async Task PreVisitPlan_AssemblesActionItemsFromSnapshot()
    {
        string icn = $"DM-PAT-{Guid.NewGuid():N}";
        DateTime visit = new(2026, 6, 1);
        await Workflow(icn).EnrollInDiabetesRegistryAsync("TYPE_2", new DateTime(2024, 1, 1));
        // Recent up-to-date HbA1c, but POOR control → goes to overdue list per the rules.
        await Workflow(icn).RecordDiabetesHbA1cAsync(9.4m, visit.AddMonths(-2));
        await Workflow(icn).RecordDiabetesFootExamAsync(visit.AddMonths(-13), "Dr.");   // Due
        // Eye exam never recorded → Overdue.
        // ACR never recorded → Overdue.

        DiabetesPreVisitPlan plan = await Workflow(icn).GetDiabetesPreVisitPlanAsync(visit);

        Assert.That(plan.ItemsOverdue.Any(i => i.Contains("poor control")), Is.True);
        Assert.That(plan.ItemsOverdue.Any(i => i.Contains("eye exam never")), Is.True);
        Assert.That(plan.ItemsOverdue.Any(i => i.Contains("nephropathy")), Is.True);
        Assert.That(plan.ItemsDue.Any(i => i.Contains("foot exam due")), Is.True);
    }

    [Test]
    public async Task RecordOlderExam_DoesNotMoveMostRecentBackwards()
    {
        string icn = $"DM-PAT-{Guid.NewGuid():N}";
        await Workflow(icn).EnrollInDiabetesRegistryAsync("TYPE_2", new DateTime(2025, 1, 1));

        DateTime newer = new(2026, 1, 1);
        DateTime older = new(2024, 1, 1);
        await Workflow(icn).RecordDiabetesFootExamAsync(newer, "Dr. New");
        await Workflow(icn).RecordDiabetesFootExamAsync(older, "Dr. Old");

        DiabetesRegistryState state = await Registry(icn).GetAsync();
        Assert.That(state.LastFootExamDate, Is.EqualTo(newer),
            "Out-of-order recording must not regress the most-recent date.");
        Assert.That(state.LastFootExamProviderName, Is.EqualTo("Dr. New"));
    }

    [Test]
    public void RecordHbA1c_OutOfRangeValue_Throws()
    {
        string icn = $"DM-PAT-{Guid.NewGuid():N}";
        Assert.That(
            async () =>
            {
                await Workflow(icn).EnrollInDiabetesRegistryAsync("TYPE_2", new DateTime(2025, 1, 1));
                await Workflow(icn).RecordDiabetesHbA1cAsync(99m, DateTime.UtcNow);
            },
            Throws.InstanceOf<ArgumentOutOfRangeException>().Or.InstanceOf<Orleans.Runtime.OrleansException>());
    }

    [Test]
    public async Task GetSnapshot_FeatureDisabled_ReturnsEmptySnapshot_DoesNotThrow()
    {
        // Disable feature for this test, then re-enable for other tests.
        ISiteParametersGrain siteParams =
            _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        await siteParams.DisableFeatureAsync("DIABETES_REGISTRY");
        try
        {
            string icn = $"DM-PAT-{Guid.NewGuid():N}";
            DiabetesRegistrySnapshot snap = await Workflow(icn).GetDiabetesRegistrySnapshotAsync();

            // Feature off: returns an empty snapshot, NOT enrolled.
            Assert.That(snap.IsEnrolled, Is.False);
            Assert.That(snap.HbA1cControl, Is.EqualTo(HbA1cControlStatus.NoData));
        }
        finally
        {
            await siteParams.EnableFeatureAsync("DIABETES_REGISTRY");
        }
    }

    [Test]
    public async Task EnrollWithFeatureDisabled_Throws()
    {
        ISiteParametersGrain siteParams =
            _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        await siteParams.DisableFeatureAsync("DIABETES_REGISTRY");
        try
        {
            string icn = $"DM-PAT-{Guid.NewGuid():N}";
            Assert.That(
                async () => await Workflow(icn).EnrollInDiabetesRegistryAsync("TYPE_2", DateTime.UtcNow),
                Throws.InstanceOf<InvalidOperationException>().Or.InstanceOf<Orleans.Runtime.OrleansException>());
        }
        finally
        {
            await siteParams.EnableFeatureAsync("DIABETES_REGISTRY");
        }
    }

    [Test]
    public async Task RegistryIndex_GetCount_ReflectsEnrollments()
    {
        IDiabetesRegistryIndexGrain idx =
            _cluster.GrainFactory.GetGrain<IDiabetesRegistryIndexGrain>("DM-REGISTRY-IDX");
        int before = await idx.GetCountAsync();

        // Enroll 3 fresh patients.
        for (int i = 0; i < 3; i++)
        {
            string icn = $"DM-IDX-{Guid.NewGuid():N}";
            await Workflow(icn).EnrollInDiabetesRegistryAsync("TYPE_2", DateTime.UtcNow);
        }

        int after = await idx.GetCountAsync();
        Assert.That(after - before, Is.EqualTo(3));
    }
}
