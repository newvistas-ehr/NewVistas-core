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
public class MentalHealthPageTests : BlazorTestBase
{
    [Test]
    public void MentalHealth_RendersPageTitle()
    {
        var cut = Ctx.Render<MentalHealth>();
        Assert.That(cut.Markup, Does.Contain("Mental Health Screening"));
    }

    [Test]
    public void MentalHealth_RendersLookupBar()
    {
        var cut = Ctx.Render<MentalHealth>();
        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void MentalHealth_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<MentalHealth>();
        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task MentalHealth_LoadsDataFromGrain()
    {
        var screens = new List<MentalHealthSummary>
        {
            new() { InstrumentId = "MH-001", InstrumentName = "PHQ-9",
                     AdministrationDateTime = DateTime.Today, TotalScore = 12,
                     ScoreInterpretation = "MODERATE", IsPositiveScreen = true, Status = "COMPLETED" }
        };
        MockWorkflowGrain.GetMentalHealthScreensAsync().Returns(screens);

        var cut = Ctx.Render<MentalHealth>();
        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetMentalHealthScreensAsync();
        Assert.That(cut.Markup, Does.Contain("PHQ-9"));
        Assert.That(cut.Markup, Does.Contain("MODERATE"));
    }

    [Test]
    public async Task MentalHealth_ShowsEmptyState()
    {
        MockWorkflowGrain.GetMentalHealthScreensAsync().Returns(new List<MentalHealthSummary>());

        var cut = Ctx.Render<MentalHealth>();
        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No mental health screens found"));
    }

    [Test]
    public async Task MentalHealth_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetMentalHealthScreensAsync().Returns<List<MentalHealthSummary>>(
            _ => throw new Exception("Silo unreachable"));

        var cut = Ctx.Render<MentalHealth>();
        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error"));
        Assert.That(cut.Markup, Does.Contain("Silo unreachable"));
    }
}
