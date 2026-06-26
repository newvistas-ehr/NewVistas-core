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
public class HomeTelehealthPageTests : BlazorTestBase
{
    [Test]
    public void HomeTelehealth_RendersPageTitle()
    {
        var cut = Ctx.Render<HomeTelehealth>();
        Assert.That(cut.Markup, Does.Contain("Home Telehealth"));
    }

    [Test]
    public void HomeTelehealth_RendersTabBar()
    {
        var cut = Ctx.Render<HomeTelehealth>();
        Assert.That(cut.Markup, Does.Contain("Enrollment"));
        Assert.That(cut.Markup, Does.Contain("Readings"));
        Assert.That(cut.Markup, Does.Contain("Alerts"));
        Assert.That(cut.Markup, Does.Contain("Device Inventory"));
    }

    [Test]
    public async Task HomeTelehealth_LoadsEnrollmentFromGrain()
    {
        var state = new HomeTelehealthPatientState
        {
            PatientId = "PAT-001",
            IsEnrolled = true,
            EnrollmentDate = new DateTime(2025, 1, 1),
            Protocol = HtCareProtocol.Hypertension,
            CareCoordinatorName = "Nurse Jones"
        };
        MockWorkflowGrain.GetHtPatientAsync().Returns(state);

        var cut = Ctx.Render<HomeTelehealth>();
        cut.Find("input.lookup-input").Change("PAT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("ENROLLED"));
    }

    [Test]
    public async Task HomeTelehealth_ShowsErrorOnFailure()
    {
        MockWorkflowGrain.GetHtPatientAsync().Returns<HomeTelehealthPatientState>(
            _ => throw new Exception("Grain unavailable"));

        var cut = Ctx.Render<HomeTelehealth>();
        cut.Find("input.lookup-input").Change("PAT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Load failed"));
    }
}
