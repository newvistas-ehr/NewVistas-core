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
public class CmopPageTests : BlazorTestBase
{
    private ICmopSuspenseGrain _mockSuspense = null!;

    public override void Setup()
    {
        base.Setup();
        _mockSuspense = Substitute.For<ICmopSuspenseGrain>();
        MockGrainFactory
            .GetGrain<ICmopSuspenseGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockSuspense);
        MockGrainFactory
            .GetGrain<ICmopTransmissionIndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Substitute.For<ICmopTransmissionIndexGrain>());
        _mockSuspense.GetQueuedPrescriptionsAsync().Returns(new List<CmopSuspenseEntry>());
    }

    [Test]
    public void Cmop_RendersPageTitle()
    {
        var cut = Ctx.Render<Cmop>();
        Assert.That(cut.Markup, Does.Contain("Consolidated Mail Outpatient Pharmacy"));
    }

    [Test]
    public void Cmop_LoadsSuspenseOnInit()
    {
        _mockSuspense.GetQueuedPrescriptionsAsync().Returns(new List<CmopSuspenseEntry>
        {
            new() { PrescriptionId = "RX-001", PatientName = "DOE, JANE", DrugName = "Lisinopril 10mg", FillType = "ORIGINAL", Priority = "ROUTINE", QueuedDate = DateTime.UtcNow }
        });

        var cut = Ctx.Render<Cmop>();
        Assert.That(cut.Markup, Does.Contain("Lisinopril 10mg"));
    }

    [Test]
    public void Cmop_ShowsEmptyQueueMessage()
    {
        var cut = Ctx.Render<Cmop>();
        Assert.That(cut.Markup, Does.Contain("No prescriptions in suspense queue"));
    }
}
