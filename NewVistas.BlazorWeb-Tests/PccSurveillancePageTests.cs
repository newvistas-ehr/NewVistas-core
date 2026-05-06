// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class PccSurveillancePageTests : BlazorTestBase
{
    [Test]
    public void PccSurveillance_RendersPageTitle()
    {
        var mockMatchIndex = Substitute.For<IPccSurveillanceMatchIndexGrain>();
        mockMatchIndex.GetAllAsync().Returns(new List<PccSurveillanceMatchIndexEntry>());
        MockGrainFactory.GetGrain<IPccSurveillanceMatchIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockMatchIndex);

        var mockConfigIndex = Substitute.For<IPccSurveillanceConfigIndexGrain>();
        mockConfigIndex.GetAllAsync().Returns(new List<PccSurveillanceConfigIndexEntry>());
        MockGrainFactory.GetGrain<IPccSurveillanceConfigIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockConfigIndex);

        var cut = Ctx.Render<PccSurveillance>();
        Assert.That(cut.Markup, Does.Contain("PCC Encounter Surveillance"));
    }

    [Test]
    public void PccSurveillance_RendersTabs()
    {
        var mockMatchIndex = Substitute.For<IPccSurveillanceMatchIndexGrain>();
        mockMatchIndex.GetAllAsync().Returns(new List<PccSurveillanceMatchIndexEntry>());
        MockGrainFactory.GetGrain<IPccSurveillanceMatchIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockMatchIndex);

        var mockConfigIndex = Substitute.For<IPccSurveillanceConfigIndexGrain>();
        mockConfigIndex.GetAllAsync().Returns(new List<PccSurveillanceConfigIndexEntry>());
        MockGrainFactory.GetGrain<IPccSurveillanceConfigIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockConfigIndex);

        var cut = Ctx.Render<PccSurveillance>();
        Assert.That(cut.Markup, Does.Contain("Surveillance Matches"));
        Assert.That(cut.Markup, Does.Contain("Configurations"));
    }

    [Test]
    public void PccSurveillance_ShowsErrorOnLoadFailure()
    {
        var mockMatchIndex = Substitute.For<IPccSurveillanceMatchIndexGrain>();
        mockMatchIndex.GetAllAsync().Returns<List<PccSurveillanceMatchIndexEntry>>(x => throw new Exception("fail"));
        MockGrainFactory.GetGrain<IPccSurveillanceMatchIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockMatchIndex);

        // Config load also needs to fail, because LoadConfigs clears the error set by LoadMatches
        var mockConfigIndex = Substitute.For<IPccSurveillanceConfigIndexGrain>();
        mockConfigIndex.GetAllAsync().Returns<List<PccSurveillanceConfigIndexEntry>>(x => throw new Exception("fail"));
        MockGrainFactory.GetGrain<IPccSurveillanceConfigIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockConfigIndex);

        var cut = Ctx.Render<PccSurveillance>();
        Assert.That(cut.Markup, Does.Contain("Failed to load"));
    }
}
