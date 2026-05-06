// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class IncompleteRecordsPageTests : BlazorTestBase
{
    private IIncompleteRecordIndexGrain _mockIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockIndex = Substitute.For<IIncompleteRecordIndexGrain>();
        MockGrainFactory.GetGrain<IIncompleteRecordIndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockIndex);
        _mockIndex.GetAllDeficienciesAsync().Returns(new List<IncompleteRecordEntry>());
    }

    [Test]
    public void IncompleteRecords_RendersPageTitle()
    {
        var cut = Ctx.Render<IncompleteRecords>();
        Assert.That(cut.Markup, Does.Contain("Incomplete Records"));
    }

    [Test]
    public void IncompleteRecords_RendersProviderSelector()
    {
        var cut = Ctx.Render<IncompleteRecords>();
        Assert.That(cut.Markup, Does.Contain("Provider ID"));
    }

    [Test]
    public async Task IncompleteRecords_LoadsDeficienciesFromGrain()
    {
        var entries = new List<IncompleteRecordEntry>
        {
            new() { DeficiencyId = "DEF-001", PatientName = "Smith, John",
                     ProviderName = "Dr. Jones", DeficiencyType = "UNSIGNED_NOTE",
                     Status = "OPEN", DaysOutstanding = 5, IsDelinquent = false }
        };
        _mockIndex.GetAllDeficienciesAsync().Returns(entries);

        var cut = Ctx.Render<IncompleteRecords>();

        Assert.That(cut.Markup, Does.Contain("Smith, John"));
        Assert.That(cut.Markup, Does.Contain("UNSIGNED"));
    }

    [Test]
    public async Task IncompleteRecords_ShowsErrorOnFailure()
    {
        _mockIndex.GetAllDeficienciesAsync().Returns<List<IncompleteRecordEntry>>(
            _ => throw new Exception("Index unavailable"));

        var cut = Ctx.Render<IncompleteRecords>();

        Assert.That(cut.Markup, Does.Contain("Index unavailable"));
    }
}
