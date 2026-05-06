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
public class MedicationsPageTests : BlazorTestBase
{
    [Test]
    public void Medications_RendersPageTitle()
    {
        var cut = Ctx.Render<Medications>();

        Assert.That(cut.Markup, Does.Contain("Active Medications"));
    }

    [Test]
    public void Medications_RendersLookupBar()
    {
        var cut = Ctx.Render<Medications>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void Medications_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<Medications>();

        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task Medications_LoadsDataFromGrain()
    {
        var meds = new List<MedicationSummary>
        {
            new() { DrugName = "Lisinopril 10mg", Sig = "Take 1 tablet daily", Status = "Active",
                     FillDate = DateTime.UtcNow, RefillsRemaining = 3 },
            new() { DrugName = "Metformin 500mg", Sig = "Take 1 tablet twice daily", Status = "Active",
                     FillDate = DateTime.UtcNow, RefillsRemaining = 5 }
        };
        MockWorkflowGrain.GetActiveMedicationsAsync().Returns(meds);

        var cut = Ctx.Render<Medications>();

        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetActiveMedicationsAsync();

        Assert.That(cut.Markup, Does.Contain("Lisinopril 10mg"));
        Assert.That(cut.Markup, Does.Contain("Metformin 500mg"));
    }

    [Test]
    public async Task Medications_ShowsEmptyState()
    {
        MockWorkflowGrain.GetActiveMedicationsAsync().Returns(new List<MedicationSummary>());

        var cut = Ctx.Render<Medications>();

        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No active medications"));
    }

    [Test]
    public async Task Medications_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetActiveMedicationsAsync().Returns<List<MedicationSummary>>(
            _ => throw new Exception("Silo unreachable"));

        var cut = Ctx.Render<Medications>();

        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error loading medications"));
        Assert.That(cut.Markup, Does.Contain("Silo unreachable"));
    }
}
