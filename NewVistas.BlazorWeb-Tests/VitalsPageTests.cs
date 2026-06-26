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
public class VitalsPageTests : BlazorTestBase
{
    [Test]
    public void Vitals_RendersPageTitle()
    {
        var cut = Ctx.Render<Vitals>();

        Assert.That(cut.Markup, Does.Contain("Vitals"));
    }

    [Test]
    public void Vitals_PromptsToSelectPatientWhenNoneChosen()
    {
        var cut = Ctx.Render<Vitals>();

        Assert.That(cut.Markup, Does.Contain("Select a patient"));
    }

    [Test]
    public void Vitals_ShowsSelectedPatientInBar()
    {
        SelectPatient("PATIENT-001", "SMITH, JOHN");

        var cut = Ctx.Render<Vitals>();

        Assert.That(cut.Markup, Does.Contain("Change patient"));
    }

    [Test]
    public void Vitals_LoadsDataFromGrain()
    {
        var vitals = new List<VitalSummary>
        {
            new() { VitalType = "TEMPERATURE", Value = "98.6", Units = "F", DateTimeTaken = DateTime.UtcNow },
            new() { VitalType = "PULSE", Value = "72", Units = "bpm", DateTimeTaken = DateTime.UtcNow, AbnormalFlag = "H" }
        };
        MockWorkflowGrain.GetLatestVitalsAsync().Returns(vitals);

        SelectPatient("PATIENT-001");
        var cut = Ctx.Render<Vitals>();

        Assert.That(cut.Markup, Does.Contain("TEMPERATURE"));
        Assert.That(cut.Markup, Does.Contain("98.6"));
        Assert.That(cut.Markup, Does.Contain("PULSE"));
    }

    [Test]
    public void Vitals_ShowsEmptyState()
    {
        MockWorkflowGrain.GetLatestVitalsAsync().Returns(new List<VitalSummary>());

        SelectPatient("PATIENT-002");
        var cut = Ctx.Render<Vitals>();

        Assert.That(cut.Markup, Does.Contain("No vitals recorded"));
    }

    [Test]
    public void Vitals_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetLatestVitalsAsync().Returns<List<VitalSummary>>(
            _ => throw new Exception("Silo unreachable"));

        SelectPatient("PATIENT-003");
        var cut = Ctx.Render<Vitals>();

        Assert.That(cut.Markup, Does.Contain("Error loading vitals"));
        Assert.That(cut.Markup, Does.Contain("Silo unreachable"));
    }
}
