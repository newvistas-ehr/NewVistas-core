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
public class VitalsPageTests : BlazorTestBase
{
    [Test]
    public void Vitals_RendersPageTitle()
    {
        var cut = Ctx.Render<Vitals>();

        Assert.That(cut.Markup, Does.Contain("Vitals"));
    }

    [Test]
    public void Vitals_RendersLookupBar()
    {
        var cut = Ctx.Render<Vitals>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void Vitals_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<Vitals>();

        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task Vitals_LoadsDataFromGrain()
    {
        var vitals = new List<VitalSummary>
        {
            new() { VitalType = "TEMPERATURE", Value = "98.6", Units = "F", DateTimeTaken = DateTime.UtcNow },
            new() { VitalType = "PULSE", Value = "72", Units = "bpm", DateTimeTaken = DateTime.UtcNow, AbnormalFlag = "H" }
        };
        MockWorkflowGrain.GetLatestVitalsAsync().Returns(vitals);

        var cut = Ctx.Render<Vitals>();

        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetLatestVitalsAsync();

        Assert.That(cut.Markup, Does.Contain("TEMPERATURE"));
        Assert.That(cut.Markup, Does.Contain("98.6"));
        Assert.That(cut.Markup, Does.Contain("PULSE"));
    }

    [Test]
    public async Task Vitals_ShowsEmptyState()
    {
        MockWorkflowGrain.GetLatestVitalsAsync().Returns(new List<VitalSummary>());

        var cut = Ctx.Render<Vitals>();

        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No vitals recorded"));
    }

    [Test]
    public async Task Vitals_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetLatestVitalsAsync().Returns<List<VitalSummary>>(
            _ => throw new Exception("Silo unreachable"));

        var cut = Ctx.Render<Vitals>();

        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error loading vitals"));
        Assert.That(cut.Markup, Does.Contain("Silo unreachable"));
    }
}
