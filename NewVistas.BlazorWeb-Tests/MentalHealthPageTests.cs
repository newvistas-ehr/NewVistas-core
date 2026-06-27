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
public class MentalHealthPageTests : BlazorTestBase
{
    [Test]
    public void MentalHealth_RendersPageTitle()
    {
        var cut = Ctx.Render<MentalHealth>();
        Assert.That(cut.Markup, Does.Contain("Mental Health Screening"));
    }

    [Test]
    public void MentalHealth_RendersLookupBar()
    {
        var cut = Ctx.Render<MentalHealth>();
        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void MentalHealth_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<MentalHealth>();
        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task MentalHealth_LoadsDataFromGrain()
    {
        var screens = new List<MentalHealthSummary>
        {
            new() { InstrumentId = "MH-001", InstrumentName = "PHQ-9",
                     AdministrationDateTime = DateTime.Today, TotalScore = 12,
                     ScoreInterpretation = "MODERATE", IsPositiveScreen = true, Status = "COMPLETED" }
        };
        MockWorkflowGrain.GetMentalHealthScreensAsync().Returns(screens);

        var cut = Ctx.Render<MentalHealth>();
        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetMentalHealthScreensAsync();
        Assert.That(cut.Markup, Does.Contain("PHQ-9"));
        Assert.That(cut.Markup, Does.Contain("MODERATE"));
    }

    [Test]
    public async Task MentalHealth_ShowsEmptyState()
    {
        MockWorkflowGrain.GetMentalHealthScreensAsync().Returns(new List<MentalHealthSummary>());

        var cut = Ctx.Render<MentalHealth>();
        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No mental health screens found"));
    }

    [Test]
    public async Task MentalHealth_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetMentalHealthScreensAsync().Returns<List<MentalHealthSummary>>(
            _ => throw new Exception("Silo unreachable"));

        var cut = Ctx.Render<MentalHealth>();
        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error"));
        Assert.That(cut.Markup, Does.Contain("Silo unreachable"));
    }

    [Test]
    public async Task MentalHealth_WhenContextKnowsUserLacksKey_ShowsNotice_AndSkipsGrain()
    {
        // Security context loaded for a user with NO keys.
        var acl = Substitute.For<IAccessControlGrain>();
        acl.GetKeysAsync().Returns((IReadOnlySet<string>)new HashSet<string>());
        MockGrainFactory.GetGrain<IAccessControlGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(acl);
        await SecurityContext.InitializeAsync(GrainService, "USER-1");

        SelectPatient("PATIENT-004"); // triggers auto-load via OnParametersSetAsync
        var cut = Ctx.Render<MentalHealth>();

        Assert.That(cut.Markup, Does.Contain("requires additional access"));
        Assert.That(cut.Markup, Does.Contain("YS MH INSTRUMENT"));
        // The page must NOT call the gated grain when we already know the key is missing.
        await MockWorkflowGrain.DidNotReceive().GetMentalHealthScreensAsync();
    }

    [Test]
    public async Task MentalHealth_WhenGrainThrowsUnauthorized_ShowsNotice_NotRawError()
    {
        // Fallback path: key context not loaded, the gated grain enforces and throws.
        MockWorkflowGrain.GetMentalHealthScreensAsync().Returns<List<MentalHealthSummary>>(
            _ => throw new UnauthorizedAccessException("Access denied: requires any of [YS MH INSTRUMENT]"));

        var cut = Ctx.Render<MentalHealth>();
        cut.Find("input.lookup-input").Input("PATIENT-005");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("requires additional access"));
        Assert.That(cut.Markup, Does.Not.Contain("Access denied:")); // raw exception text not shown
    }
}
