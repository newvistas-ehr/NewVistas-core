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
public class DieteticsPageTests : BlazorTestBase
{
    [Test]
    public void Dietetics_RendersPageTitle()
    {
        var cut = Ctx.Render<Dietetics>();
        Assert.That(cut.Markup, Does.Contain("Dietetics"));
    }

    [Test]
    public void Dietetics_RendersLookupBar()
    {
        var cut = Ctx.Render<Dietetics>();
        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void Dietetics_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<Dietetics>();
        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task Dietetics_LoadsDataFromGrain()
    {
        var orders = new List<DieteticsSummary>
        {
            new() { DietOrderId = "DIET-001", DietType = "CARDIAC",
                     Status = "ACTIVE", StartDateTime = DateTime.Today }
        };
        MockWorkflowGrain.GetDietOrdersAsync().Returns(orders);

        var cut = Ctx.Render<Dietetics>();
        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetDietOrdersAsync();
        Assert.That(cut.Markup, Does.Contain("CARDIAC"));
        Assert.That(cut.Markup, Does.Contain("ACTIVE"));
    }

    [Test]
    public async Task Dietetics_ShowsEmptyState()
    {
        MockWorkflowGrain.GetDietOrdersAsync().Returns(new List<DieteticsSummary>());

        var cut = Ctx.Render<Dietetics>();
        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No diet orders found"));
    }

    [Test]
    public async Task Dietetics_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetDietOrdersAsync().Returns<List<DieteticsSummary>>(
            _ => throw new Exception("Storage unavailable"));

        var cut = Ctx.Render<Dietetics>();
        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error"));
        Assert.That(cut.Markup, Does.Contain("Storage unavailable"));
    }
}
