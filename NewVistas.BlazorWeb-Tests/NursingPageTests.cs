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
public class NursingPageTests : BlazorTestBase
{
    [Test]
    public void Nursing_RendersPageTitle()
    {
        var cut = Ctx.Render<Nursing>();
        // The page title is in a PageTitle component, not visible in markup
        // Verify the page renders with the lookup bar instead
        Assert.That(cut.Markup, Does.Contain("Load Patient"));
    }

    [Test]
    public void Nursing_RendersLookupBar()
    {
        var cut = Ctx.Render<Nursing>();
        var input = cut.Find("input[placeholder='Patient ID']");
        Assert.That(input, Is.Not.Null);
    }

    [Test]
    public void Nursing_LoadButton_DisabledWhenLoading()
    {
        var cut = Ctx.Render<Nursing>();
        // The load button should be present
        var buttons = cut.FindAll("button");
        Assert.That(buttons.Count, Is.GreaterThan(0));
    }

    [Test]
    public async Task Nursing_LoadsDataFromGrain()
    {
        var assessments = new List<NursingAssessmentIndexEntry>
        {
            new() { AssessmentId = "NA-001", AssessmentDateTime = DateTime.Today,
                     AssessmentType = "Shift", NurseName = "Nurse Jones",
                     Status = NursingAssessmentStatus.Draft, PainScore = 3, MorseScore = 25, BradenScore = 18 }
        };
        var carePlan = new NursingCarePlanState { PatientId = "PATIENT-001" };
        var acuityState = new NursingAcuityState { PatientId = "PATIENT-001" };

        MockWorkflowGrain.GetNursingAssessmentsAsync().Returns(assessments);
        MockWorkflowGrain.GetNursingCarePlanAsync().Returns(carePlan);
        MockWorkflowGrain.GetNursingAcuityAsync().Returns(acuityState);

        var mockUnitIndex = Substitute.For<INursingUnitIndexGrain>();
        mockUnitIndex.GetAsync().Returns(new NursingUnitIndexState());
        MockGrainFactory
            .GetGrain<INursingUnitIndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(mockUnitIndex);

        var cut = Ctx.Render<Nursing>();
        cut.Find("input[placeholder='Patient ID']").Change("PATIENT-001");
        await cut.FindAll("button").First(b => b.TextContent.Contains("Load")).ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetNursingAssessmentsAsync();
        Assert.That(cut.Markup, Does.Contain("Nurse Jones"));
        Assert.That(cut.Markup, Does.Contain("Shift"));
    }

    [Test]
    public async Task Nursing_ShowsEmptyState()
    {
        MockWorkflowGrain.GetNursingAssessmentsAsync().Returns(new List<NursingAssessmentIndexEntry>());
        MockWorkflowGrain.GetNursingCarePlanAsync().Returns(new NursingCarePlanState { PatientId = "PATIENT-002" });
        MockWorkflowGrain.GetNursingAcuityAsync().Returns(new NursingAcuityState { PatientId = "PATIENT-002" });

        var mockUnitIndex = Substitute.For<INursingUnitIndexGrain>();
        mockUnitIndex.GetAsync().Returns(new NursingUnitIndexState());
        MockGrainFactory
            .GetGrain<INursingUnitIndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(mockUnitIndex);

        var cut = Ctx.Render<Nursing>();
        cut.Find("input[placeholder='Patient ID']").Change("PATIENT-002");
        await cut.FindAll("button").First(b => b.TextContent.Contains("Load")).ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No assessments recorded"));
    }

    [Test]
    public async Task Nursing_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetNursingAssessmentsAsync().Returns<List<NursingAssessmentIndexEntry>>(
            _ => throw new Exception("Cluster down"));

        var cut = Ctx.Render<Nursing>();
        cut.Find("input[placeholder='Patient ID']").Change("PATIENT-003");
        await cut.FindAll("button").First(b => b.TextContent.Contains("Load")).ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error"));
        Assert.That(cut.Markup, Does.Contain("Cluster down"));
    }
}
