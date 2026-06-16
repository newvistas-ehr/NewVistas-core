// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class EventCapturePageTests : BlazorTestBase
{
    private IDssUnitIndexGrain _mockDssIdx = null!;

    public override void Setup()
    {
        base.Setup();
        _mockDssIdx = Substitute.For<IDssUnitIndexGrain>();
        _mockDssIdx.GetAllAsync().Returns(new List<DssUnitIndexEntry>());
        MockGrainFactory.GetGrain<IDssUnitIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockDssIdx);
    }

    [Test]
    public void EventCapture_RendersPageTitle()
    {
        var cut = Ctx.Render<EventCapture>();
        Assert.That(cut.Markup, Does.Contain("Event Capture"));
    }

    [Test]
    public void EventCapture_RendersTabs()
    {
        var cut = Ctx.Render<EventCapture>();
        Assert.That(cut.Markup, Does.Contain("Encounters"));
        Assert.That(cut.Markup, Does.Contain("DSS Units"));
        Assert.That(cut.Markup, Does.Contain("Workload Search"));
    }

    [Test]
    public async Task EventCapture_ShowsErrorOnLoadFailure()
    {
        var mockEncIdx = Substitute.For<IEventCaptureEncounterIndexGrain>();
        mockEncIdx.GetByPatientAsync(Arg.Any<string>(), Arg.Any<int>())
            .Returns<List<EventCaptureIndexEntry>>(_ => throw new Exception("Encounter index fail"));
        MockGrainFactory.GetGrain<IEventCaptureEncounterIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockEncIdx);

        var cut = Ctx.Render<EventCapture>();
        cut.Find("input[type='text']").Input("PAT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Failed to load encounters"));
    }
}
