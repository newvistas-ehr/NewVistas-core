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
public class MedicationsPageTests : BlazorTestBase
{
    [Test]
    public void Medications_RendersPageTitle()
    {
        var cut = Ctx.Render<Medications>();

        Assert.That(cut.Markup, Does.Contain("Active Medications"));
    }

    [Test]
    public void Medications_PromptsToSelectPatientWhenNoneChosen()
    {
        var cut = Ctx.Render<Medications>();

        Assert.That(cut.Markup, Does.Contain("Select a patient"));
    }

    [Test]
    public void Medications_ShowsSelectedPatientInBar()
    {
        SelectPatient("PATIENT-001", "SMITH, JOHN");

        var cut = Ctx.Render<Medications>();

        Assert.That(cut.Markup, Does.Contain("Change patient"));
    }

    [Test]
    public void Medications_LoadsDataFromGrain()
    {
        var meds = new List<MedicationSummary>
        {
            new() { DrugName = "Lisinopril 10mg", Sig = "Take 1 tablet daily", Status = "Active",
                     FillDate = DateTime.UtcNow, RefillsRemaining = 3 },
            new() { DrugName = "Metformin 500mg", Sig = "Take 1 tablet twice daily", Status = "Active",
                     FillDate = DateTime.UtcNow, RefillsRemaining = 5 }
        };
        MockWorkflowGrain.GetActiveMedicationsAsync().Returns(meds);

        SelectPatient("PATIENT-001");
        var cut = Ctx.Render<Medications>();

        Assert.That(cut.Markup, Does.Contain("Lisinopril 10mg"));
        Assert.That(cut.Markup, Does.Contain("Metformin 500mg"));
    }

    [Test]
    public void Medications_ShowsEmptyState()
    {
        MockWorkflowGrain.GetActiveMedicationsAsync().Returns(new List<MedicationSummary>());

        SelectPatient("PATIENT-002");
        var cut = Ctx.Render<Medications>();

        Assert.That(cut.Markup, Does.Contain("No active medications"));
    }

    [Test]
    public void Medications_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetActiveMedicationsAsync().Returns<List<MedicationSummary>>(
            _ => throw new Exception("Silo unreachable"));

        SelectPatient("PATIENT-003");
        var cut = Ctx.Render<Medications>();

        Assert.That(cut.Markup, Does.Contain("Error loading medications"));
        Assert.That(cut.Markup, Does.Contain("Silo unreachable"));
    }
}
