// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class HomeHealthPageTests : BlazorTestBase
{
    private IHBPCRegistryGrain _mockRegistry = null!;

    public override void Setup()
    {
        base.Setup();
        _mockRegistry = Substitute.For<IHBPCRegistryGrain>();
        MockGrainFactory.GetGrain<IHBPCRegistryGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockRegistry);
        _mockRegistry.GetActivePatientsAsync().Returns(new List<HBPCRegistryEntry>());
        _mockRegistry.GetAllPatientsAsync().Returns(new List<HBPCRegistryEntry>());
    }

    [Test]
    public void HomeHealth_RendersPageTitle()
    {
        var cut = Ctx.Render<HomeHealth>();
        Assert.That(cut.Markup, Does.Contain("Home Health"));
    }

    [Test]
    public void HomeHealth_RendersTabBar()
    {
        var cut = Ctx.Render<HomeHealth>();
        Assert.That(cut.Markup, Does.Contain("HBPC Registry"));
        Assert.That(cut.Markup, Does.Contain("Visit Schedule"));
        Assert.That(cut.Markup, Does.Contain("Dashboard"));
    }

    [Test]
    public async Task HomeHealth_ShowsErrorOnGrainFailure()
    {
        _mockRegistry.GetActivePatientsAsync().Returns<List<HBPCRegistryEntry>>(
            _ => throw new Exception("Registry unavailable"));

        var cut = Ctx.Render<HomeHealth>();

        Assert.That(cut.Markup, Does.Contain("Error loading registry"));
    }
}
