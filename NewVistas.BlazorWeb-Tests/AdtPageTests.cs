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
public class AdtPageTests : BlazorTestBase
{
    private IWardLocationIndexGrain _mockWardIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockWardIndex = Substitute.For<IWardLocationIndexGrain>();
        MockGrainFactory.GetGrain<IWardLocationIndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockWardIndex);
        _mockWardIndex.GetAllWardsAsync().Returns(new List<WardLocationEntry>());
    }

    [Test]
    public void Adt_RendersPageTitle()
    {
        var cut = Ctx.Render<Adt>();
        Assert.That(cut.Markup, Does.Contain("ADT"));
    }

    [Test]
    public void Adt_RendersLookupBar()
    {
        var cut = Ctx.Render<Adt>();
        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void Adt_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<Adt>();
        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task Adt_LoadsMovementsFromGrain()
    {
        var movements = new List<AdtSummary>
        {
            new() { MovementId = "M1", MovementType = "ADMISSION", MovementDateTime = DateTime.Now,
                     WardLocationName = "Ward 3A", RoomBed = "301-A", AttendingPhysicianName = "Dr. Smith",
                     Status = "ADMITTED" }
        };
        MockWorkflowGrain.GetAdtMovementsAsync().Returns(movements);

        var cut = Ctx.Render<Adt>();
        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetAdtMovementsAsync();
        Assert.That(cut.Markup, Does.Contain("Ward 3A"));
        Assert.That(cut.Markup, Does.Contain("ADMITTED"));
    }

    [Test]
    public async Task Adt_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetAdtMovementsAsync().Returns<List<AdtSummary>>(
            _ => throw new Exception("Grain timeout"));

        var cut = Ctx.Render<Adt>();
        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Grain timeout"));
    }

    [Test]
    public async Task Adt_ShowsEmptyStateWhenNoMovements()
    {
        MockWorkflowGrain.GetAdtMovementsAsync().Returns(new List<AdtSummary>());

        var cut = Ctx.Render<Adt>();
        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No ADT movements found"));
    }
}
