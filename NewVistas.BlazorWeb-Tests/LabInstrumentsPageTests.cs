// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class LabInstrumentsPageTests : BlazorTestBase
{
    [Test]
    public void LabInstruments_RendersPageTitle()
    {
        var mockIndex = Substitute.For<IInstrumentIndexGrain>();
        mockIndex.GetAllInstrumentsAsync().Returns(new List<InstrumentEntry>());
        MockGrainFactory.GetGrain<IInstrumentIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var cut = Ctx.Render<LabInstruments>();
        Assert.That(cut.Markup, Does.Contain("Automated Lab Instruments"));
    }

    [Test]
    public void LabInstruments_RendersTabs()
    {
        var mockIndex = Substitute.For<IInstrumentIndexGrain>();
        mockIndex.GetAllInstrumentsAsync().Returns(new List<InstrumentEntry>());
        MockGrainFactory.GetGrain<IInstrumentIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var cut = Ctx.Render<LabInstruments>();
        Assert.That(cut.Markup, Does.Contain("Instrument List"));
    }

    [Test]
    public void LabInstruments_ShowsErrorOnFailure()
    {
        var mockIndex = Substitute.For<IInstrumentIndexGrain>();
        mockIndex.GetAllInstrumentsAsync().Returns<List<InstrumentEntry>>(x => throw new Exception("fail"));
        MockGrainFactory.GetGrain<IInstrumentIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var cut = Ctx.Render<LabInstruments>();
        Assert.That(cut.Markup, Does.Contain("fail"));
    }
}
