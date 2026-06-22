// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace NewVistas.Abstractions.Security;

/// <summary>
/// Functional areas of the application for menu visibility control.
/// Each area maps to a set of security keys that grant access.
/// </summary>
public enum MenuArea
{
    /// <summary>Always visible — Home, Patient Lookup, Reference data.</summary>
    General,

    /// <summary>Clinical patient care — Cover Sheet, Problems, Meds, Orders, Notes, etc.</summary>
    Clinical,

    /// <summary>Pharmacy operations — Outpatient Rx, Inpatient Meds, Drug Accountability, etc.</summary>
    Pharmacy,

    /// <summary>Laboratory — Lab results, orders, instruments, shipping.</summary>
    Laboratory,

    /// <summary>Radiology — Imaging studies, reports.</summary>
    Radiology,

    /// <summary>Surgery — Operative scheduling, reports.</summary>
    Surgery,

    /// <summary>Mental Health — Screening instruments, assessments.</summary>
    MentalHealth,

    /// <summary>Nursing — assessments, shift handoff, care plans, triage, task worklist, pain.</summary>
    Nursing,

    /// <summary>Registration / Scheduling — ADT, appointments, eligibility.</summary>
    Registration,

    /// <summary>Financial / Billing — AR, IB, EDI, Fee Basis, IFCAP.</summary>
    Financial,

    /// <summary>System administration — Site parameters, user management, audit.</summary>
    SystemAdmin,
}

/// <summary>
/// Maps menu areas to the security keys that grant access.
/// Used by all UI layers (Blazor, WPF, CharUI) for consistent menu filtering.
///
/// Design: one-time HashSet intersection per area — O(min(n,m)) where n=user keys, m=area keys.
/// User keys are fetched once at login and cached for the session lifetime.
/// </summary>
public static class MenuAccessMap
{
    // ── Key groups (computed once, shared across all calls) ─────────────

    private static readonly HashSet<string> ClinicalKeys =
    [
        SecurityKeys.PROVIDER, SecurityKeys.ORES, SecurityKeys.ORELSE,
        SecurityKeys.TIU_SIGN, SecurityKeys.TIU_COSIGN,
        SecurityKeys.GMRV_VITALS, SecurityKeys.GMRA_ALLERGY, SecurityKeys.GMPL_PROBLEM,
        SecurityKeys.LRLAB, SecurityKeys.LRVERIFY,
        SecurityKeys.RA_VERIFY, SecurityKeys.SR_SURGERY,
        SecurityKeys.YS_MH_INSTRUMENT, SecurityKeys.GMRC_CONSULTS,
        // Disease-specific registries surfaced on the chart (e.g., diabetes
        // pre-visit plan on Cover Sheet) and CHS authorization workflow on
        // the Consults tab — both gated by a clinician-relevant key.
        SecurityKeys.CanManageDiabetesRegistry,
        SecurityKeys.CanAuthorizeChs,
    ];

    private static readonly HashSet<string> PharmacyKeys =
    [
        SecurityKeys.PSO_PHARMACY, SecurityKeys.PSJ_RPHARM,
        SecurityKeys.PSA_ORDERS, SecurityKeys.PSB_MANAGER,
    ];

    private static readonly HashSet<string> LaboratoryKeys =
    [
        SecurityKeys.LRLAB, SecurityKeys.LRVERIFY, SecurityKeys.LRSU,
        // Clinical providers also see labs
        SecurityKeys.PROVIDER, SecurityKeys.ORES, SecurityKeys.ORELSE,
    ];

    private static readonly HashSet<string> RadiologyKeys =
    [
        SecurityKeys.RA_VERIFY, SecurityKeys.RA_TECHNOLOGIST,
        SecurityKeys.PROVIDER, SecurityKeys.ORES, SecurityKeys.ORELSE,
    ];

    private static readonly HashSet<string> SurgeryKeys =
    [
        SecurityKeys.SR_SURGERY,
        SecurityKeys.PROVIDER, SecurityKeys.ORES,
    ];

    private static readonly HashSet<string> MentalHealthKeys =
    [
        SecurityKeys.YS_MH_INSTRUMENT,
        SecurityKeys.PROVIDER,
    ];

    private static readonly HashSet<string> NursingKeys =
    [
        // Nursing workflows — nurses (order-entry-else, vitals, allergy, problem)
        // plus the providers who supervise/review them.
        SecurityKeys.ORELSE, SecurityKeys.GMRV_VITALS,
        SecurityKeys.GMRA_ALLERGY, SecurityKeys.GMPL_PROBLEM,
        SecurityKeys.PROVIDER, SecurityKeys.ORES,
    ];

    private static readonly HashSet<string> RegistrationKeys =
    [
        SecurityKeys.SD_SCHEDULING, SecurityKeys.DG_ADMIT,
        SecurityKeys.DG_SENSITIVITY,
        // Admins can also access registration functions
        SecurityKeys.XUMGR,
    ];

    private static readonly HashSet<string> FinancialKeys =
    [
        // Financial modules — admins until dedicated financial keys are added
        SecurityKeys.XUMGR, SecurityKeys.XUAUDIT,
    ];

    private static readonly HashSet<string> SystemAdminKeys =
    [
        SecurityKeys.XUMGR, SecurityKeys.XUPROG,
    ];

    /// <summary>
    /// Returns the set of security keys that grant access to a menu area.
    /// </summary>
    public static IReadOnlySet<string> GetKeysForArea(MenuArea area) => area switch
    {
        MenuArea.General => Empty,
        MenuArea.Clinical => ClinicalKeys,
        MenuArea.Pharmacy => PharmacyKeys,
        MenuArea.Laboratory => LaboratoryKeys,
        MenuArea.Radiology => RadiologyKeys,
        MenuArea.Surgery => SurgeryKeys,
        MenuArea.MentalHealth => MentalHealthKeys,
        MenuArea.Nursing => NursingKeys,
        MenuArea.Registration => RegistrationKeys,
        MenuArea.Financial => FinancialKeys,
        MenuArea.SystemAdmin => SystemAdminKeys,
        _ => Empty,
    };

    private static readonly HashSet<string> Empty = [];

    /// <summary>
    /// Check if a user with the given keys has access to a menu area.
    /// General area is always accessible. Other areas require at least one matching key.
    /// </summary>
    public static bool HasAccess(MenuArea area, IReadOnlyCollection<string> userKeys)
    {
        if (area == MenuArea.General)
            return true;

        IReadOnlySet<string> required = GetKeysForArea(area);
        foreach (string key in userKeys)
        {
            if (required.Contains(key))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns all menu areas a user has access to, given their security keys.
    /// Called once after login to precompute accessible areas.
    /// </summary>
    public static HashSet<MenuArea> GetAccessibleAreas(IReadOnlyCollection<string> userKeys)
    {
        var areas = new HashSet<MenuArea> { MenuArea.General };
        foreach (MenuArea area in Enum.GetValues<MenuArea>())
        {
            if (HasAccess(area, userKeys))
                areas.Add(area);
        }
        return areas;
    }
}
