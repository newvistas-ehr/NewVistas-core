// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
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
    public void Allergies_RendersLookupBar()
    {
        var cut = Ctx.Render<Allergies>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void Allergies_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<Allergies>();

        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task Allergies_LoadsAllergiesFromGrain()
    {
        var allergies = new List<AllergySummary>
        {
            new() { Allergen = "Penicillin", AllergenType = "Drug", Severity = "Severe",
                     Reactions = ["Rash", "Hives"], ObservedHistorical = "O" },
            new() { Allergen = "Peanuts", AllergenType = "Food", Severity = "Moderate",
                     Reactions = ["Swelling"], ObservedHistorical = "H" }
        };
        MockWorkflowGrain.GetAllergiesAsync().Returns(allergies);

        var cut = Ctx.Render<Allergies>();

        // Type patient ID and click Load
        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Verify grain was called
        await MockWorkflowGrain.Received(1).GetAllergiesAsync();

        // Verify allergies are displayed in table
        Assert.That(cut.Markup, Does.Contain("Penicillin"));
        Assert.That(cut.Markup, Does.Contain("Peanuts"));
        Assert.That(cut.Markup, Does.Contain("Severe"));
        Assert.That(cut.Markup, Does.Contain("Rash, Hives"));
    }

    [Test]
    public async Task Allergies_ShowsNkaWhenEmpty()
    {
        MockWorkflowGrain.GetAllergiesAsync().Returns(new List<AllergySummary>());

        var cut = Ctx.Render<Allergies>();

        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No Known Allergies"));
    }

    [Test]
    public async Task Allergies_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetAllergiesAsync().Returns<List<AllergySummary>>(
            _ => throw new Exception("Silo unreachable"));

        var cut = Ctx.Render<Allergies>();

        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error loading allergies"));
        Assert.That(cut.Markup, Does.Contain("Silo unreachable"));
    }

    [Test]
    public async Task Allergies_RendersTabs()
    {
        MockWorkflowGrain.GetAllergiesAsync().Returns(new List<AllergySummary>());
        var cut = Ctx.Render<Allergies>();

        // Use NavigationManager to set query parameter (SupplyParameterFromQuery)
        var nav = Ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(nav.GetUriWithQueryParameter("patientId", "PATIENT-001"));
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

        var cut = Ctx.Render<Allergies>();

        // Type patient ID and load
        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Switch to record tab and wait for form to appear
        cut.FindAll("button.tab")[1].Click();
        cut.WaitForState(() => cut.Markup.Contains("Record Allergy") && cut.FindAll("input.form-input").Count > 0);

        // Fill out allergen (required field)
        cut.Find("input.form-input").Change("Aspirin");

        // Submit
        await cut.Find(".form-actions button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Verify grain was called
        await MockWorkflowGrain.Received(1).RecordAllergyAsync(
            "Aspirin", Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<List<string>?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>());
    }
}
