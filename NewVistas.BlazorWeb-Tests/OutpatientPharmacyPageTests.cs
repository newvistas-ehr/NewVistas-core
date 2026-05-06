// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class OutpatientPharmacyPageTests : BlazorTestBase
{
    private IPatientPrescriptionIndexGrain _mockIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockIndex = Substitute.For<IPatientPrescriptionIndexGrain>();
        MockGrainFactory
            .GetGrain<IPatientPrescriptionIndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockIndex);
    }

    [Test]
    public void OutpatientPharmacy_RendersPageTitle()
    {
        var cut = Ctx.Render<OutpatientPharmacy>();
        Assert.That(cut.Markup, Does.Contain("Outpatient Pharmacy"));
    }

    [Test]
    public async Task OutpatientPharmacy_LoadsPrescriptionsFromGrain()
    {
        var entries = new List<PrescriptionIndexEntry>
        {
            new() { PrescriptionId = "RX-001", DrugName = "Metformin 500mg", Status = "ACTIVE", Priority = "ROUTINE", IsVerified = true, ProviderName = "Dr. Smith" }
        };
        _mockIndex.GetAllAsync().Returns(entries);

        var cut = Ctx.Render<OutpatientPharmacy>();
        cut.Find("input.input-field").Change("P-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Metformin 500mg"));
        Assert.That(cut.Markup, Does.Contain("ACTIVE"));
    }

    [Test]
    public async Task OutpatientPharmacy_ShowsErrorOnGrainFailure()
    {
        _mockIndex.GetAllAsync().Returns<List<PrescriptionIndexEntry>>(
            _ => throw new Exception("Grain unavailable"));

        var cut = Ctx.Render<OutpatientPharmacy>();
        cut.Find("input.input-field").Change("P-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Failed to load prescriptions"));
        Assert.That(cut.Markup, Does.Contain("Grain unavailable"));
    }
}
