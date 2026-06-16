// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class SecurityPageTests : BlazorTestBase
{
    [Test]
    public void Security_RendersPageTitle()
    {
        var cut = Ctx.Render<Security>();
        Assert.That(cut.Markup, Does.Contain("Patient Access Control"));
    }

    [Test]
    public void Security_RendersLookupBar()
    {
        var cut = Ctx.Render<Security>();
        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
    }

    [Test]
    public async Task Security_LoadsAccessControlFromGrain()
    {
        var mockGrain = Substitute.For<IPatientAccessControlGrain>();
        mockGrain.GetAccessControlAsync().Returns(new PatientAccessControlState
        {
            PatientId = "PAT-001", IsSensitive = true, SensitivityLevel = "HIGH",
            SensitivityCategories = new List<string> { "HIV" },
            AuthorizedProviderIds = new List<string> { "PROV-001" }
        });
        mockGrain.GetAccessLogAsync().Returns(new List<PatientAccessLog>());
        MockGrainFactory.GetGrain<IPatientAccessControlGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockGrain);

        var cut = Ctx.Render<Security>();
        cut.Find("input.lookup-input").Input("PAT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("YES - SENSITIVE"));
    }

    [Test]
    public async Task Security_ShowsErrorOnGrainFailure()
    {
        var mockGrain = Substitute.For<IPatientAccessControlGrain>();
        mockGrain.GetAccessControlAsync().Returns<PatientAccessControlState>(x => throw new Exception("Grain error"));
        MockGrainFactory.GetGrain<IPatientAccessControlGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockGrain);

        var cut = Ctx.Render<Security>();
        cut.Find("input.lookup-input").Input("PAT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error loading data"));
    }
}
