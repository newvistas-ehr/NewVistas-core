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
public class ClinicalCaseRegistriesPageTests : BlazorTestBase
{
    private IClinicalRegistryIndexGrain _mockRegIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockRegIndex = Substitute.For<IClinicalRegistryIndexGrain>();
        MockGrainFactory.GetGrain<IClinicalRegistryIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockRegIndex);
        _mockRegIndex.GetActiveEntriesAsync().Returns(new List<CCREntrySummary>());
    }

    [Test]
    public void ClinicalCaseRegistries_RendersTitle()
    {
        var cut = Ctx.Render<ClinicalCaseRegistries>();
        Assert.That(cut.Markup, Does.Contain("Clinical Case Registries"));
    }

    [Test]
    public void ClinicalCaseRegistries_RendersTabs()
    {
        var cut = Ctx.Render<ClinicalCaseRegistries>();
        Assert.That(cut.Markup, Does.Contain("HIV Registry"));
        Assert.That(cut.Markup, Does.Contain("Hepatitis C Registry"));
        Assert.That(cut.Markup, Does.Contain("Diabetes Registry"));
    }

    [Test]
    public async Task ClinicalCaseRegistries_LoadsEntries()
    {
        var entries = new List<CCREntrySummary>
        {
            new() { PatientId = "P1", PatientName = "HIV Patient",
                     Status = CCREnrollmentStatus.Active,
                     EnrollmentDate = DateTime.UtcNow.AddMonths(-6) }
        };
        _mockRegIndex.GetActiveEntriesAsync().Returns(entries);

        var cut = Ctx.Render<ClinicalCaseRegistries>();
        cut.WaitForState(() => cut.Markup.Contains("HIV Patient"));

        Assert.That(cut.Markup, Does.Contain("HIV Patient"));
    }

    [Test]
    public async Task ClinicalCaseRegistries_ShowsEmptyState()
    {
        _mockRegIndex.GetActiveEntriesAsync().Returns(new List<CCREntrySummary>());

        var cut = Ctx.Render<ClinicalCaseRegistries>();
        cut.WaitForState(() => cut.Markup.Contains("No patients found"));

        Assert.That(cut.Markup, Does.Contain("No patients found in this registry"));
    }
}
