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
public class ConsultsPageTests : BlazorTestBase
{
    [Test]
    public void Consults_RendersPageTitle()
    {
        var cut = Ctx.Render<Consults>();

        Assert.That(cut.Markup, Does.Contain("Consults"));
    }

    [Test]
    public void Consults_PromptsToSelectPatientWhenNoneChosen()
    {
        var cut = Ctx.Render<Consults>();

        Assert.That(cut.Markup, Does.Contain("Select a patient"));
    }

    [Test]
    public void Consults_ShowsSelectedPatientInBar()
    {
        SelectPatient("PATIENT-001", "SMITH, JOHN");

        var cut = Ctx.Render<Consults>();

        Assert.That(cut.Markup, Does.Contain("Change patient"));
    }

    [Test]
    public void Consults_LoadsDataFromGrain()
    {
        var consults = new List<ConsultSummary>
        {
            new() { ConsultId = "C-001", ToService = "Cardiology", Status = "PENDING",
                     Urgency = "ROUTINE", RequestDateTime = DateTime.UtcNow },
            new() { ConsultId = "C-002", ToService = "Orthopedics", Status = "ACTIVE",
                     Urgency = "URGENT", RequestDateTime = DateTime.UtcNow }
        };
        MockWorkflowGrain.GetConsultsAsync(null, 100).Returns(consults);

        SelectPatient("PATIENT-001");
        var cut = Ctx.Render<Consults>();

        Assert.That(cut.Markup, Does.Contain("Cardiology"));
        Assert.That(cut.Markup, Does.Contain("Orthopedics"));
    }

    [Test]
    public void Consults_ShowsEmptyState()
    {
        MockWorkflowGrain.GetConsultsAsync(null, 100).Returns(new List<ConsultSummary>());

        SelectPatient("PATIENT-002");
        var cut = Ctx.Render<Consults>();

        Assert.That(cut.Markup, Does.Contain("No consults found"));
    }

    [Test]
    public void Consults_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetConsultsAsync(null, 100).Returns<List<ConsultSummary>>(
            _ => throw new Exception("Silo unreachable"));

        SelectPatient("PATIENT-003");
        var cut = Ctx.Render<Consults>();

        Assert.That(cut.Markup, Does.Contain("Error loading consults"));
        Assert.That(cut.Markup, Does.Contain("Silo unreachable"));
    }
}
