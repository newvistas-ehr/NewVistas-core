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
public class LabsPageTests : BlazorTestBase
{
    [Test]
    public void Labs_RendersPageTitle()
    {
        var cut = Ctx.Render<Labs>();

        Assert.That(cut.Markup, Does.Contain("Laboratory Information System"));
    }

    [Test]
    public void Labs_PromptsToSelectPatientWhenNoneChosen()
    {
        var cut = Ctx.Render<Labs>();

        Assert.That(cut.Markup, Does.Contain("Select a patient"));
    }

    [Test]
    public void Labs_ShowsSelectedPatientInBar()
    {
        MockWorkflowGrain.GetLabResultsAsync().Returns(new List<LabResultSummary>());

        SelectPatient("PATIENT-001", "SMITH, JOHN");
        var cut = Ctx.Render<Labs>();

        Assert.That(cut.Markup, Does.Contain("Change patient"));
    }

    [Test]
    public void Labs_LoadsDataFromGrain()
    {
        var results = new List<LabResultSummary>
        {
            new() { LabTestId = "LAB-001", TestName = "CBC", ResultValue = "7.5", Units = "K/cmm",
                     Flag = "H", Status = "Completed", CollectionDate = DateTime.UtcNow },
            new() { LabTestId = "LAB-002", TestName = "BMP", Status = "Ordered" }
        };
        MockWorkflowGrain.GetLabResultsAsync().Returns(results);

        // Patient chosen in Patient Lookup; the Results tab auto-loads on render.
        SelectPatient("PATIENT-001");
        var cut = Ctx.Render<Labs>();

        Assert.That(cut.Markup, Does.Contain("CBC"));
        Assert.That(cut.Markup, Does.Contain("7.5"));
    }

    [Test]
    public void Labs_ShowsEmptyState()
    {
        MockWorkflowGrain.GetLabResultsAsync().Returns(new List<LabResultSummary>());

        SelectPatient("PATIENT-002");
        var cut = Ctx.Render<Labs>();

        Assert.That(cut.Markup, Does.Contain("No lab orders found"));
    }

    [Test]
    public void Labs_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetLabResultsAsync().Returns<List<LabResultSummary>>(
            _ => throw new Exception("Silo unreachable"));

        SelectPatient("PATIENT-003");
        var cut = Ctx.Render<Labs>();

        Assert.That(cut.Markup, Does.Contain("Silo unreachable"));
    }
}
