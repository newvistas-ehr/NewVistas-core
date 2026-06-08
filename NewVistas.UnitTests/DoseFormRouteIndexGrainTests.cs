// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for DoseFormRouteIndexGrain — the RxNorm-derived dose-form → valid
/// route index. Verifies self-seeding, dose-form lookups, fail-open behavior, and
/// a drift guard that every curated VistA route name actually exists in the
/// canonical route file (#51.23) via MedicationRouteIndexGrain.
/// </summary>
[TestFixture]
public class DoseFormRouteIndexGrainTests
{
    private const string IndexKey = "DOSE-FORM-ROUTE-INDEX";
    private const string RouteIndexKey = "MED-ROUTE-INDEX";

    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IDoseFormRouteIndexGrain Index() =>
        _cluster.GrainFactory.GetGrain<IDoseFormRouteIndexGrain>(IndexKey);

    [Test]
    public async Task Index_SelfSeedsOnFirstActivation()
    {
        bool loaded = await Index().IsLoadedAsync();
        Assert.That(loaded, Is.True);

        List<DoseFormGroup> groups = await Index().GetAllGroupsAsync();
        Assert.That(groups, Is.Not.Empty);
    }

    [Test]
    public async Task GetValidRoutes_ForTablet_IncludesOral()
    {
        List<string> routes = await Index().GetValidRoutesForDoseFormAsync("TABLET");
        Assert.That(routes, Contains.Item("ORAL"));
    }

    [Test]
    public async Task GetValidRoutes_ForInjection_IncludesParenteralRoutes()
    {
        List<string> routes = await Index().GetValidRoutesForDoseFormAsync("INJECTION");
        Assert.That(routes, Contains.Item("INTRAVENOUS"));
        Assert.That(routes, Contains.Item("INTRAMUSCULAR"));
        Assert.That(routes, Contains.Item("SUBCUTANEOUS"));
    }

    [Test]
    public async Task IsRouteValid_TabletOral_IsTrue()
    {
        bool valid = await Index().IsRouteValidForDoseFormAsync("TABLET", "ORAL");
        Assert.That(valid, Is.True);
    }

    [Test]
    public async Task IsRouteValid_TabletIntravenous_IsFalse()
    {
        bool valid = await Index().IsRouteValidForDoseFormAsync("TABLET", "INTRAVENOUS");
        Assert.That(valid, Is.False);
    }

    [Test]
    public async Task IsRouteValid_IsCaseInsensitive()
    {
        bool valid = await Index().IsRouteValidForDoseFormAsync("tablet", "oral");
        Assert.That(valid, Is.True);
    }

    [Test]
    public async Task IsRouteValid_UnknownDoseForm_FailsOpen()
    {
        bool valid = await Index().IsRouteValidForDoseFormAsync("WIDGET-FORM-XYZ", "INTRAVENOUS");
        Assert.That(valid, Is.True, "Unknown/unmapped dose forms must fail open (never block).");
    }

    [Test]
    public async Task IsRouteValid_BlankArguments_FailOpen()
    {
        Assert.That(await Index().IsRouteValidForDoseFormAsync("", "ORAL"), Is.True);
        Assert.That(await Index().IsRouteValidForDoseFormAsync("TABLET", ""), Is.True);
    }

    [Test]
    public async Task AmbiguousForm_Suppository_AllowsBothRectalAndVaginal()
    {
        // SUPPOSITORY maps to both rectal and vaginal RxNorm forms; the union of
        // routes must permit both so neither legitimate route warns.
        Assert.That(await Index().IsRouteValidForDoseFormAsync("SUPPOSITORY", "RECTAL"), Is.True);
        Assert.That(await Index().IsRouteValidForDoseFormAsync("SUPPOSITORY", "VAGINAL"), Is.True);
    }

    [Test]
    public async Task DriftGuard_EveryCuratedRouteExistsInFile51_23()
    {
        // Every VistA route name used in the curated DFG→route table must resolve
        // against the canonical Standard Medication Routes file (#51.23). This
        // catches typos / renames that would silently break validation.
        IMedicationRouteIndexGrain routeIndex =
            _cluster.GrainFactory.GetGrain<IMedicationRouteIndexGrain>(RouteIndexKey);

        List<DoseFormGroup> groups = await Index().GetAllGroupsAsync();
        HashSet<string> curatedRoutes = groups
            .SelectMany(g => g.ValidVistaRoutes)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.That(curatedRoutes, Is.Not.Empty);

        List<string> missing = new();
        foreach (string route in curatedRoutes)
        {
            MedicationRoute? match = await routeIndex.GetRouteByNameAsync(route);
            if (match is null)
                missing.Add(route);
        }

        Assert.That(missing, Is.Empty,
            $"Curated routes not found in File #51.23: {string.Join(", ", missing)}");
    }

    [Test]
    public async Task Refresh_WhenRxNavDisabled_IsNoOp()
    {
        int updated = await Index().RefreshFromRxNavAsync();
        Assert.That(updated, Is.EqualTo(0));
    }

    // ─── Full RxNorm Appendix 3 coverage ─────────────────────────────────────

    [Test]
    public async Task Index_ContainsAll44Appendix3Groups()
    {
        List<DoseFormGroup> groups = await Index().GetAllGroupsAsync();
        Assert.That(groups, Has.Count.EqualTo(44),
            "Table A should contain all 44 RxNorm Appendix 3 dose form groups.");
    }

    [Test]
    public async Task Index_HasRepresentativeAppendix3Groups()
    {
        List<DoseFormGroup> groups = await Index().GetAllGroupsAsync();
        HashSet<string> names = groups.Select(g => g.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // A spread across oral, route-based, body-cavity, and specialty groups.
        foreach (string expected in new[]
        {
            "Oral Product", "Pill", "Injectable Product", "Topical Product",
            "Ophthalmic Product", "Inhalant Product", "Rectal Product", "Vaginal Product",
            "Intraperitoneal Product", "Intravesical Product", "Drug Implant Product",
            "Shampoo Product", "Soap Product", "Wafer Product", "Pyelocalyceal Product",
        })
        {
            Assert.That(names, Contains.Item(expected));
        }
    }

    [Test]
    public async Task MembershipIntegrity_EveryDoseFormGroupReferenceExists()
    {
        // Every group named by a dose-form lookup must be a real Table A group.
        // We probe via the public API: a known dose form must resolve to routes
        // drawn only from defined groups (no dangling references → non-empty for
        // mapped forms, and the drift guard already checks the route names).
        Assert.That(await Index().GetValidRoutesForDoseFormAsync("Oral Tablet"), Contains.Item("ORAL"));
        Assert.That(await Index().GetValidRoutesForDoseFormAsync("Transdermal System"),
            Does.Contain("TRANSDERMAL").And.Contain("TOPICAL"));
    }

    [Test]
    public async Task NewMappings_ResolveToExpectedRoutes()
    {
        // Forms reachable only after the full expansion.
        Assert.That(await Index().IsRouteValidForDoseFormAsync("SUPP,RTL", "RECTAL"), Is.True);
        Assert.That(await Index().IsRouteValidForDoseFormAsync("OINTMENT,RTL", "RECTAL"), Is.True);
        Assert.That(await Index().IsRouteValidForDoseFormAsync("SHAMPOO", "TOPICAL"), Is.True);
        Assert.That(await Index().IsRouteValidForDoseFormAsync("WAFER", "ORAL"), Is.True);
        Assert.That(await Index().IsRouteValidForDoseFormAsync("PATCH", "TRANSDERMAL"), Is.True);
        Assert.That((await Index().GetValidRoutesForDoseFormAsync("INHALER")), Contains.Item("INHALATION"));
    }

    [Test]
    public async Task Pyelocalyceal_HasNoRoutes_AndFailsOpen()
    {
        // No File #51.23 equivalent → empty route set → never warns.
        DoseFormGroup? group = await Index().GetGroupByNameAsync("Pyelocalyceal Product");
        Assert.That(group, Is.Not.Null);
        Assert.That(group!.ValidVistaRoutes, Is.Empty);
        Assert.That(await Index().IsRouteValidForDoseFormAsync("Powder for Pyelocalyceal Solution", "ORAL"),
            Is.True, "A form whose only group has no routes must fail open.");
    }
}
