// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
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
    public void Orders_RendersLookupBar()
    {
        var cut = Ctx.Render<Orders>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void Orders_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<Orders>();

        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task Orders_LoadsDataFromGrain()
    {
        var orders = new List<OrderSummary>
        {
            new() { OrderId = "ORD-001", OrderText = "CBC WITH DIFFERENTIAL", OrderType = "Lab",
                     Status = "Active", StartDate = DateTime.UtcNow, ProviderName = "Dr. Smith" },
            new() { OrderId = "ORD-002", OrderText = "LISINOPRIL 10MG", OrderType = "Pharmacy",
                     Status = "Pending", StartDate = DateTime.UtcNow, ProviderName = "Dr. Jones" }
        };
        MockWorkflowGrain.GetOrdersByFilterAsync(2).Returns(orders);

        var cut = Ctx.Render<Orders>();

        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetOrdersByFilterAsync(2);

        Assert.That(cut.Markup, Does.Contain("CBC WITH DIFFERENTIAL"));
        Assert.That(cut.Markup, Does.Contain("LISINOPRIL 10MG"));
    }

    [Test]
    public async Task Orders_ShowsEmptyState()
    {
        MockWorkflowGrain.GetOrdersByFilterAsync(2).Returns(new List<OrderSummary>());

        var cut = Ctx.Render<Orders>();

        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No orders found"));
    }

    [Test]
    public async Task Orders_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetOrdersByFilterAsync(2).Returns<List<OrderSummary>>(
            _ => throw new Exception("Silo unreachable"));

        var cut = Ctx.Render<Orders>();

        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error loading orders"));
        Assert.That(cut.Markup, Does.Contain("Silo unreachable"));
    }
}
