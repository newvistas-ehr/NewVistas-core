// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class ControlledSubstancesPageTests : BlazorTestBase
{
    private ICSDispenseLogGrain _mockDispenseLog = null!;
    private ICSInspectionLogGrain _mockInspectionLog = null!;

    public override void Setup()
    {
        base.Setup();
        _mockDispenseLog = Substitute.For<ICSDispenseLogGrain>();
        _mockInspectionLog = Substitute.For<ICSInspectionLogGrain>();
        MockGrainFactory
            .GetGrain<ICSDispenseLogGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockDispenseLog);
        MockGrainFactory
            .GetGrain<ICSInspectionLogGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockInspectionLog);
    }

    [Test]
    public void ControlledSubstances_RendersPageTitle()
    {
        var cut = Ctx.Render<ControlledSubstances>();
        Assert.That(cut.Markup, Does.Contain("Controlled Substances"));
    }

    [Test]
    public async Task ControlledSubstances_LoadsDispenseLog()
    {
        var entries = new List<CSDispenseSummaryEntry>
        {
            new() { RecordId = "D-001", PatientName = "SMITH, JOHN", DrugName = "Oxycodone 5mg", DrugSchedule = DEADrugSchedule.ScheduleII, QuantityDispensed = 30, UnitOfMeasure = "tablets", DispensedByName = "RPh Jones", DispenseDateTime = DateTime.UtcNow, RunningBalance = 70 }
        };
        _mockDispenseLog.GetAllRecordsAsync().Returns(entries);
        _mockInspectionLog.GetAllInspectionsAsync().Returns(new List<CSInspectionSummaryEntry>());

        var cut = Ctx.Render<ControlledSubstances>();
        cut.Find("input.input-field").Input("VAULT-1A");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Oxycodone 5mg"));
        Assert.That(cut.Markup, Does.Contain("SMITH, JOHN"));
    }

    [Test]
    public async Task ControlledSubstances_ShowsErrorOnFailure()
    {
        _mockDispenseLog.GetAllRecordsAsync().Returns<List<CSDispenseSummaryEntry>>(
            _ => throw new Exception("Timeout"));
        _mockInspectionLog.GetAllInspectionsAsync().Returns<List<CSInspectionSummaryEntry>>(
            _ => throw new Exception("Timeout"));

        var cut = Ctx.Render<ControlledSubstances>();
        cut.Find("input.input-field").Input("VAULT-1A");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Failed to load"));
    }
}
