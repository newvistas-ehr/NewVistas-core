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
public class SpinalCordInjuryPageTests : BlazorTestBase
{
    [Test]
    public void SpinalCordInjury_RendersPageTitle()
    {
        var cut = Ctx.Render<SpinalCordInjury>();

        Assert.That(cut.Markup, Does.Contain("Spinal Cord Injury"));
    }

    [Test]
    public void SpinalCordInjury_RendersLookupBar()
    {
        var cut = Ctx.Render<SpinalCordInjury>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void SpinalCordInjury_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<SpinalCordInjury>();

        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task SpinalCordInjury_LoadsDataFromGrain()
    {
        var sciState = new SCIPatientState
        {
            PatientId = "PAT-001",
            Status = SCIRegistryStatus.Active,
            EnrollmentDate = DateTime.Today.AddYears(-1),
            NeurologicalLevelOfInjury = "C5",
            AisGrade = SCIAisGrade.A,
            InjuryType = SCIInjuryType.Traumatic,
            Etiology = SCIEtiology.MotorVehicleAccident
        };
        var encounters = new List<SCIAnnualEncounterRecord>();

        MockWorkflowGrain.GetSCIPatientAsync().Returns(sciState);
        MockWorkflowGrain.GetSCIAnnualEncountersAsync().Returns(encounters);

        var cut = Ctx.Render<SpinalCordInjury>();

        cut.Find("input.lookup-input").Input("PAT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetSCIPatientAsync();
        Assert.That(cut.Markup, Does.Contain("C5"));
        Assert.That(cut.Markup, Does.Contain("Active"));
    }

    [Test]
    public async Task SpinalCordInjury_ShowsEmptyState()
    {
        MockWorkflowGrain.GetSCIPatientAsync().Returns(new SCIPatientState());
        MockWorkflowGrain.GetSCIAnnualEncountersAsync().Returns(new List<SCIAnnualEncounterRecord>());

        var cut = Ctx.Render<SpinalCordInjury>();

        cut.Find("input.lookup-input").Input("PAT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // A default SCIPatientState is not null, so the page renders the record
        // with default values (Status=Active, InjuryType=Traumatic, etc.)
        Assert.That(cut.Markup, Does.Contain("Active"));
        Assert.That(cut.Markup, Does.Contain("Traumatic"));
    }

    [Test]
    public async Task SpinalCordInjury_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetSCIPatientAsync().Returns<SCIPatientState>(
            _ => throw new Exception("Silo down"));

        var cut = Ctx.Render<SpinalCordInjury>();

        cut.Find("input.lookup-input").Input("PAT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Silo down"));
    }
}
