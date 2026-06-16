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
public class EdiBillingPageTests : BlazorTestBase
{
    private IEdiTransmissionIndexGrain _mockTxIdx = null!;
    private IEraIndexGrain _mockEraIdx = null!;

    public override void Setup()
    {
        base.Setup();
        _mockTxIdx = Substitute.For<IEdiTransmissionIndexGrain>();
        _mockTxIdx.GetAllAsync().Returns(new List<EdiTransmissionIndexEntry>());
        MockGrainFactory.GetGrain<IEdiTransmissionIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockTxIdx);

        _mockEraIdx = Substitute.For<IEraIndexGrain>();
        _mockEraIdx.GetAllAsync().Returns(new List<EraIndexEntry>());
        MockGrainFactory.GetGrain<IEraIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockEraIdx);
    }

    [Test]
    public void EdiBilling_RendersPageTitle()
    {
        var cut = Ctx.Render<EdiBilling>();
        Assert.That(cut.Markup, Does.Contain("EDI / Electronic Billing"));
    }

    [Test]
    public void EdiBilling_RendersTabs()
    {
        var cut = Ctx.Render<EdiBilling>();
        Assert.That(cut.Markup, Does.Contain("Claims"));
        Assert.That(cut.Markup, Does.Contain("Transmissions"));
        Assert.That(cut.Markup, Does.Contain("Remittances"));
    }

    [Test]
    public async Task EdiBilling_ShowsErrorOnClaimLoadFailure()
    {
        var mockClaimIdx = Substitute.For<IEdiClaimIndexGrain>();
        mockClaimIdx.GetAllAsync().Returns<List<EdiClaimIndexEntry>>(_ => throw new Exception("Claim index down"));
        MockGrainFactory.GetGrain<IEdiClaimIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockClaimIdx);

        var cut = Ctx.Render<EdiBilling>();
        cut.Find("input.form-input").Change("PAT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error loading claims"));
    }
}
