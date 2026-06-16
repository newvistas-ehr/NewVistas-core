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
using Orleans;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class MasterPatientIndexPageTests : BlazorTestBase
{
    private IMpiSearchGrain MockMpiSearchGrain { get; set; } = null!;
    private IMpiMatchGrain MockMpiMatchGrain { get; set; } = null!;

    public override void Setup()
    {
        base.Setup();

        MockMpiSearchGrain = Substitute.For<IMpiSearchGrain>();
        MockMpiMatchGrain = Substitute.For<IMpiMatchGrain>();

        MockGrainFactory
            .GetGrain<IMpiSearchGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(MockMpiSearchGrain);
        MockGrainFactory
            .GetGrain<IMpiMatchGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(MockMpiMatchGrain);
    }

    [Test]
    public void MasterPatientIndex_RendersPageTitle()
    {
        var cut = Ctx.Render<MasterPatientIndex>();

        Assert.That(cut.Markup, Does.Contain("Master Patient Index"));
    }

    [Test]
    public void MasterPatientIndex_RendersSearchInput()
    {
        var cut = Ctx.Render<MasterPatientIndex>();

        var input = cut.Find("input.input-wide");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Does.Contain("Search"));
    }

    [Test]
    public void MasterPatientIndex_HasTabs()
    {
        var cut = Ctx.Render<MasterPatientIndex>();

        Assert.That(cut.Markup, Does.Contain("Patient Search"));
        Assert.That(cut.Markup, Does.Contain("Patient Match"));
        Assert.That(cut.Markup, Does.Contain("MPI Status"));
    }

    [Test]
    public async Task MasterPatientIndex_LoadsDataFromGrain()
    {
        var results = new List<MpiSearchResult>
        {
            new() { Icn = "ICN-001", PatientName = "SMITH,JOHN", Ssn = "123456789",
                     DateOfBirth = new DateTime(1960, 5, 15), Sex = "M",
                     TreatingFacilities = new List<string> { "VA Boston" }, IsDeceased = false }
        };
        MockMpiSearchGrain.SearchAsync(Arg.Any<string>(), Arg.Any<int>()).Returns(results);

        var cut = Ctx.Render<MasterPatientIndex>();

        cut.Find("input.input-wide").Change("SMITH");
        // Use Last to find the Search action button, not the "Patient Search" tab
        await cut.FindAll("button").Last(b => b.TextContent.Trim() == "Search")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockMpiSearchGrain.Received(1).SearchAsync("SMITH", 25);

        Assert.That(cut.Markup, Does.Contain("SMITH,JOHN"));
        Assert.That(cut.Markup, Does.Contain("ICN-001"));
    }

    [Test]
    public async Task MasterPatientIndex_ShowsEmptyState()
    {
        MockMpiSearchGrain.SearchAsync(Arg.Any<string>(), Arg.Any<int>()).Returns(new List<MpiSearchResult>());

        var cut = Ctx.Render<MasterPatientIndex>();

        cut.Find("input.input-wide").Change("NOBODY");
        await cut.FindAll("button").Last(b => b.TextContent.Trim() == "Search")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No patients found"));
    }

    [Test]
    public async Task MasterPatientIndex_ShowsErrorOnGrainFailure()
    {
        MockMpiSearchGrain.SearchAsync(Arg.Any<string>(), Arg.Any<int>()).Returns<List<MpiSearchResult>>(
            _ => throw new Exception("MPI unavailable"));

        var cut = Ctx.Render<MasterPatientIndex>();

        cut.Find("input.input-wide").Change("TEST");
        await cut.FindAll("button").Last(b => b.TextContent.Trim() == "Search")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // The page wraps the error as "Error: {message}"
        Assert.That(cut.Markup, Does.Contain("Error: MPI unavailable"));
    }
}
