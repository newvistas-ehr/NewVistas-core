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
public class ImagingPageTests : BlazorTestBase
{
    [Test]
    public void Imaging_RendersPageTitle()
    {
        var cut = Ctx.Render<Imaging>();
        Assert.That(cut.Markup, Does.Contain("Imaging"));
    }

    [Test]
    public void Imaging_PromptsToSelectPatientWhenNoneChosen()
    {
        var cut = Ctx.Render<Imaging>();
        Assert.That(cut.Markup, Does.Contain("Select a patient"));
    }

    [Test]
    public void Imaging_ShowsSelectedPatientInBar()
    {
        SelectPatient("PATIENT-001", "SMITH, JOHN");
        var cut = Ctx.Render<Imaging>();
        Assert.That(cut.Markup, Does.Contain("Change patient"));
    }

    [Test]
    public void Imaging_LoadsDataFromGrain()
    {
        var images = new List<ImagingSummary>
        {
            new() { ImageId = "IMG-001", ObjectType = "XRAY",
                     ProcedureDescription = "Chest X-Ray", Status = "VIEWABLE",
                     CaptureDate = DateTime.Today, ImageCount = 2 }
        };
        MockWorkflowGrain.GetImagesAsync(Arg.Any<int>()).Returns(images);

        SelectPatient("PATIENT-001");
        var cut = Ctx.Render<Imaging>();

        Assert.That(cut.Markup, Does.Contain("Chest X-Ray"));
        Assert.That(cut.Markup, Does.Contain("XRAY"));
    }

    [Test]
    public void Imaging_ShowsEmptyState()
    {
        MockWorkflowGrain.GetImagesAsync(Arg.Any<int>()).Returns(new List<ImagingSummary>());

        SelectPatient("PATIENT-002");
        var cut = Ctx.Render<Imaging>();

        Assert.That(cut.Markup, Does.Contain("No images found"));
    }

    [Test]
    public void Imaging_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetImagesAsync(Arg.Any<int>()).Returns<List<ImagingSummary>>(
            _ => throw new Exception("PACS unavailable"));

        SelectPatient("PATIENT-003");
        var cut = Ctx.Render<Imaging>();

        Assert.That(cut.Markup, Does.Contain("Error"));
        Assert.That(cut.Markup, Does.Contain("PACS unavailable"));
    }
}
