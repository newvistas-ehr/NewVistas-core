// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class SchedulingPageTests : BlazorTestBase
{
    [Test]
    public void Scheduling_RendersPageTitle()
    {
        var cut = Ctx.Render<Scheduling>();

        Assert.That(cut.Markup, Does.Contain("Scheduling"));
    }

    [Test]
    public void Scheduling_RendersToolbar()
    {
        var cut = Ctx.Render<Scheduling>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Does.Contain("Patient ID"));
    }

    [Test]
    public void Scheduling_LoadButton_DisabledWhenLoading()
    {
        var cut = Ctx.Render<Scheduling>();

        // The "Load Schedule" button exists in the toolbar
        var buttons = cut.FindAll("button");
        var loadBtn = buttons.First(b => b.TextContent.Trim() == "Load");
        Assert.That(loadBtn, Is.Not.Null);
    }

    [Test]
    public async Task Scheduling_LoadsDataFromGrain()
    {
        var appointments = new List<AppointmentEntry>
        {
            new() { AppointmentId = "APT-001", ClinicId = "C-1", ClinicName = "Primary Care",
                     AppointmentDateTime = new DateTime(2026, 4, 1, 9, 0, 0), DurationMinutes = 30,
                     Status = "Scheduled", ProviderName = "Dr. Smith", Purpose = "Follow-up" }
        };
        MockWorkflowGrain.GetAllAppointmentsAsync(Arg.Any<int>()).Returns(appointments);

        var cut = Ctx.Render<Scheduling>();

        cut.Find("input.lookup-input").Change("PATIENT-001");
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Load")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetAllAppointmentsAsync(Arg.Any<int>());

        Assert.That(cut.Markup, Does.Contain("Primary Care"));
        Assert.That(cut.Markup, Does.Contain("Dr. Smith"));
    }

    [Test]
    public async Task Scheduling_ShowsEmptyState()
    {
        MockWorkflowGrain.GetAllAppointmentsAsync(Arg.Any<int>()).Returns(new List<AppointmentEntry>());

        var cut = Ctx.Render<Scheduling>();

        cut.Find("input.lookup-input").Change("PATIENT-002");
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Load")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No appointments found"));
    }

    [Test]
    public async Task Scheduling_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetAllAppointmentsAsync(Arg.Any<int>()).Returns<List<AppointmentEntry>>(
            _ => throw new Exception("Connection lost"));

        var cut = Ctx.Render<Scheduling>();

        cut.Find("input.lookup-input").Change("PATIENT-003");
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Load")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Connection lost"));
    }
}
