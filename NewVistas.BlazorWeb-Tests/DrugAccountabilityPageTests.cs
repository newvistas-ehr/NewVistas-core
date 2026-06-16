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
public class DrugAccountabilityPageTests : BlazorTestBase
{
    private IDrugAccountabilityLocationGrain _mockLocGrain = null!;

    public override void Setup()
    {
        base.Setup();
        _mockLocGrain = Substitute.For<IDrugAccountabilityLocationGrain>();
        MockGrainFactory
            .GetGrain<IDrugAccountabilityLocationGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockLocGrain);
    }

    [Test]
    public void DrugAccountability_RendersPageTitle()
    {
        var cut = Ctx.Render<DrugAccountability>();
        Assert.That(cut.Markup, Does.Contain("Drug Accountability"));
    }

    [Test]
    public async Task DrugAccountability_LoadsDrugsFromGrain()
    {
        var drugs = new List<DrugBalanceSummary>
        {
            new() { DrugId = "D-001", DrugName = "Morphine 10mg", CurrentBalance = 50, UnitOfMeasure = "tablets", IsControlled = true, ReorderPoint = 20 }
        };
        _mockLocGrain.GetAllDrugsAsync().Returns(drugs);

        var cut = Ctx.Render<DrugAccountability>();
        cut.Find("input.input-id").Change("VAULT-001");
        var buttons = cut.FindAll("button");
        await buttons[0].ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Morphine 10mg"));
        Assert.That(cut.Markup, Does.Contain("DEA"));
    }

    [Test]
    public async Task DrugAccountability_ShowsErrorOnFailure()
    {
        _mockLocGrain.GetAllDrugsAsync().Returns<List<DrugBalanceSummary>>(
            _ => throw new Exception("Location not found"));

        var cut = Ctx.Render<DrugAccountability>();
        cut.Find("input.input-id").Change("BAD-LOC");
        var buttons = cut.FindAll("button");
        await buttons[0].ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Location not found"));
    }
}
