// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class RadiologyPageTests : BlazorTestBase
{
    [Test]
    public void Radiology_RendersPageTitle()
    {
        var cut = Ctx.Render<Radiology>();

        Assert.That(cut.Markup, Does.Contain("Radiology"));
    }

    [Test]
    public void Radiology_RendersLookupBar()
    {
        var cut = Ctx.Render<Radiology>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void Radiology_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<Radiology>();

        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task Radiology_LoadsDataFromGrain()
    {
        var studies = new List<RadiologySummary>
        {
            new() { RadiologyId = "RAD-001", ProcedureName = "Chest X-Ray PA and Lateral",
                     ImagingType = "GENERAL RADIOLOGY", Status = "COMPLETE",
                     ExamDateTime = DateTime.UtcNow, RequestingProviderName = "Dr. Smith", HasReport = true },
            new() { RadiologyId = "RAD-002", ProcedureName = "CT Head Without Contrast",
                     ImagingType = "CT SCAN", Status = "PENDING",
                     ExamDateTime = null, RequestingProviderName = "Dr. Jones", HasReport = false }
        };
        MockWorkflowGrain.GetRadiologyStudiesAsync(100).Returns(studies);

        var cut = Ctx.Render<Radiology>();

        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetRadiologyStudiesAsync(100);

        Assert.That(cut.Markup, Does.Contain("Chest X-Ray PA and Lateral"));
        Assert.That(cut.Markup, Does.Contain("CT Head Without Contrast"));
    }

    [Test]
    public async Task Radiology_ShowsEmptyState()
    {
        MockWorkflowGrain.GetRadiologyStudiesAsync(100).Returns(new List<RadiologySummary>());

        var cut = Ctx.Render<Radiology>();

        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No radiology studies found"));
    }

    [Test]
    public async Task Radiology_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetRadiologyStudiesAsync(100).Returns<List<RadiologySummary>>(
            _ => throw new Exception("Silo unreachable"));

        var cut = Ctx.Render<Radiology>();

        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Silo unreachable"));
    }
}
