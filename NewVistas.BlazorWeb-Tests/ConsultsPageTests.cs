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
public class ConsultsPageTests : BlazorTestBase
{
    [Test]
    public void Consults_RendersPageTitle()
    {
        var cut = Ctx.Render<Consults>();

        Assert.That(cut.Markup, Does.Contain("Consults"));
    }

    [Test]
    public void Consults_RendersLookupBar()
    {
        var cut = Ctx.Render<Consults>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void Consults_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<Consults>();

        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task Consults_LoadsDataFromGrain()
    {
        var consults = new List<ConsultSummary>
        {
            new() { ConsultId = "C-001", ToService = "Cardiology", Status = "PENDING",
                     Urgency = "ROUTINE", RequestDateTime = DateTime.UtcNow },
            new() { ConsultId = "C-002", ToService = "Orthopedics", Status = "ACTIVE",
                     Urgency = "URGENT", RequestDateTime = DateTime.UtcNow }
        };
        MockWorkflowGrain.GetConsultsAsync(null, 100).Returns(consults);

        var cut = Ctx.Render<Consults>();

        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetConsultsAsync(null, 100);

        Assert.That(cut.Markup, Does.Contain("Cardiology"));
        Assert.That(cut.Markup, Does.Contain("Orthopedics"));
    }

    [Test]
    public async Task Consults_ShowsEmptyState()
    {
        MockWorkflowGrain.GetConsultsAsync(null, 100).Returns(new List<ConsultSummary>());

        var cut = Ctx.Render<Consults>();

        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No consults found"));
    }

    [Test]
    public async Task Consults_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetConsultsAsync(null, 100).Returns<List<ConsultSummary>>(
            _ => throw new Exception("Silo unreachable"));

        var cut = Ctx.Render<Consults>();

        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error loading consults"));
        Assert.That(cut.Markup, Does.Contain("Silo unreachable"));
    }
}
