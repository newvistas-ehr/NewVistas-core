// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// End-to-end route-vs-dose-form validation. Seeds an NDF VA Product with a real
/// DosageFormName and a local DRUG that links to it, then verifies the warn-only
/// advisory is resolved from the VA Product dose form and round-trips through the
/// grain read path the UI uses (GetPrescriptionAsync / GetOrderAsync).
/// </summary>
[TestFixture]
public class DoseFormRouteWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    /// <summary>
    /// Seeds a VA Product (#50.68) with the given dose form and a DRUG (#50)
    /// pointing at it, returning the drug id (DRUG IEN) for use in orders.
    /// </summary>
    private async Task<string> SeedDrugWithDoseFormAsync(string dosageFormName)
    {
        string productIen = $"VAPROD-{Guid.NewGuid()}";
        await _cluster.GrainFactory.GetGrain<IVaProductGrain>(productIen).LoadProductAsync(
            name: "TEST PRODUCT",
            vaGenericIen: "",
            vaGenericName: "TEST GENERIC",
            dosageFormIen: "",
            dosageFormName: dosageFormName,
            strength: "10",
            strengthUnitIen: "",
            strengthUnitName: "MG",
            printName: "TEST PRODUCT 10MG",
            primaryDrugClassCode: "",
            primaryDrugClassName: "",
            formularyIndicator: true,
            formularyRestrictions: null,
            controlledSubstanceSchedule: null,
            copayTier: null,
            maxSingleDose: null,
            minSingleDose: null,
            maxDailyDose: null,
            minDailyDose: null,
            maxCumulativeDose: null,
            doseUnit: null,
            isDosageCheckExcluded: false,
            isHazardousWaste: false,
            rxNormCode: null,
            vuid: null,
            isActive: true,
            inactivationDate: null,
            ingredients: new List<DrugIngredient>(),
            ndcCodes: new List<NdcEntry>(),
            secondaryDrugClassCodes: new List<string>());

        string drugId = $"DRUG-{Guid.NewGuid()}";
        await _cluster.GrainFactory.GetGrain<IDrugGrain>(drugId).SaveDrugAsync(new DrugState
        {
            LocalName = "TEST DRUG",
            VaProductIen = productIen,
            VaProductName = "TEST PRODUCT"
        });

        return drugId;
    }

    [Test]
    public async Task OutpatientRx_MismatchedRoute_WarningRoundTripsViaVaProductDoseForm()
    {
        string drugId = await SeedDrugWithDoseFormAsync("TABLET");

        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.CreatePrescriptionAsync(
            "P-001", "TEST DRUG", drugId, "10MG", "INTRAVENOUS", "DAILY",
            "TAKE ONE", 90, 90, 5, "PROV-001", "DR. SMITH",
            "PHARM-01", "MAIN PHARMACY", null, null);

        // Re-read through the same path the UI uses.
        PharmacyState state = await rx.GetPrescriptionAsync();

        Assert.That(state.RouteValidationWarning, Is.Not.Null.And.Contains("INTRAVENOUS"));
        Assert.That(state.RouteSuggestions, Contains.Item("ORAL"));
        Assert.That(state.Status, Is.EqualTo("ACTIVE"), "Warn-only: the prescription is still created.");
    }

    [Test]
    public async Task OutpatientRx_MatchingRoute_NoWarning()
    {
        string drugId = await SeedDrugWithDoseFormAsync("TABLET");

        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.CreatePrescriptionAsync(
            "P-001", "TEST DRUG", drugId, "10MG", "ORAL", "DAILY",
            "TAKE ONE", 90, 90, 5, "PROV-001", "DR. SMITH",
            "PHARM-01", "MAIN PHARMACY", null, null);

        PharmacyState state = await rx.GetPrescriptionAsync();
        Assert.That(state.RouteValidationWarning, Is.Null);
        Assert.That(state.RouteSuggestions, Is.Empty);
    }

    [Test]
    public async Task InpatientIvOrder_OralRoute_WarningRoundTrips()
    {
        string drugId = await SeedDrugWithDoseFormAsync("INJECTION");

        string orderId = $"PSJ-{Guid.NewGuid()}";
        IInpatientOrderGrain order = _cluster.GrainFactory.GetGrain<IInpatientOrderGrain>(orderId);
        await order.CreateOrderAsync(
            "P-001", "W1", "WARD 1", "4B-12", "IV", "TEST DRUG", drugId,
            "1", "GM", "ORAL", "Q8H", "ROUTINE",
            null, null, null, null, "PROV-001", "DR. SMITH", null, "NS", 100, "over 60 min");

        InpatientOrderState state = await order.GetOrderAsync();
        Assert.That(state.RouteValidationWarning, Is.Not.Null.And.Contains("ORAL"));
        Assert.That(state.RouteSuggestions, Contains.Item("INTRAVENOUS"));
    }
}
