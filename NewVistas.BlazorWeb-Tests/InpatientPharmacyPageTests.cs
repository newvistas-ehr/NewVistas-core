// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class InpatientPharmacyPageTests : BlazorTestBase
{
    private IPatientInpatientProfileGrain _mockProfile = null!;

    public override void Setup()
    {
        base.Setup();
        _mockProfile = Substitute.For<IPatientInpatientProfileGrain>();
        MockGrainFactory
            .GetGrain<IPatientInpatientProfileGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockProfile);
    }

    [Test]
    public void InpatientPharmacy_RendersPageTitle()
    {
        var cut = Ctx.Render<InpatientPharmacy>();
        Assert.That(cut.Markup, Does.Contain("Inpatient Pharmacy"));
    }

    [Test]
    public async Task InpatientPharmacy_LoadsOrdersFromGrain()
    {
        var entries = new List<InpatientOrderIndexEntry>
        {
            new() { OrderId = "ORD-001", DrugName = "Vancomycin 1g", OrderType = "IV", Status = "ACTIVE", Priority = "STAT", IsVerified = true, ProviderName = "Dr. Jones" }
        };
        _mockProfile.GetAllOrdersAsync().Returns(entries);

        var cut = Ctx.Render<InpatientPharmacy>();
        cut.Find("input.input-field").Change("P-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Vancomycin 1g"));
        Assert.That(cut.Markup, Does.Contain("STAT"));
    }

    [Test]
    public async Task InpatientPharmacy_ShowsErrorOnGrainFailure()
    {
        _mockProfile.GetAllOrdersAsync().Returns<List<InpatientOrderIndexEntry>>(
            _ => throw new Exception("Silo down"));

        var cut = Ctx.Render<InpatientPharmacy>();
        cut.Find("input.input-field").Change("P-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Failed to load orders"));
    }
}
