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
public class PharmacyPosPageTests : BlazorTestBase
{
    public override void Setup()
    {
        base.Setup();
        MockGrainFactory.GetGrain<IPharmacyPosInsurerIndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Substitute.For<IPharmacyPosInsurerIndexGrain>());
        MockGrainFactory.GetGrain<IPharmacyPosInsurerGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Substitute.For<IPharmacyPosInsurerGrain>());
    }

    [Test]
    public void PharmacyPos_RendersPageTitle()
    {
        var cut = Ctx.Render<PharmacyPos>();
        Assert.That(cut.Markup, Does.Contain("Pharmacy Point of Sale"));
    }

    [Test]
    public async Task PharmacyPos_LoadsClaimsFromWorkflowGrain()
    {
        var claims = new List<PosClaimIndexEntry>
        {
            new() { ClaimId = "C-001", PatientId = "P-001", DrugName = "Atorvastatin 20mg", Status = PosClaimStatus.Pending, TransactionType = NcpdpTransactionType.B1, DateOfService = DateTime.UtcNow }
        };
        MockWorkflowGrain.GetPosClaimsAsync().Returns(claims);

        var cut = Ctx.Render<PharmacyPos>();
        cut.Find("input.lookup-input").Change("P-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Atorvastatin 20mg"));
    }

    [Test]
    public async Task PharmacyPos_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetPosClaimsAsync().Returns<List<PosClaimIndexEntry>>(
            _ => throw new Exception("Grain error"));

        var cut = Ctx.Render<PharmacyPos>();
        cut.Find("input.lookup-input").Change("P-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Grain error"));
    }
}
