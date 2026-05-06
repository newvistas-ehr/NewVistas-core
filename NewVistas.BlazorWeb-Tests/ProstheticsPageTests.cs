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
public class ProstheticsPageTests : BlazorTestBase
{
    [Test]
    public void Prosthetics_RendersPageTitle()
    {
        var cut = Ctx.Render<Prosthetics>();

        Assert.That(cut.Markup, Does.Contain("Prosthetics"));
    }

    [Test]
    public void Prosthetics_RendersLookupBar()
    {
        var cut = Ctx.Render<Prosthetics>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Does.Contain("Patient ID"));
    }

    [Test]
    public void Prosthetics_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<Prosthetics>();

        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task Prosthetics_LoadsDataFromGrain()
    {
        var items = new List<ProstheticsSummary>
        {
            new() { ProstheticsId = "PROSTH-001", ItemDescription = "Below-Knee Prosthesis",
                     HcpcsCode = "L5301", DateIssued = new DateTime(2026, 2, 1),
                     IsServiceConnected = true, Status = "ISSUED" }
        };
        MockWorkflowGrain.GetProstheticsAsync().Returns(items);

        var cut = Ctx.Render<Prosthetics>();

        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetProstheticsAsync();

        Assert.That(cut.Markup, Does.Contain("Below-Knee Prosthesis"));
        Assert.That(cut.Markup, Does.Contain("L5301"));
    }

    [Test]
    public async Task Prosthetics_ShowsEmptyState()
    {
        MockWorkflowGrain.GetProstheticsAsync().Returns(new List<ProstheticsSummary>());

        var cut = Ctx.Render<Prosthetics>();

        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No prosthetics items found"));
    }

    [Test]
    public async Task Prosthetics_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetProstheticsAsync().Returns<List<ProstheticsSummary>>(
            _ => throw new Exception("Network error"));

        var cut = Ctx.Render<Prosthetics>();

        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Network error"));
    }
}
