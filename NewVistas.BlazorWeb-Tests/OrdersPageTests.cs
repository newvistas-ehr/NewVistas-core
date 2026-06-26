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
public class OrdersPageTests : BlazorTestBase
{
    [Test]
    public void Orders_RendersPageTitle()
    {
        var cut = Ctx.Render<Orders>();

        Assert.That(cut.Markup, Does.Contain("Order Entry"));
    }

    [Test]
    public void Orders_PromptsToSelectPatientWhenNoneChosen()
    {
        var cut = Ctx.Render<Orders>();

        Assert.That(cut.Markup, Does.Contain("Select a patient"));
    }

    [Test]
    public void Orders_ShowsSelectedPatientInBar()
    {
        SelectPatient("PATIENT-001", "SMITH, JOHN");

        var cut = Ctx.Render<Orders>();

        Assert.That(cut.Markup, Does.Contain("Change patient"));
    }

    [Test]
    public void Orders_LoadsDataFromGrain()
    {
        var orders = new List<OrderSummary>
        {
            new() { OrderId = "ORD-001", OrderText = "CBC WITH DIFFERENTIAL", OrderType = "Lab",
                     Status = "Active", StartDate = DateTime.UtcNow, ProviderName = "Dr. Smith" },
            new() { OrderId = "ORD-002", OrderText = "LISINOPRIL 10MG", OrderType = "Pharmacy",
                     Status = "Pending", StartDate = DateTime.UtcNow, ProviderName = "Dr. Jones" }
        };
        MockWorkflowGrain.GetOrdersByFilterAsync(2).Returns(orders);

        SelectPatient("PATIENT-001");
        var cut = Ctx.Render<Orders>();

        Assert.That(cut.Markup, Does.Contain("CBC WITH DIFFERENTIAL"));
        Assert.That(cut.Markup, Does.Contain("LISINOPRIL 10MG"));
    }

    [Test]
    public void Orders_ShowsEmptyState()
    {
        MockWorkflowGrain.GetOrdersByFilterAsync(2).Returns(new List<OrderSummary>());

        SelectPatient("PATIENT-002");
        var cut = Ctx.Render<Orders>();

        Assert.That(cut.Markup, Does.Contain("No orders found"));
    }

    [Test]
    public void Orders_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetOrdersByFilterAsync(2).Returns<List<OrderSummary>>(
            _ => throw new Exception("Silo unreachable"));

        SelectPatient("PATIENT-003");
        var cut = Ctx.Render<Orders>();

        Assert.That(cut.Markup, Does.Contain("Error loading orders"));
        Assert.That(cut.Markup, Does.Contain("Silo unreachable"));
    }
}
