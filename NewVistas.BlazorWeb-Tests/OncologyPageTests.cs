// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class OncologyPageTests : BlazorTestBase
{
    [Test]
    public void Oncology_RendersPageTitle()
    {
        var cut = Ctx.Render<Oncology>();
        Assert.That(cut.Markup, Does.Contain("Oncology"));
        Assert.That(cut.Markup, Does.Contain("Tumor Registry"));
    }

    [Test]
    public void Oncology_RendersPatientInput()
    {
        var cut = Ctx.Render<Oncology>();
        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
    }

    [Test]
    public async Task Oncology_LoadsTumorsFromGrain()
    {
        var tumors = new List<OncologyTumorIndexEntry>
        {
            new() { TumorId = "ONC-T1", PrimarySite = "C34.1", PrimarySiteText = "Upper lobe of lung",
                     Histology = "8140/3", HistologyText = "Adenocarcinoma",
                     DateOfDiagnosis = new DateTime(2025, 6, 15), Status = OncologyStatus.Active,
                     SequenceNumber = 1 }
        };
        MockWorkflowGrain.GetOncologyTumorsAsync().Returns(tumors);
        MockWorkflowGrain.GetOncologyTreatmentsAsync().Returns(new List<OncologyTreatmentIndexEntry>());

        var cut = Ctx.Render<Oncology>();
        cut.Find("input.lookup-input").Change("PAT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Adenocarcinoma"));
        Assert.That(cut.Markup, Does.Contain("Upper lobe of lung"));
    }

    [Test]
    public async Task Oncology_ShowsErrorOnFailure()
    {
        MockWorkflowGrain.GetOncologyTumorsAsync().Returns<List<OncologyTumorIndexEntry>>(
            _ => throw new Exception("Grain error"));

        var cut = Ctx.Render<Oncology>();
        cut.Find("input.lookup-input").Change("PAT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Failed to load"));
    }
}
