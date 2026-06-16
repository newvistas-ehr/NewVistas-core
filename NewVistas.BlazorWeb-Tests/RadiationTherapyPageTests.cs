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
public class RadiationTherapyPageTests : BlazorTestBase
{
    [Test]
    public void RadiationTherapy_RendersPageTitle()
    {
        var cut = Ctx.Render<RadiationTherapy>();
        Assert.That(cut.Markup, Does.Contain("Radiation Therapy"));
    }

    [Test]
    public void RadiationTherapy_RendersPatientBar()
    {
        var cut = Ctx.Render<RadiationTherapy>();
        var input = cut.Find("input");
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Enter patient ID"));
    }

    [Test]
    public async Task RadiationTherapy_LoadsCoursesFromGrain()
    {
        var courses = new List<RtCourseIndexEntry>
        {
            new() { CourseId = "RT-001", CourseName = "Prostate IMRT", Status = RtCourseStatus.Active,
                     Intent = RtIntent.Curative, Modality = RtModality.IMRT,
                     TreatmentSite = "Prostate", DiagnosisCode = "C61",
                     PrescribedDoseCgy = 7600, FractionsPlanned = 38 }
        };
        MockWorkflowGrain.GetRtCoursesAsync().Returns(courses);

        var cut = Ctx.Render<RadiationTherapy>();
        cut.Find("input").Change("PAT-001");
        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Prostate IMRT"));
        Assert.That(cut.Markup, Does.Contain("7600"));
    }

    [Test]
    public async Task RadiationTherapy_ShowsErrorOnFailure()
    {
        MockWorkflowGrain.GetRtCoursesAsync().Returns<List<RtCourseIndexEntry>>(
            _ => throw new Exception("Grain error"));

        var cut = Ctx.Render<RadiationTherapy>();
        cut.Find("input").Change("PAT-002");
        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Failed to load"));
    }
}
