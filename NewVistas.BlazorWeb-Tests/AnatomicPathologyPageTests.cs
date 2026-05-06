// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class AnatomicPathologyPageTests : BlazorTestBase
{
    [Test]
    public void AnatomicPathology_RendersPageTitle()
    {
        var cut = Ctx.Render<AnatomicPathology>();

        Assert.That(cut.Markup, Does.Contain("Anatomic Pathology (AP)"));
    }

    [Test]
    public void AnatomicPathology_RendersTabButtons()
    {
        var cut = Ctx.Render<AnatomicPathology>();

        Assert.That(cut.Markup, Does.Contain("Cases"));
        Assert.That(cut.Markup, Does.Contain("Surgical Path"));
        Assert.That(cut.Markup, Does.Contain("Cytology"));
        Assert.That(cut.Markup, Does.Contain("Autopsy"));
        Assert.That(cut.Markup, Does.Contain("Accession"));
    }

    [Test]
    public void AnatomicPathology_LoadButton_Present()
    {
        var cut = Ctx.Render<AnatomicPathology>();

        var buttons = cut.FindAll("button");
        Assert.That(buttons.Any(b => b.TextContent.Contains("Load Cases")), Is.True);
    }

    [Test]
    public async Task AnatomicPathology_LoadsCasesFromGrain()
    {
        var entries = new List<APCaseIndexEntry>
        {
            new()
            {
                CaseId = "AP-001",
                AccessionNumber = "SP-2024-00001",
                CaseType = APCaseType.SurgicalPathology,
                Status = APCaseStatus.Final,
                DateReceived = new DateTime(2024, 6, 15),
                SpecimenSource = "Right lung",
                PrimaryDiagnosis = "Adenocarcinoma",
                PathologistName = "Dr. Path"
            }
        };
        MockWorkflowGrain.GetAPCasesAsync().Returns(entries);

        var cut = Ctx.Render<AnatomicPathology>();

        cut.Find("input").Change("PAT-001");
        await cut.FindAll("button").First(b => b.TextContent.Contains("Load Cases"))
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetAPCasesAsync();
        Assert.That(cut.Markup, Does.Contain("SP-2024-00001"));
        Assert.That(cut.Markup, Does.Contain("Right lung"));
        Assert.That(cut.Markup, Does.Contain("Dr. Path"));
    }

    [Test]
    public async Task AnatomicPathology_ShowsEmptyState()
    {
        MockWorkflowGrain.GetAPCasesAsync().Returns(new List<APCaseIndexEntry>());

        var cut = Ctx.Render<AnatomicPathology>();

        cut.Find("input").Change("PAT-002");
        await cut.FindAll("button").First(b => b.TextContent.Contains("Load Cases"))
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No anatomic pathology cases found"));
    }

    [Test]
    public async Task AnatomicPathology_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetAPCasesAsync().Returns<List<APCaseIndexEntry>>(
            _ => throw new Exception("Grain unavailable"));

        var cut = Ctx.Render<AnatomicPathology>();

        cut.Find("input").Change("PAT-003");
        await cut.FindAll("button").First(b => b.TextContent.Contains("Load Cases"))
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Grain unavailable"));
    }

    [Test]
    public async Task AnatomicPathology_DisplaysSummaryCards()
    {
        var entries = new List<APCaseIndexEntry>
        {
            new() { CaseId = "AP-001", AccessionNumber = "SP-001", CaseType = APCaseType.SurgicalPathology, Status = APCaseStatus.Final },
            new() { CaseId = "AP-002", AccessionNumber = "CY-001", CaseType = APCaseType.Cytology, Status = APCaseStatus.Received },
            new() { CaseId = "AP-003", AccessionNumber = "AU-001", CaseType = APCaseType.Autopsy, Status = APCaseStatus.InProgress }
        };
        MockWorkflowGrain.GetAPCasesAsync().Returns(entries);

        var cut = Ctx.Render<AnatomicPathology>();

        cut.Find("input").Change("PAT-004");
        await cut.FindAll("button").First(b => b.TextContent.Contains("Load Cases"))
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Total Cases"));
        var cards = cut.FindAll(".card-num");
        Assert.That(cards.Count, Is.GreaterThanOrEqualTo(4));
    }
}
