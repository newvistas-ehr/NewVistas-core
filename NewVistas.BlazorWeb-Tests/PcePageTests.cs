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
public class PcePageTests : BlazorTestBase
{
    [Test]
    public void Pce_RendersPageTitle()
    {
        var cut = Ctx.Render<Pce>();
        Assert.That(cut.Markup, Does.Contain("Patient Care Encounter"));
    }

    [Test]
    public void Pce_RendersToolbar()
    {
        var cut = Ctx.Render<Pce>();
        var input = cut.Find("input.input-id");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void Pce_LoadButton_Present()
    {
        var cut = Ctx.Render<Pce>();
        var buttons = cut.FindAll("button");
        Assert.That(buttons.Any(b => b.TextContent.Contains("Load Encounters")), Is.True);
    }

    [Test]
    public async Task Pce_LoadsDataFromGrain()
    {
        var visits = new List<PceVisitEntry>
        {
            new() { VisitId = "V-001", VisitDateTime = DateTime.Today,
                     ServiceCategory = "A", LocationName = "Primary Care",
                     PrimaryProviderName = "Dr. Smith", Status = "OPEN",
                     DiagnosisCount = 1, ProcedureCount = 0 }
        };
        MockWorkflowGrain.GetEncounterListAsync(Arg.Any<int>()).Returns(visits);

        var cut = Ctx.Render<Pce>();
        cut.Find("input.input-id").Change("PATIENT-001");
        await cut.FindAll("button").First(b => b.TextContent.Contains("Load Encounters")).ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetEncounterListAsync(Arg.Any<int>());
        Assert.That(cut.Markup, Does.Contain("Primary Care"));
        Assert.That(cut.Markup, Does.Contain("Dr. Smith"));
        Assert.That(cut.Markup, Does.Contain("OPEN"));
    }

    [Test]
    public async Task Pce_ShowsEmptyState()
    {
        MockWorkflowGrain.GetEncounterListAsync(Arg.Any<int>()).Returns(new List<PceVisitEntry>());

        var cut = Ctx.Render<Pce>();
        cut.Find("input.input-id").Change("PATIENT-002");
        await cut.FindAll("button").First(b => b.TextContent.Contains("Load Encounters")).ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No encounters recorded"));
    }

    [Test]
    public async Task Pce_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetEncounterListAsync(Arg.Any<int>()).Returns<List<PceVisitEntry>>(
            _ => throw new Exception("Visit grain error"));

        var cut = Ctx.Render<Pce>();
        cut.Find("input.input-id").Change("PATIENT-003");
        await cut.FindAll("button").First(b => b.TextContent.Contains("Load Encounters")).ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error"));
        Assert.That(cut.Markup, Does.Contain("Visit grain error"));
    }
}
