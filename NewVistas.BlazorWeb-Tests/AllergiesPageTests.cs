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
public class AllergiesPageTests : BlazorTestBase
{
    [Test]
    public void Allergies_RendersPageTitle()
    {
        var cut = Ctx.Render<Allergies>();

        Assert.That(cut.Markup, Does.Contain("Allergies"));
    }

    [Test]
    public void Allergies_PromptsToSelectPatientWhenNoneChosen()
    {
        var cut = Ctx.Render<Allergies>();

        Assert.That(cut.Markup, Does.Contain("Select a patient"));
    }

    [Test]
    public void Allergies_ShowsSelectedPatientInBar()
    {
        MockWorkflowGrain.GetAllergiesAsync().Returns(new List<AllergySummary>());

        SelectPatient("PATIENT-001", "SMITH, JOHN");
        var cut = Ctx.Render<Allergies>();

        Assert.That(cut.Markup, Does.Contain("Change patient"));
    }

    [Test]
    public void Allergies_LoadsAllergiesFromGrain()
    {
        var allergies = new List<AllergySummary>
        {
            new() { Allergen = "Penicillin", AllergenType = "Drug", Severity = "Severe",
                     Reactions = ["Rash", "Hives"], ObservedHistorical = "O" },
            new() { Allergen = "Peanuts", AllergenType = "Food", Severity = "Moderate",
                     Reactions = ["Swelling"], ObservedHistorical = "H" }
        };
        MockWorkflowGrain.GetAllergiesAsync().Returns(allergies);

        // Patient was chosen in Patient Lookup; the page auto-loads on render.
        SelectPatient("PATIENT-001");
        var cut = Ctx.Render<Allergies>();

        Assert.That(cut.Markup, Does.Contain("Penicillin"));
        Assert.That(cut.Markup, Does.Contain("Peanuts"));
        Assert.That(cut.Markup, Does.Contain("Severe"));
        Assert.That(cut.Markup, Does.Contain("Rash, Hives"));
    }

    [Test]
    public void Allergies_ShowsNkaWhenEmpty()
    {
        MockWorkflowGrain.GetAllergiesAsync().Returns(new List<AllergySummary>());

        SelectPatient("PATIENT-002");
        var cut = Ctx.Render<Allergies>();

        Assert.That(cut.Markup, Does.Contain("No Known Allergies"));
    }

    [Test]
    public void Allergies_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetAllergiesAsync().Returns<List<AllergySummary>>(
            _ => throw new Exception("Silo unreachable"));

        SelectPatient("PATIENT-003");
        var cut = Ctx.Render<Allergies>();

        Assert.That(cut.Markup, Does.Contain("Error loading allergies"));
        Assert.That(cut.Markup, Does.Contain("Silo unreachable"));
    }

    [Test]
    public void Allergies_RendersTabs()
    {
        MockWorkflowGrain.GetAllergiesAsync().Returns(new List<AllergySummary>());

        SelectPatient("PATIENT-001");
        var cut = Ctx.Render<Allergies>();
        cut.WaitForState(() => cut.FindAll("button.tab").Count > 0);

        var tabs = cut.FindAll("button.tab");
        Assert.That(tabs, Has.Count.EqualTo(2));
        Assert.That(tabs[0].TextContent, Does.Contain("Allergies"));
        Assert.That(tabs[1].TextContent, Does.Contain("Record Allergy"));
    }

    [Test]
    public async Task Allergies_SubmitCallsRecordAllergyAsync()
    {
        MockWorkflowGrain.GetAllergiesAsync().Returns(new List<AllergySummary>());
        MockWorkflowGrain.RecordAllergyAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<List<string>?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>()).Returns("ALLERGY-001");

        SelectPatient("PATIENT-001");
        var cut = Ctx.Render<Allergies>();

        // Switch to record tab and wait for the form to appear
        cut.WaitForState(() => cut.FindAll("button.tab").Count > 1);
        cut.FindAll("button.tab")[1].Click();
        cut.WaitForState(() => cut.Markup.Contains("Record Allergy") && cut.FindAll("input.form-input").Count > 0);

        // Fill out allergen (required field)
        cut.Find("input.form-input").Change("Aspirin");

        // Submit
        await cut.Find(".form-actions button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).RecordAllergyAsync(
            "Aspirin", Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<List<string>?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>());
    }
}
