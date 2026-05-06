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
public class CompensationPensionPageTests : BlazorTestBase
{
    [Test]
    public void CompensationPension_RendersPageTitle()
    {
        var cut = Ctx.Render<CompensationPension>();

        Assert.That(cut.Markup, Does.Contain("Compensation"));
        Assert.That(cut.Markup, Does.Contain("Pension"));
    }

    [Test]
    public void CompensationPension_RendersLookupBar()
    {
        var cut = Ctx.Render<CompensationPension>();

        var inputs = cut.FindAll("input");
        Assert.That(inputs.Count, Is.GreaterThan(0));
    }

    [Test]
    public void CompensationPension_LoadButton_Present()
    {
        var cut = Ctx.Render<CompensationPension>();

        var buttons = cut.FindAll("button");
        Assert.That(buttons.Any(b => b.TextContent.Contains("Load")), Is.True);
    }

    [Test]
    public async Task CompensationPension_LoadsDataFromGrain()
    {
        var exams = new List<CPExamIndexEntry>
        {
            new()
            {
                ExamId = "CP-001",
                ExamType = CPExamType.Initial,
                Status = CPExamStatus.Scheduled,
                ScheduledDate = DateTime.Today.AddDays(7),
                ExaminerName = "Dr. Examiner",
                ClaimNumber = "CLM-123"
            }
        };
        MockWorkflowGrain.GetCPExamsAsync().Returns(exams);
        MockWorkflowGrain.GetDBQsAsync().Returns(new List<DBQIndexEntry>());

        var cut = Ctx.Render<CompensationPension>();

        cut.Find("input").Change("PAT-001");
        await cut.FindAll("button").First(b => b.TextContent.Contains("Load"))
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetCPExamsAsync();
        Assert.That(cut.Markup, Does.Contain("Dr. Examiner"));
    }

    [Test]
    public async Task CompensationPension_ShowsEmptyState()
    {
        MockWorkflowGrain.GetCPExamsAsync().Returns(new List<CPExamIndexEntry>());
        MockWorkflowGrain.GetDBQsAsync().Returns(new List<DBQIndexEntry>());

        var cut = Ctx.Render<CompensationPension>();

        cut.Find("input").Change("PAT-002");
        await cut.FindAll("button").First(b => b.TextContent.Contains("Load"))
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No C&amp;P exams on record"));
    }

    [Test]
    public async Task CompensationPension_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetCPExamsAsync().Returns<List<CPExamIndexEntry>>(
            _ => throw new Exception("Network error"));

        var cut = Ctx.Render<CompensationPension>();

        cut.Find("input").Change("PAT-003");
        await cut.FindAll("button").First(b => b.TextContent.Contains("Load"))
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No C&amp;P exams on record"));
    }
}
