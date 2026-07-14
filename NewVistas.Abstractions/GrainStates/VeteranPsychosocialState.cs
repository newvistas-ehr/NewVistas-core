// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Period-of-service era. The existing patient veteran fields (Veteran, service-connected %, branch,
/// discharge, entry/separation dates) are eligibility/rating-centric; era captures the psychosocial /
/// exposure-relevant "when did they serve" that drives presumptive-exposure and cohort work (a Vietnam
/// veteran → Agent Orange presumptions; a post-9/11 veteran → burn-pit / PACT Act presumptions).
/// </summary>
public enum MilitaryServiceEra
{
    WorldWarII = 0,
    KoreanConflict = 1,
    VietnamEra = 2,
    PostVietnam = 3,
    PersianGulfWar = 4,
    PostGulfWar_OefOif = 5, // Afghanistan / Iraq (OEF/OIF/OND)
    Peacetime = 6,
    Other = 99,
}

/// <summary>
/// Presumptive environmental / occupational exposure category (PACT Act &amp; legacy presumptions).
/// Kept as a curated set — these drive registry cohorts and presumptive-condition prompting, not
/// free text.
/// </summary>
public enum MilitaryEnvironmentalExposure
{
    AgentOrangeHerbicide = 0,
    BurnPitAirborneHazards = 1,
    IonizingRadiation = 2,
    GulfWarSwAsiaConditions = 3,
    CampLejeuneContaminatedWater = 4,
    MustardGasOrLewisite = 5,
    AsbestosOther = 6,
    ContaminatedWaterOther = 7,
    Other = 99,
}

/// <summary>
/// Veterans Service Organization / accredited representative point of contact — who advocates for the
/// veteran's benefits (VFW, DAV, American Legion, county VSO, an attorney). <see cref="PowerOfAttorneyOnFile"/>
/// flags a VA Form 21-22 (POA) on file.
/// </summary>
[GenerateSerializer]
public record VsoContact
{
    [Id(0)] public string OrganizationName { get; set; } = string.Empty;
    [Id(1)] public string? RepresentativeName { get; set; }
    [Id(2)] public string? Phone { get; set; }
    [Id(3)] public string? Email { get; set; }
    [Id(4)] public bool PowerOfAttorneyOnFile { get; set; }
}

/// <summary>
/// Veteran psychosocial enrichment (Whole-Person Social Care roadmap R4): the service-context and
/// advocacy facts that matter for whole-person / homeless-veteran / behavioral-health care but sit
/// outside the existing rating-and-eligibility veteran fields. Attached to the patient aggregate as a
/// single optional profile so the existing veteran <c>[Id]</c> surface is untouched.
/// </summary>
[GenerateSerializer]
public record VeteranPsychosocialProfile
{
    /// <summary>Served in a combat theater / received hostile-fire or imminent-danger pay.</summary>
    [Id(0)] public bool CombatVeteran { get; set; }

    /// <summary>Period(s) of service era — a veteran may span more than one.</summary>
    [Id(1)] public List<MilitaryServiceEra> ServiceEras { get; set; } = new();

    /// <summary>Presumptive environmental / occupational exposures.</summary>
    [Id(2)] public List<MilitaryEnvironmentalExposure> Exposures { get; set; } = new();

    /// <summary>Purple Heart recipient (drives copay/priority and is a psychosocial flag).</summary>
    [Id(3)] public bool PurpleHeart { get; set; }

    /// <summary>Homeless / at-risk flag surfaced here for the veteran-outreach workflow (ties to the SDOH Z59 loop).</summary>
    [Id(4)] public bool HomelessOrAtRisk { get; set; }

    /// <summary>Veterans Service Organization / accredited-representative contact.</summary>
    [Id(5)] public VsoContact? Vso { get; set; }

    /// <summary>Free-text psychosocial notes (military sexual trauma flag context, reintegration, etc.).</summary>
    [Id(6)] public string? Notes { get; set; }

    [Id(7)] public DateTime? LastUpdatedDate { get; set; }
    [Id(8)] public string? LastUpdatedBy { get; set; }
}

/// <summary>
/// Suggests a service era from a service-entry date, mirroring the CDC reporting materializer's
/// buckets (<c>PatientMaterializer.DeriveServiceEra</c>) so a UI can prefill the era from the existing
/// service dates instead of asking twice. Suggestion only — the recorded eras remain user-editable.
/// </summary>
public static class VeteranEraHelper
{
    public static MilitaryServiceEra? SuggestEraFromEntryDate(DateTime? serviceEntryDate)
    {
        if (serviceEntryDate is not { } d)
            return null;
        int y = d.Year;
        return y switch
        {
            >= 2001 => MilitaryServiceEra.PostGulfWar_OefOif,
            >= 1990 => MilitaryServiceEra.PersianGulfWar,
            >= 1975 => MilitaryServiceEra.PostVietnam,
            >= 1964 => MilitaryServiceEra.VietnamEra,
            >= 1950 => MilitaryServiceEra.KoreanConflict,
            >= 1941 => MilitaryServiceEra.WorldWarII,
            _ => MilitaryServiceEra.Other,
        };
    }
}
