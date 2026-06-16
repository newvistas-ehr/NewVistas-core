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
public class ProblemsPageTests : BlazorTestBase
{
    [Test]
    public void Problems_RendersPageTitle()
    {
        var cut = Ctx.Render<Problems>();

        Assert.That(cut.Markup, Does.Contain("Problem List"));
    }

    [Test]
    public void Problems_RendersLookupBar()
    {
        var cut = Ctx.Render<Problems>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void Problems_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<Problems>();

        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task Problems_LoadsActiveProblemsFromGrain()
    {
        var problems = new List<ProblemSummary>
        {
            new() { Diagnosis = "Type 2 Diabetes", DiagnosisCode = "E11.9", Status = "ACTIVE",
                     DateOfOnset = new DateTime(2020, 1, 15), Condition = "CHRONIC", IsServiceConnected = true },
            new() { Diagnosis = "Hypertension", DiagnosisCode = "I10", Status = "ACTIVE",
                     DateOfOnset = new DateTime(2019, 6, 1), Condition = "CHRONIC", IsServiceConnected = false }
        };
        MockWorkflowGrain.GetActiveProblemsAsync().Returns(problems);

        var cut = Ctx.Render<Problems>();

        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // activeOnly defaults to true, so GetActiveProblemsAsync should be called
        await MockWorkflowGrain.Received(1).GetActiveProblemsAsync();
        await MockWorkflowGrain.DidNotReceive().GetAllProblemsAsync();

        Assert.That(cut.Markup, Does.Contain("Type 2 Diabetes"));
        Assert.That(cut.Markup, Does.Contain("E11.9"));
        Assert.That(cut.Markup, Does.Contain("Hypertension"));
        Assert.That(cut.Markup, Does.Contain("I10"));
    }

    [Test]
    public async Task Problems_ShowsEmptyStateWhenNoProblems()
    {
        MockWorkflowGrain.GetActiveProblemsAsync().Returns(new List<ProblemSummary>());

        var cut = Ctx.Render<Problems>();

        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No problems found"));
    }

    [Test]
    public async Task Problems_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetActiveProblemsAsync().Returns<List<ProblemSummary>>(
            _ => throw new Exception("Grain timeout"));

        var cut = Ctx.Render<Problems>();

        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error loading problems"));
        Assert.That(cut.Markup, Does.Contain("Grain timeout"));
    }

    [Test]
    public async Task Problems_RendersTabs()
    {
        MockWorkflowGrain.GetActiveProblemsAsync().Returns(new List<ProblemSummary>());
        var cut = Ctx.Render<Problems>();

        var nav = Ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(nav.GetUriWithQueryParameter("patientId", "PATIENT-001"));
        cut.WaitForState(() => cut.FindAll("button.tab").Count > 0);

        var tabs = cut.FindAll("button.tab");
        Assert.That(tabs, Has.Count.EqualTo(2));
        Assert.That(tabs[0].TextContent, Does.Contain("Problem List"));
        Assert.That(tabs[1].TextContent, Does.Contain("Add Problem"));
    }

    [Test]
    public async Task Problems_SubmitCallsAddProblemAsync()
    {
        MockWorkflowGrain.GetActiveProblemsAsync().Returns(new List<ProblemSummary>());
        MockWorkflowGrain.AddProblemAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<DateTime?>(),
            Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<bool>(), Arg.Any<string?>()).Returns("PROBLEM-001");

        var cut = Ctx.Render<Problems>();

        // Load patient first
        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Switch to Add Problem tab and wait for form to appear
        cut.FindAll("button.tab")[1].Click();
        cut.WaitForState(() => cut.FindAll("input.form-input").Count > 0);

        // Fill out diagnosis (required field)
        cut.Find("input.form-input").Change("Low Back Pain");

        // Submit
        await cut.Find(".form-actions button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Verify grain was called
        await MockWorkflowGrain.Received(1).AddProblemAsync(
            "Low Back Pain", Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<DateTime?>(),
            Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<bool>(), Arg.Any<string?>());
    }

    [Test]
    public async Task Problems_ServiceConnectedFlagDisplays()
    {
        var problems = new List<ProblemSummary>
        {
            new() { Diagnosis = "PTSD", DiagnosisCode = "F43.10", Status = "ACTIVE",
                     IsServiceConnected = true }
        };
        MockWorkflowGrain.GetActiveProblemsAsync().Returns(problems);

        var cut = Ctx.Render<Problems>();

        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("PTSD"));
        Assert.That(cut.Markup, Does.Contain("Yes")); // SC column
    }
}
