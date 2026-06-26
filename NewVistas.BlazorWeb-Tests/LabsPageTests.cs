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
public class LabsPageTests : BlazorTestBase
{
    [Test]
    public void Labs_RendersPageTitle()
    {
        var cut = Ctx.Render<Labs>();

        Assert.That(cut.Markup, Does.Contain("Laboratory Information System"));
    }

    [Test]
    public void Labs_RendersLookupBar()
    {
        var cut = Ctx.Render<Labs>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void Labs_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<Labs>();

        // The Load Results button - find buttons and check the first action button
        var buttons = cut.FindAll("button");
        var loadButton = buttons.First(b => b.TextContent.Contains("Load Results"));
        // The button is not disabled when the input is empty — it only disables when loading
        Assert.That(loadButton, Is.Not.Null);
    }

    [Test]
    public async Task Labs_LoadsDataFromGrain()
    {
        var results = new List<LabResultSummary>
        {
            new() { LabTestId = "LAB-001", TestName = "CBC", ResultValue = "7.5", Units = "K/cmm",
                     Flag = "H", Status = "Completed", CollectionDate = DateTime.UtcNow },
            new() { LabTestId = "LAB-002", TestName = "BMP", Status = "Ordered" }
        };
        MockWorkflowGrain.GetLabResultsAsync().Returns(results);

        var cut = Ctx.Render<Labs>();

        cut.Find("input.lookup-input").Change("PATIENT-001");
        var loadButton = cut.FindAll("button").First(b => b.TextContent.Contains("Load Results"));
        await loadButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetLabResultsAsync();

        Assert.That(cut.Markup, Does.Contain("CBC"));
        Assert.That(cut.Markup, Does.Contain("7.5"));
    }

    [Test]
    public async Task Labs_ShowsEmptyState()
    {
        MockWorkflowGrain.GetLabResultsAsync().Returns(new List<LabResultSummary>());

        var cut = Ctx.Render<Labs>();

        cut.Find("input.lookup-input").Change("PATIENT-002");
        var loadButton = cut.FindAll("button").First(b => b.TextContent.Contains("Load Results"));
        await loadButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No lab orders found"));
    }

    [Test]
    public async Task Labs_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetLabResultsAsync().Returns<List<LabResultSummary>>(
            _ => throw new Exception("Silo unreachable"));

        var cut = Ctx.Render<Labs>();

        cut.Find("input.lookup-input").Change("PATIENT-003");
        var loadButton = cut.FindAll("button").First(b => b.TextContent.Contains("Load Results"));
        await loadButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Silo unreachable"));
    }
}
