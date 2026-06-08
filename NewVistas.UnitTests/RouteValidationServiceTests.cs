// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for RouteValidationService and the warn-only route check wired into
/// PharmacyGrain and InpatientOrderGrain. Validation never blocks an order; a
/// mismatch stamps an advisory warning with suggested routes.
/// </summary>
[TestFixture]
public class RouteValidationServiceTests
{
    private TestCluster _cluster = default!;
    private readonly RouteValidationService _service = new();

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Service: core ValidateAsync (dose form already known) ───────────────

    [Test]
    public async Task Validate_MatchingRoute_IsValid()
    {
        RouteValidationResult result =
            await _service.ValidateAsync(_cluster.GrainFactory, "TABLET", "ORAL");
        Assert.That(result.Outcome, Is.EqualTo(RouteValidationOutcome.Valid));
    }

    [Test]
    public async Task Validate_MismatchedRoute_WarnsWithSuggestions()
    {
        RouteValidationResult result =
            await _service.ValidateAsync(_cluster.GrainFactory, "TABLET", "INTRAVENOUS");

        Assert.That(result.Outcome, Is.EqualTo(RouteValidationOutcome.Warn));
        Assert.That(result.Message, Is.Not.Null.And.Contains("INTRAVENOUS"));
        Assert.That(result.SuggestedRoutes, Contains.Item("ORAL"));
    }

    [Test]
    public async Task Validate_NullDoseForm_IsValid()
    {
        RouteValidationResult result =
            await _service.ValidateAsync(_cluster.GrainFactory, null, "ORAL");
        Assert.That(result.Outcome, Is.EqualTo(RouteValidationOutcome.Valid));
    }

    [Test]
    public async Task Validate_UnknownDoseForm_FailsOpen()
    {
        RouteValidationResult result =
            await _service.ValidateAsync(_cluster.GrainFactory, "WIDGET-FORM-XYZ", "INTRAVENOUS");
        Assert.That(result.Outcome, Is.EqualTo(RouteValidationOutcome.Valid));
    }

    // ─── Service: resolve dose form from a drug, then validate ───────────────

    [Test]
    public async Task ResolveDoseForm_PrefersVaProductThenDispenseUnit()
    {
        // Drug with a dispense unit only (no VA product link).
        string drugId = $"DRUG-{Guid.NewGuid()}";
        await _cluster.GrainFactory.GetGrain<IDrugGrain>(drugId)
            .SaveDrugAsync(new DrugState { LocalName = "TEST TAB", DispenseUnit = "TABLET" });

        string? doseForm = await _service.ResolveDoseFormAsync(_cluster.GrainFactory, drugId);
        Assert.That(doseForm, Is.EqualTo("TABLET"));
    }

    [Test]
    public async Task ResolveDoseForm_NullDrugId_ReturnsNull()
    {
        string? doseForm = await _service.ResolveDoseFormAsync(_cluster.GrainFactory, null);
        Assert.That(doseForm, Is.Null);
    }

    // ─── PharmacyGrain warn-stamp ────────────────────────────────────────────

    [Test]
    public async Task PharmacyGrain_MismatchedRoute_StampsWarning()
    {
        string drugId = $"DRUG-{Guid.NewGuid()}";
        await _cluster.GrainFactory.GetGrain<IDrugGrain>(drugId)
            .SaveDrugAsync(new DrugState { LocalName = "TEST TAB", DispenseUnit = "TABLET" });

        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.CreatePrescriptionAsync("P-001", "TEST TAB", drugId, "1 TAB",
            "INTRAVENOUS", "QD", null, 30, 30, 0, null, null, null, null, null, null);

        PharmacyState state = await rx.GetPrescriptionAsync();
        Assert.That(state.RouteValidationWarning, Is.Not.Null.And.Contains("INTRAVENOUS"));
        Assert.That(state.RouteSuggestions, Contains.Item("ORAL"));
        // Warn-only: the prescription is still created.
        Assert.That(state.Status, Is.EqualTo("ACTIVE"));
    }

    [Test]
    public async Task PharmacyGrain_MatchingRoute_NoWarning()
    {
        string drugId = $"DRUG-{Guid.NewGuid()}";
        await _cluster.GrainFactory.GetGrain<IDrugGrain>(drugId)
            .SaveDrugAsync(new DrugState { LocalName = "TEST TAB", DispenseUnit = "TABLET" });

        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.CreatePrescriptionAsync("P-001", "TEST TAB", drugId, "1 TAB",
            "ORAL", "QD", null, 30, 30, 0, null, null, null, null, null, null);

        PharmacyState state = await rx.GetPrescriptionAsync();
        Assert.That(state.RouteValidationWarning, Is.Null);
        Assert.That(state.RouteSuggestions, Is.Empty);
    }

    [Test]
    public async Task PharmacyGrain_UnknownDrug_NoWarning()
    {
        // No seeded drug → dose form unresolved → fail open, no warning.
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.CreatePrescriptionAsync("P-001", "MYSTERY DRUG", $"DRUG-{Guid.NewGuid()}", "1",
            "INTRAVENOUS", "QD", null, 30, 30, 0, null, null, null, null, null, null);

        PharmacyState state = await rx.GetPrescriptionAsync();
        Assert.That(state.RouteValidationWarning, Is.Null);
    }

    // ─── InpatientOrderGrain warn-stamp ──────────────────────────────────────

    [Test]
    public async Task InpatientOrderGrain_IvOrderWithOralRoute_StampsWarning()
    {
        string drugId = $"DRUG-{Guid.NewGuid()}";
        await _cluster.GrainFactory.GetGrain<IDrugGrain>(drugId)
            .SaveDrugAsync(new DrugState { LocalName = "TEST INJ", DispenseUnit = "INJECTION" });

        string orderId = $"PSJ-{Guid.NewGuid()}";
        IInpatientOrderGrain order = _cluster.GrainFactory.GetGrain<IInpatientOrderGrain>(orderId);
        await order.CreateOrderAsync("P-001", "W1", "WARD 1", "4B-12", "IV",
            "TEST INJ", drugId, "1", "GM", "ORAL", "Q8H", "ROUTINE",
            null, null, null, null, null, null, null, null, null, null);

        InpatientOrderState state = await order.GetOrderAsync();
        Assert.That(state.RouteValidationWarning, Is.Not.Null.And.Contains("ORAL"));
        Assert.That(state.RouteSuggestions, Contains.Item("INTRAVENOUS"));
        Assert.That(state.Status, Is.EqualTo("PENDING"));
    }
}
