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
public class QualityManagementPageTests : BlazorTestBase
{
    private IQMIncidentIndexGrain _mockIncIndex = null!;
    private IQMReviewIndexGrain _mockRevIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockIncIndex = Substitute.For<IQMIncidentIndexGrain>();
        _mockRevIndex = Substitute.For<IQMReviewIndexGrain>();
        MockGrainFactory.GetGrain<IQMIncidentIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockIncIndex);
        MockGrainFactory.GetGrain<IQMReviewIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockRevIndex);
        _mockIncIndex.GetAllIncidentsAsync().Returns(new List<QMIncidentIndexEntry>());
    }

    [Test]
    public void QualityManagement_RendersTitle()
    {
        var cut = Ctx.Render<QualityManagement>();
        Assert.That(cut.Markup, Does.Contain("Quality Management"));
    }

    [Test]
    public void QualityManagement_RendersTabs()
    {
        var cut = Ctx.Render<QualityManagement>();
        Assert.That(cut.Markup, Does.Contain("Incident Reporting"));
        Assert.That(cut.Markup, Does.Contain("Peer Reviews"));
        Assert.That(cut.Markup, Does.Contain("RCA Dashboard"));
    }

    [Test]
    public async Task QualityManagement_LoadsIncidents()
    {
        var incidents = new List<QMIncidentIndexEntry>
        {
            new() { IncidentId = "QM-1", PatientName = "Jane Doe",
                     OccurrenceDate = DateTime.UtcNow, Category = OccurrenceCategory.MedicationError,
                     Severity = OccurrenceSeverity.MinorHarm, Status = IncidentStatus.Reported }
        };
        _mockIncIndex.GetAllIncidentsAsync().Returns(incidents);

        var cut = Ctx.Render<QualityManagement>();
        cut.WaitForState(() => cut.Markup.Contains("Jane Doe"));

        Assert.That(cut.Markup, Does.Contain("Jane Doe"));
    }

    [Test]
    public async Task QualityManagement_ShowsEmptyState()
    {
        _mockIncIndex.GetAllIncidentsAsync().Returns(new List<QMIncidentIndexEntry>());

        var cut = Ctx.Render<QualityManagement>();
        cut.WaitForState(() => cut.Markup.Contains("No incidents"));

        Assert.That(cut.Markup, Does.Contain("No incidents on record"));
    }
}
