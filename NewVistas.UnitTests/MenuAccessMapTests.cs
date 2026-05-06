// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using NewVistas.Abstractions.Security;

namespace NewVistas.UnitTests;

/// <summary>
/// Pin the menu-area visibility behavior for <see cref="MenuAccessMap"/>.
/// Tests are pure (no TestCluster) since the map is a static lookup.
///
/// The "new clinical keys" (CanManageDiabetesRegistry, CanAuthorizeChs)
/// were added to <see cref="MenuAccessMap"/>'s ClinicalKeys set so that
/// users holding only one of those keys (e.g., a CHS coordinator with no
/// other clinical key) still see the Clinical menu area. Without this,
/// the WpfDelphiUI's Cover Sheet diabetes panel and Consults-tab CHS
/// action bar would be silently hidden — the underlying grain calls would
/// succeed but the user couldn't navigate to the chart tab that triggers
/// them.
/// </summary>
[TestFixture]
public class MenuAccessMapTests
{
    // ── General is always accessible ─────────────────────────────────────

    [Test]
    public void HasAccess_General_AlwaysTrue_EvenForEmptyKeys()
    {
        Assert.That(MenuAccessMap.HasAccess(MenuArea.General, []), Is.True);
    }

    [Test]
    public void GetAccessibleAreas_EmptyKeys_OnlyGeneral()
    {
        HashSet<MenuArea> areas = MenuAccessMap.GetAccessibleAreas([]);
        Assert.That(areas, Is.EquivalentTo(new[] { MenuArea.General }));
    }

    // ── Clinical area: classic VistA keys ─────────────────────────────────

    [Test]
    public void HasAccess_Clinical_GrantedByProvider()
    {
        Assert.That(MenuAccessMap.HasAccess(MenuArea.Clinical, [SecurityKeys.PROVIDER]), Is.True);
    }

    [Test]
    public void HasAccess_Clinical_GrantedByOres()
    {
        Assert.That(MenuAccessMap.HasAccess(MenuArea.Clinical, [SecurityKeys.ORES]), Is.True);
    }

    [Test]
    public void HasAccess_Clinical_GrantedByLrLab()
    {
        Assert.That(MenuAccessMap.HasAccess(MenuArea.Clinical, [SecurityKeys.LRLAB]), Is.True);
    }

    [Test]
    public void HasAccess_Clinical_DeniedByPharmacyKey()
    {
        Assert.That(MenuAccessMap.HasAccess(MenuArea.Clinical, [SecurityKeys.PSO_PHARMACY]), Is.False);
    }

    // ── Clinical area: new chart-feature keys (regression for the
    //    MenuAccessMap update that backs the WpfDelphiUI refresh) ──────────

    [Test]
    public void HasAccess_Clinical_GrantedByCanManageDiabetesRegistry()
    {
        // Without this, the WpfDelphiUI Cover Sheet diabetes panel would
        // never be visible to a diabetes-program coordinator who holds
        // *only* CanManageDiabetesRegistry — even though the snapshot/
        // pre-visit-plan endpoints would happily serve them.
        Assert.That(MenuAccessMap.HasAccess(MenuArea.Clinical, [SecurityKeys.CanManageDiabetesRegistry]), Is.True);
    }

    [Test]
    public void HasAccess_Clinical_GrantedByCanAuthorizeChs()
    {
        // Same regression for the CHS action bar on the Consults tab.
        // CHS coordinators frequently hold no other clinical key.
        Assert.That(MenuAccessMap.HasAccess(MenuArea.Clinical, [SecurityKeys.CanAuthorizeChs]), Is.True);
    }

    [Test]
    public void GetAccessibleAreas_OnlyDiabetesKey_IncludesClinical()
    {
        HashSet<MenuArea> areas = MenuAccessMap.GetAccessibleAreas([SecurityKeys.CanManageDiabetesRegistry]);
        Assert.That(areas, Does.Contain(MenuArea.Clinical));
        Assert.That(areas, Does.Contain(MenuArea.General));
    }

    [Test]
    public void GetAccessibleAreas_OnlyChsKey_IncludesClinical()
    {
        HashSet<MenuArea> areas = MenuAccessMap.GetAccessibleAreas([SecurityKeys.CanAuthorizeChs]);
        Assert.That(areas, Does.Contain(MenuArea.Clinical));
    }

    // ── Other areas: unaffected by the new keys ──────────────────────────

    [Test]
    public void HasAccess_Pharmacy_NotGrantedByDiabetesKey()
    {
        Assert.That(MenuAccessMap.HasAccess(MenuArea.Pharmacy, [SecurityKeys.CanManageDiabetesRegistry]), Is.False);
    }

    [Test]
    public void HasAccess_Pharmacy_NotGrantedByChsKey()
    {
        Assert.That(MenuAccessMap.HasAccess(MenuArea.Pharmacy, [SecurityKeys.CanAuthorizeChs]), Is.False);
    }

    [Test]
    public void HasAccess_Registration_NotGrantedByDiabetesKey()
    {
        // Diabetes/CHS keys are clinical, not registration. Verify they
        // don't accidentally light up unrelated menu areas.
        Assert.That(MenuAccessMap.HasAccess(MenuArea.Registration, [SecurityKeys.CanManageDiabetesRegistry]), Is.False);
    }

    [Test]
    public void HasAccess_SystemAdmin_NotGrantedByChsKey()
    {
        Assert.That(MenuAccessMap.HasAccess(MenuArea.SystemAdmin, [SecurityKeys.CanAuthorizeChs]), Is.False);
    }

    // ── Multi-key composition ────────────────────────────────────────────

    [Test]
    public void GetAccessibleAreas_DoctorWithBothNewKeys_StillOnlyClinical()
    {
        // A doctor who happens to hold BOTH new keys plus PROVIDER should
        // see Clinical (and only Clinical-relevant areas) — the new keys
        // don't leak into Pharmacy/Lab/etc.
        HashSet<MenuArea> areas = MenuAccessMap.GetAccessibleAreas(
        [
            SecurityKeys.PROVIDER,
            SecurityKeys.CanManageDiabetesRegistry,
            SecurityKeys.CanAuthorizeChs,
        ]);
        Assert.That(areas, Does.Contain(MenuArea.Clinical));
        Assert.That(areas, Does.Contain(MenuArea.Laboratory),
            "PROVIDER is in LaboratoryKeys (clinicians can see labs).");
        Assert.That(areas, Does.Contain(MenuArea.Radiology),
            "PROVIDER is in RadiologyKeys (clinicians can see radiology).");
        Assert.That(areas, Does.Not.Contain(MenuArea.Pharmacy));
        Assert.That(areas, Does.Not.Contain(MenuArea.Financial));
        Assert.That(areas, Does.Not.Contain(MenuArea.SystemAdmin));
    }

    [Test]
    public void GetKeysForArea_Clinical_ContainsBothNewKeys()
    {
        IReadOnlySet<string> clinicalKeys = MenuAccessMap.GetKeysForArea(MenuArea.Clinical);
        Assert.That(clinicalKeys, Does.Contain(SecurityKeys.CanManageDiabetesRegistry));
        Assert.That(clinicalKeys, Does.Contain(SecurityKeys.CanAuthorizeChs));
    }

    [Test]
    public void GetKeysForArea_General_IsEmpty()
    {
        IReadOnlySet<string> generalKeys = MenuAccessMap.GetKeysForArea(MenuArea.General);
        Assert.That(generalKeys, Is.Empty,
            "General area is open to everyone — no key set required.");
    }
}
