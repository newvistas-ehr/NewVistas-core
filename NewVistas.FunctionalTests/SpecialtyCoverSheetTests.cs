// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for the specialty cover sheet: layout-driven section composition, the
/// non-suppressible safety spine, and per-patient-then-viewer resolution — end-to-end via
/// <see cref="IPatientWorkflowGrain.GetSpecialtyCoverSheetAsync"/> on the shared TestCluster.
/// </summary>
[TestFixture]
public class SpecialtyCoverSheetTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private async Task<IPatientWorkflowGrain> NewPatientAsync()
    {
        var wf = Workflow($"CSP-PT-{Guid.NewGuid()}");
        await wf.UpdateDemographicsAsync("COVERSHEET,TEST", "M", new DateTime(1970, 1, 1), "666770001");
        return wf;
    }

    [Test]
    public async Task General_LoadsDeclaredSections_AndSpine()
    {
        var wf = await NewPatientAsync();
        SpecialtyCoverSheet cs = await wf.GetSpecialtyCoverSheetAsync("general", null);

        Assert.That(cs.LayoutId, Is.EqualTo(CoverSheetLayouts.General));
        var keys = cs.Sections.Select(s => s.SectionKey).ToList();
        Assert.That(keys, Does.Contain(CoverSheetSections.Problems));
        Assert.That(keys, Does.Contain(CoverSheetSections.Reminders));
        Assert.That(cs.Demographics.Name, Is.EqualTo("COVERSHEET,TEST"));   // spine loaded
        Assert.That(cs.SectionsLoaded, Is.EqualTo(cs.Sections.Count));
    }

    [Test]
    public async Task Oncology_DropsGeneralOnlySections()
    {
        var wf = await NewPatientAsync();
        SpecialtyCoverSheet cs = await wf.GetSpecialtyCoverSheetAsync("oncology", null);

        Assert.That(cs.LayoutId, Is.EqualTo(CoverSheetLayouts.Oncology));
        var keys = cs.Sections.Select(s => s.SectionKey).ToList();
        Assert.That(keys, Does.Contain(CoverSheetSections.Oncology));
        Assert.That(keys, Does.Not.Contain(CoverSheetSections.Visits));
        Assert.That(keys, Does.Not.Contain(CoverSheetSections.Reminders));
        // A section the layout doesn't declare is not loaded.
        Assert.That(cs.RecentVisits, Is.Empty);
        Assert.That(cs.ClinicalReminders, Is.Empty);
    }

    [Test]
    public async Task Procedural_IncludesProceduresAndImaging()
    {
        var wf = await NewPatientAsync();
        SpecialtyCoverSheet cs = await wf.GetSpecialtyCoverSheetAsync("procedural", null);

        var keys = cs.Sections.Select(s => s.SectionKey).ToList();
        Assert.That(keys, Does.Contain(CoverSheetSections.Procedures));
        Assert.That(keys, Does.Contain(CoverSheetSections.Imaging));
    }

    [Test]
    public async Task SafetySpine_NeverDropped_RegardlessOfLayout()
    {
        var wf = await NewPatientAsync();
        foreach (string layoutId in new[] { CoverSheetLayouts.General, CoverSheetLayouts.Oncology, CoverSheetLayouts.Procedural })
        {
            SpecialtyCoverSheet cs = await wf.GetSpecialtyCoverSheetAsync(layoutId, null);
            Assert.That(cs.Demographics.Name, Is.EqualTo("COVERSHEET,TEST"), $"demographics dropped in {layoutId}");
            Assert.That(cs.Cwad, Is.Not.Null, $"CWAD dropped in {layoutId}");
            Assert.That(cs.Allergies, Is.Not.Null, $"allergies dropped in {layoutId}");
            // The spine is never a declarable section — no layout can even reference it.
            Assert.That(cs.Sections.Select(s => s.SectionKey), Does.Not.Contain("allergies"));
        }
    }

    [Test]
    public async Task Auto_NoContext_ResolvesGeneral()
    {
        var wf = await NewPatientAsync();
        SpecialtyCoverSheet cs = await wf.GetSpecialtyCoverSheetAsync(null, null);
        Assert.That(cs.LayoutId, Is.EqualTo(CoverSheetLayouts.General));
        Assert.That(cs.LayoutReason, Does.Contain("general"));
    }

    [Test]
    public async Task Auto_SurgeonViewer_ButNoSurgery_StaysGeneral()
    {
        // Viewer is a surgeon but the bare patient has no surgery → procedural not relevant → general.
        var wf = await NewPatientAsync();
        SpecialtyCoverSheet cs = await wf.GetSpecialtyCoverSheetAsync(null, "SURGERY");
        Assert.That(cs.LayoutId, Is.EqualTo(CoverSheetLayouts.General));
    }

    [Test]
    public async Task ManualSelection_OverridesViewer_AndReasonIsManual()
    {
        var wf = await NewPatientAsync();
        SpecialtyCoverSheet cs = await wf.GetSpecialtyCoverSheetAsync("oncology", "SURGERY");
        Assert.That(cs.LayoutId, Is.EqualTo(CoverSheetLayouts.Oncology));
        Assert.That(cs.LayoutReason, Does.Contain("Manual"));
    }
}
