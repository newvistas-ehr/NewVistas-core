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
public class LabEdiPageTests : BlazorTestBase
{
    [Test]
    public void LabEdi_RendersPageTitle()
    {
        var cut = Ctx.Render<LabEdi>();
        Assert.That(cut.Markup, Does.Contain("Lab EDI"));
    }

    [Test]
    public void LabEdi_RendersTabs()
    {
        var cut = Ctx.Render<LabEdi>();
        Assert.That(cut.Markup, Does.Contain("Reference Labs"));
        Assert.That(cut.Markup, Does.Contain("Orders"));
        Assert.That(cut.Markup, Does.Contain("Dashboard"));
    }

    [Test]
    public async Task LabEdi_LoadsLabsFromGrain()
    {
        var mockIndex = Substitute.For<ILabEdiIndexGrain>();
        mockIndex.GetReferenceLabsAsync().Returns(new List<LabEdiLabSummary>
        {
            new() { ReferenceLabId = "LAB-1", LabName = "Quest", ConnectionType = "HL7", IsActive = true }
        });
        MockGrainFactory.GetGrain<ILabEdiIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var cut = Ctx.Render<LabEdi>();
        var buttons = cut.FindAll("button");
        var loadBtn = buttons.First(b => b.TextContent.Contains("Load Reference Labs"));
        await loadBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Quest"));
    }
}
