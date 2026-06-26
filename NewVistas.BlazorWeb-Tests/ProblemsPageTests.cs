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
    public void Problems_PromptsToSelectPatientWhenNoneChosen()
    {
        var cut = Ctx.Render<Problems>();

        Assert.That(cut.Markup, Does.Contain("Select a patient"));
    }

    [Test]
    public void Problems_ShowsSelectedPatientInBar()
    {
        MockWorkflowGrain.GetActiveProblemsAsync().Returns(new List<ProblemSummary>());

        SelectPatient("PATIENT-001", "SMITH, JOHN");
        var cut = Ctx.Render<Problems>();

        Assert.That(cut.Markup, Does.Contain("Change patient"));
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

        SelectPatient("PATIENT-001");
        var cut = Ctx.Render<Problems>();

        // activeOnly defaults to true, so GetActiveProblemsAsync should be called
        await MockWorkflowGrain.Received(1).GetActiveProblemsAsync();
        await MockWorkflowGrain.DidNotReceive().GetAllProblemsAsync();

        Assert.That(cut.Markup, Does.Contain("Type 2 Diabetes"));
        Assert.That(cut.Markup, Does.Contain("E11.9"));
        Assert.That(cut.Markup, Does.Contain("Hypertension"));
        Assert.That(cut.Markup, Does.Contain("I10"));
    }

    [Test]
    public void Problems_ShowsEmptyStateWhenNoProblems()
    {
        MockWorkflowGrain.GetActiveProblemsAsync().Returns(new List<ProblemSummary>());

        SelectPatient("PATIENT-002");
        var cut = Ctx.Render<Problems>();

        Assert.That(cut.Markup, Does.Contain("No problems found"));
    }

    [Test]
    public void Problems_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetActiveProblemsAsync().Returns<List<ProblemSummary>>(
            _ => throw new Exception("Grain timeout"));

        SelectPatient("PATIENT-003");
        var cut = Ctx.Render<Problems>();

        Assert.That(cut.Markup, Does.Contain("Error loading problems"));
        Assert.That(cut.Markup, Does.Contain("Grain timeout"));
    }

    [Test]
    public void Problems_RendersTabs()
    {
        MockWorkflowGrain.GetActiveProblemsAsync().Returns(new List<ProblemSummary>());

        SelectPatient("PATIENT-001");
        var cut = Ctx.Render<Problems>();
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

        SelectPatient("PATIENT-001");
        var cut = Ctx.Render<Problems>();

        // Switch to Add Problem tab and wait for the form to appear
        cut.WaitForState(() => cut.FindAll("button.tab").Count > 1);
        cut.FindAll("button.tab")[1].Click();
        cut.WaitForState(() => cut.FindAll("input.form-input").Count > 0);

        // Fill out diagnosis (required field)
        cut.Find("input.form-input").Change("Low Back Pain");

        // Submit
        await cut.Find(".form-actions button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).AddProblemAsync(
            "Low Back Pain", Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<DateTime?>(),
            Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<bool>(), Arg.Any<string?>());
    }

    [Test]
    public void Problems_ServiceConnectedFlagDisplays()
    {
        var problems = new List<ProblemSummary>
        {
            new() { Diagnosis = "PTSD", DiagnosisCode = "F43.10", Status = "ACTIVE",
                     IsServiceConnected = true }
        };
        MockWorkflowGrain.GetActiveProblemsAsync().Returns(problems);

        SelectPatient("PATIENT-001");
        var cut = Ctx.Render<Problems>();

        Assert.That(cut.Markup, Does.Contain("PTSD"));
        Assert.That(cut.Markup, Does.Contain("Yes")); // SC column
    }
}
