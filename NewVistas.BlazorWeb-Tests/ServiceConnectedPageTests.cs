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
public class ServiceConnectedPageTests : BlazorTestBase
{
    [Test]
    public void ServiceConnected_RendersPageTitle()
    {
        var cut = Ctx.Render<ServiceConnected>();

        Assert.That(cut.Markup, Does.Contain("Service Connected"));
    }

    [Test]
    public void ServiceConnected_RendersLookupBar()
    {
        var cut = Ctx.Render<ServiceConnected>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Does.Contain("Patient ID"));
    }

    [Test]
    public void ServiceConnected_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<ServiceConnected>();

        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task ServiceConnected_LoadsDataFromGrain()
    {
        var conditions = new List<ServiceConnectedSummary>
        {
            new() { ConditionId = "SC-001", Condition = "Hearing Loss", DiagnosisCode = "H90.3",
                     DisabilityPercentage = 40, IsServiceConnected = true, Status = "ACTIVE" }
        };
        MockWorkflowGrain.GetServiceConnectedConditionsAsync().Returns(conditions);

        var cut = Ctx.Render<ServiceConnected>();

        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetServiceConnectedConditionsAsync();

        Assert.That(cut.Markup, Does.Contain("Hearing Loss"));
        Assert.That(cut.Markup, Does.Contain("H90.3"));
        Assert.That(cut.Markup, Does.Contain("40%"));
    }

    [Test]
    public async Task ServiceConnected_ShowsEmptyState()
    {
        MockWorkflowGrain.GetServiceConnectedConditionsAsync().Returns(new List<ServiceConnectedSummary>());

        var cut = Ctx.Render<ServiceConnected>();

        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No service connected conditions found"));
    }

    [Test]
    public async Task ServiceConnected_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetServiceConnectedConditionsAsync().Returns<List<ServiceConnectedSummary>>(
            _ => throw new Exception("Grain unavailable"));

        var cut = Ctx.Render<ServiceConnected>();

        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Grain unavailable"));
    }
}
