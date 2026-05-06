// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class DrgGrouperPageTests : BlazorTestBase
{
    private IDrgIndexGrain _mockDrgIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockDrgIndex = Substitute.For<IDrgIndexGrain>();
        MockGrainFactory.GetGrain<IDrgIndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockDrgIndex);
        _mockDrgIndex.GetAllAssignmentsAsync().Returns(new List<DrgAssignmentEntry>());
    }

    [Test]
    public void DrgGrouper_RendersPageTitle()
    {
        var cut = Ctx.Render<DrgGrouper>();
        Assert.That(cut.Markup, Does.Contain("DRG Grouper"));
    }

    [Test]
    public void DrgGrouper_RendersRefreshButton()
    {
        var cut = Ctx.Render<DrgGrouper>();
        Assert.That(cut.Markup, Does.Contain("Refresh"));
    }

    [Test]
    public async Task DrgGrouper_LoadsAssignmentsFromGrain()
    {
        var entries = new List<DrgAssignmentEntry>
        {
            new() { AdmissionId = "ADM-001", PatientName = "Doe, Jane", DrgCode = "470",
                     DrgDescription = "Major Joint Replacement", RelativeWeight = 2.0547m,
                     ActualLOS = 3, Status = "ASSIGNED", CcMccLevel = "MCC" }
        };
        _mockDrgIndex.GetAllAssignmentsAsync().Returns(entries);

        var cut = Ctx.Render<DrgGrouper>();

        Assert.That(cut.Markup, Does.Contain("Doe, Jane"));
        Assert.That(cut.Markup, Does.Contain("470"));
    }

    [Test]
    public async Task DrgGrouper_ShowsErrorOnFailure()
    {
        _mockDrgIndex.GetAllAssignmentsAsync().Returns<List<DrgAssignmentEntry>>(
            _ => throw new Exception("DRG index unavailable"));

        var cut = Ctx.Render<DrgGrouper>();

        Assert.That(cut.Markup, Does.Contain("DRG index unavailable"));
    }
}
