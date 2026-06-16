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
public class MeansTestPageTests : BlazorTestBase
{
    [Test]
    public void MeansTest_RendersPageTitle()
    {
        var cut = Ctx.Render<MeansTest>();

        Assert.That(cut.Markup, Does.Contain("Means Test"));
    }

    [Test]
    public void MeansTest_RendersLookupBar()
    {
        var cut = Ctx.Render<MeansTest>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Does.Contain("Patient ID"));
    }

    [Test]
    public void MeansTest_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<MeansTest>();

        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task MeansTest_LoadsDataFromGrain()
    {
        var tests = new List<MeansTestSummary>
        {
            new() { MeansTestId = "MT-001", TestType = "MEANS TEST",
                     DateOfTest = new DateTime(2026, 1, 15), Status = "COMPLETED",
                     EligibilityStatus = "VERIFIED", PriorityGroup = "GROUP 5" }
        };
        MockWorkflowGrain.GetMeansTestsAsync().Returns(tests);

        var cut = Ctx.Render<MeansTest>();

        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetMeansTestsAsync();

        Assert.That(cut.Markup, Does.Contain("MEANS TEST"));
        Assert.That(cut.Markup, Does.Contain("01/15/2026"));
    }

    [Test]
    public async Task MeansTest_ShowsEmptyState()
    {
        MockWorkflowGrain.GetMeansTestsAsync().Returns(new List<MeansTestSummary>());

        var cut = Ctx.Render<MeansTest>();

        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No means tests found"));
    }

    [Test]
    public async Task MeansTest_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetMeansTestsAsync().Returns<List<MeansTestSummary>>(
            _ => throw new Exception("Timeout expired"));

        var cut = Ctx.Render<MeansTest>();

        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Timeout expired"));
    }
}
