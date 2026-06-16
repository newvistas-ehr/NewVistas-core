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
public class ImmunizationsPageTests : BlazorTestBase
{
    [Test]
    public void Immunizations_RendersPageTitle()
    {
        var cut = Ctx.Render<Immunizations>();
        Assert.That(cut.Markup, Does.Contain("Immunizations"));
    }

    [Test]
    public void Immunizations_RendersLookupBar()
    {
        var cut = Ctx.Render<Immunizations>();
        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void Immunizations_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<Immunizations>();
        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task Immunizations_LoadsDataFromGrain()
    {
        var immunizations = new List<ImmunizationSummary>
        {
            new() { ImmunizationId = "IMM-001", ImmunizationName = "Influenza Vaccine",
                     CvxCode = "158", EventDateTime = DateTime.Today, Series = "1",
                     AdministeredByName = "Nurse Smith" }
        };
        MockWorkflowGrain.GetImmunizationsAsync().Returns(immunizations);

        var cut = Ctx.Render<Immunizations>();
        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetImmunizationsAsync();
        Assert.That(cut.Markup, Does.Contain("Influenza Vaccine"));
        Assert.That(cut.Markup, Does.Contain("158"));
    }

    [Test]
    public async Task Immunizations_ShowsEmptyState()
    {
        MockWorkflowGrain.GetImmunizationsAsync().Returns(new List<ImmunizationSummary>());

        var cut = Ctx.Render<Immunizations>();
        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No immunizations found"));
    }

    [Test]
    public async Task Immunizations_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetImmunizationsAsync().Returns<List<ImmunizationSummary>>(
            _ => throw new Exception("Network error"));

        var cut = Ctx.Render<Immunizations>();
        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error"));
        Assert.That(cut.Markup, Does.Contain("Network error"));
    }
}
