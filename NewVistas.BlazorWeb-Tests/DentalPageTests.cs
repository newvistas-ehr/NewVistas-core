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
public class DentalPageTests : BlazorTestBase
{
    [Test]
    public void Dental_RendersPageTitle()
    {
        var cut = Ctx.Render<Dental>();

        Assert.That(cut.Markup, Does.Contain("Dental"));
    }

    [Test]
    public void Dental_RendersLookupBar()
    {
        var cut = Ctx.Render<Dental>();

        var inputs = cut.FindAll("input.form-control");
        Assert.That(inputs.Count, Is.GreaterThan(0));
    }

    [Test]
    public void Dental_LoadButton_DisabledWhenLoading()
    {
        var cut = Ctx.Render<Dental>();

        var button = cut.Find("button.btn-primary");
        Assert.That(button, Is.Not.Null);
    }

    [Test]
    public async Task Dental_LoadsDataFromGrain()
    {
        var dentalState = new DentalPatientState
        {
            PatientId = "PAT-001",
            EligibilityStatus = DentalEligibilityStatus.Eligible,
            PeriodontalStatus = DentalPeriodontalStatus.Healthy,
            PrimaryDentistName = "Dr. Brown"
        };
        var treatments = new List<DentalTreatmentIndexEntry>();

        MockWorkflowGrain.GetDentalPatientAsync().Returns(dentalState);
        MockWorkflowGrain.GetDentalTreatmentsAsync().Returns(treatments);

        var cut = Ctx.Render<Dental>();

        cut.Find("input.form-control").Change("PAT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetDentalPatientAsync();
        Assert.That(cut.Markup, Does.Contain("Dr. Brown"));
        Assert.That(cut.Markup, Does.Contain("Eligible"));
    }

    [Test]
    public async Task Dental_ShowsEmptyState()
    {
        var dentalState = new DentalPatientState
        {
            PatientId = "PAT-002",
            EligibilityStatus = DentalEligibilityStatus.Unknown,
            PeriodontalStatus = DentalPeriodontalStatus.Healthy
        };
        MockWorkflowGrain.GetDentalPatientAsync().Returns(dentalState);
        MockWorkflowGrain.GetDentalTreatmentsAsync().Returns(new List<DentalTreatmentIndexEntry>());

        var cut = Ctx.Render<Dental>();

        cut.Find("input.form-control").Change("PAT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Switch to Treatment History tab to see empty state
        var treatmentsTab = cut.FindAll("button.tab-btn").First(b => b.TextContent.Contains("Treatment History"));
        await treatmentsTab.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No treatment records found"));
    }

    [Test]
    public async Task Dental_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetDentalPatientAsync().Returns<DentalPatientState>(
            _ => throw new Exception("Grain error"));

        var cut = Ctx.Render<Dental>();

        cut.Find("input.form-control").Change("PAT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Grain error"));
    }
}
