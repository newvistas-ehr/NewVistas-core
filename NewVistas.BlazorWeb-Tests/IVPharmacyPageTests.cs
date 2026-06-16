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
public class IVPharmacyPageTests : BlazorTestBase
{
    [Test]
    public void IVPharmacy_RendersPageTitle()
    {
        var cut = Ctx.Render<IVPharmacy>();
        Assert.That(cut.Markup, Does.Contain("IV Pharmacy"));
    }

    [Test]
    public async Task IVPharmacy_LoadsOrdersFromWorkflowGrain()
    {
        var orders = new List<IVAdmixOrderIndexEntry>
        {
            new() { OrderId = "IV-001", BaseSolution = "Normal Saline", Status = IVAdmixOrderStatus.Pending, Priority = IVAdmixPriority.Routine, TotalVolumeMl = 1000, CreatedDate = DateTime.UtcNow }
        };
        MockWorkflowGrain.GetIVAdmixOrdersAsync().Returns(orders);
        MockWorkflowGrain.GetPendingIVAdmixOrdersAsync().Returns(orders);
        MockWorkflowGrain.GetActiveIVAdmixOrdersAsync().Returns(new List<IVAdmixOrderIndexEntry>());

        var cut = Ctx.Render<IVPharmacy>();
        cut.Find("input").Change("P-001");
        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Normal Saline"));
    }

    [Test]
    public async Task IVPharmacy_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetIVAdmixOrdersAsync().Returns<List<IVAdmixOrderIndexEntry>>(
            _ => throw new Exception("Connection lost"));

        var cut = Ctx.Render<IVPharmacy>();
        cut.Find("input").Change("P-002");
        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Failed to load orders"));
    }
}
