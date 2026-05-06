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
public class RemindersPageTests : BlazorTestBase
{
    [Test]
    public void Reminders_RendersPageTitle()
    {
        var cut = Ctx.Render<Reminders>();
        Assert.That(cut.Markup, Does.Contain("Clinical Reminders"));
    }

    [Test]
    public void Reminders_RendersLookupBar()
    {
        var cut = Ctx.Render<Reminders>();
        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void Reminders_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<Reminders>();
        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task Reminders_LoadsDataFromGrain()
    {
        var reminders = new List<ReminderSummary>
        {
            new() { ReminderId = "REM-001", ReminderName = "Influenza Vaccine",
                     Status = "DUE", DueDate = DateTime.Today.AddDays(7) }
        };
        MockWorkflowGrain.GetRemindersAsync().Returns(reminders);

        var cut = Ctx.Render<Reminders>();
        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetRemindersAsync();
        Assert.That(cut.Markup, Does.Contain("Influenza Vaccine"));
        Assert.That(cut.Markup, Does.Contain("DUE"));
    }

    [Test]
    public async Task Reminders_ShowsEmptyState()
    {
        MockWorkflowGrain.GetRemindersAsync().Returns(new List<ReminderSummary>());

        var cut = Ctx.Render<Reminders>();
        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No reminders found"));
    }

    [Test]
    public async Task Reminders_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetRemindersAsync().Returns<List<ReminderSummary>>(
            _ => throw new Exception("Grain timeout"));

        var cut = Ctx.Render<Reminders>();
        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error"));
        Assert.That(cut.Markup, Does.Contain("Grain timeout"));
    }
}
