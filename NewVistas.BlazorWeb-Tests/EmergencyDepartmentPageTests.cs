// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class EmergencyDepartmentPageTests : BlazorTestBase
{
    private IEdBoardGrain _mockEdBoard = null!;

    public override void Setup()
    {
        base.Setup();
        _mockEdBoard = Substitute.For<IEdBoardGrain>();
        MockGrainFactory.GetGrain<IEdBoardGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockEdBoard);
        _mockEdBoard.GetActiveVisitsAsync().Returns(new List<EdBoardEntry>());
    }

    [Test]
    public void EmergencyDepartment_RendersPageTitle()
    {
        var cut = Ctx.Render<EmergencyDepartment>();
        Assert.That(cut.Markup, Does.Contain("Emergency Department"));
    }

    [Test]
    public void EmergencyDepartment_RendersTabBar()
    {
        var cut = Ctx.Render<EmergencyDepartment>();
        Assert.That(cut.Markup, Does.Contain("Tracking Board"));
        Assert.That(cut.Markup, Does.Contain("Register Patient"));
        Assert.That(cut.Markup, Does.Contain("ED Statistics"));
    }

    [Test]
    public async Task EmergencyDepartment_ShowsErrorOnFailure()
    {
        _mockEdBoard.GetActiveVisitsAsync().Returns<List<EdBoardEntry>>(
            _ => throw new Exception("ED board unavailable"));

        var cut = Ctx.Render<EmergencyDepartment>();

        Assert.That(cut.Markup, Does.Contain("ED board unavailable"));
    }
}
