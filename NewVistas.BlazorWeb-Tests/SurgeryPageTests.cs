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
public class SurgeryPageTests : BlazorTestBase
{
    [Test]
    public void Surgery_RendersPageTitle()
    {
        var cut = Ctx.Render<Surgery>();

        Assert.That(cut.Markup, Does.Contain("Surgery"));
    }

    [Test]
    public void Surgery_RendersLookupBar()
    {
        var cut = Ctx.Render<Surgery>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void Surgery_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<Surgery>();

        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task Surgery_LoadsDataFromGrain()
    {
        var surgeries = new List<SurgerySummary>
        {
            new() { SurgeryId = "S-001", PrincipalProcedure = "Total Knee Replacement",
                     Status = "SCHEDULED", DateOfOperation = DateTime.UtcNow.AddDays(7),
                     SurgeonName = "Dr. Smith", SurgicalSpecialty = "Orthopedics" },
            new() { SurgeryId = "S-002", PrincipalProcedure = "Appendectomy",
                     Status = "COMPLETED", DateOfOperation = DateTime.UtcNow.AddDays(-3),
                     SurgeonName = "Dr. Jones", SurgicalSpecialty = "General Surgery" }
        };
        MockWorkflowGrain.GetSurgeriesAsync(100).Returns(surgeries);

        var cut = Ctx.Render<Surgery>();

        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetSurgeriesAsync(100);

        Assert.That(cut.Markup, Does.Contain("Total Knee Replacement"));
        Assert.That(cut.Markup, Does.Contain("Appendectomy"));
    }

    [Test]
    public async Task Surgery_ShowsEmptyState()
    {
        MockWorkflowGrain.GetSurgeriesAsync(100).Returns(new List<SurgerySummary>());

        var cut = Ctx.Render<Surgery>();

        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No surgeries found"));
    }

    [Test]
    public async Task Surgery_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetSurgeriesAsync(100).Returns<List<SurgerySummary>>(
            _ => throw new Exception("Silo unreachable"));

        var cut = Ctx.Render<Surgery>();

        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Silo unreachable"));
    }
}
