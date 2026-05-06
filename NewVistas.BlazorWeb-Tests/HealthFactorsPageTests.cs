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
public class HealthFactorsPageTests : BlazorTestBase
{
    [Test]
    public void HealthFactors_RendersPageTitle()
    {
        var cut = Ctx.Render<HealthFactors>();
        Assert.That(cut.Markup, Does.Contain("Health Factors"));
    }

    [Test]
    public void HealthFactors_RendersLookupBar()
    {
        var cut = Ctx.Render<HealthFactors>();
        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void HealthFactors_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<HealthFactors>();
        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task HealthFactors_LoadsDataFromGrain()
    {
        var factors = new List<HealthFactorSummary>
        {
            new() { HealthFactorId = "HF-001", HealthFactorName = "CURRENT SMOKER",
                     Category = "TOBACCO", EventDateTime = DateTime.Today, LevelSeverity = "HEAVY/SEVERE" }
        };
        MockWorkflowGrain.GetHealthFactorsAsync().Returns(factors);

        var cut = Ctx.Render<HealthFactors>();
        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetHealthFactorsAsync();
        Assert.That(cut.Markup, Does.Contain("CURRENT SMOKER"));
        Assert.That(cut.Markup, Does.Contain("TOBACCO"));
    }

    [Test]
    public async Task HealthFactors_ShowsEmptyState()
    {
        MockWorkflowGrain.GetHealthFactorsAsync().Returns(new List<HealthFactorSummary>());

        var cut = Ctx.Render<HealthFactors>();
        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No health factors found"));
    }

    [Test]
    public async Task HealthFactors_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetHealthFactorsAsync().Returns<List<HealthFactorSummary>>(
            _ => throw new Exception("Connection refused"));

        var cut = Ctx.Render<HealthFactors>();
        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error"));
        Assert.That(cut.Markup, Does.Contain("Connection refused"));
    }
}
