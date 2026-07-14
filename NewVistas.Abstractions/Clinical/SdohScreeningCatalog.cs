// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Clinical;

/// <summary>
/// Curated crosswalk for health-related social-needs screening (the CMS <b>AHC-HRSN</b> / PRAPARE
/// core domains): each SDOH domain → the mapped, HIPAA-billable ICD-10 Z-code and the community
/// referral service type. This is what turns a positive social-needs screen into a coded, actionable
/// intervention — a Z55–Z65 code on the problem list and a referral into the existing Social Work
/// machinery.
///
/// Deterministic and static, in the same house style as <c>SymptomCatalog</c> / <c>Pharmacogenomics</c>.
/// The Z-codes were validated against the FY2026 ICD-10-CM set (all billable, not category headers).
/// </summary>
public static class SdohScreeningCatalog
{
    private sealed record DomainDef(
        SdohDomain Domain,
        string Display,
        string ZCode,
        string ZCodeDisplay,
        SocialWorkReferralServiceType ReferralServiceType);

    // AHC-HRSN core-domain → billable Z-code → referral service type.
    private static readonly DomainDef[] Domains =
    {
        new(SdohDomain.Homelessness,             "Homelessness",              "Z59.00",  "Homelessness, unspecified",                                 SocialWorkReferralServiceType.HomelessServices),
        new(SdohDomain.HousingInstability,       "Housing instability",       "Z59.811", "Housing instability, housed, with risk of homelessness",    SocialWorkReferralServiceType.Housing),
        new(SdohDomain.FoodInsecurity,           "Food insecurity",           "Z59.41",  "Food insecurity",                                           SocialWorkReferralServiceType.Food),
        new(SdohDomain.TransportationInsecurity, "Transportation insecurity", "Z59.82",  "Transportation insecurity",                                 SocialWorkReferralServiceType.Transportation),
        new(SdohDomain.UtilityNeeds,             "Utility needs",             "Z59.12",  "Inadequate housing utilities",                              SocialWorkReferralServiceType.FinancialAssistance),
        new(SdohDomain.FinancialStrain,          "Financial strain",          "Z59.869", "Financial insecurity, unspecified",                         SocialWorkReferralServiceType.FinancialAssistance),
        new(SdohDomain.Employment,               "Employment",                "Z56.0",   "Unemployment, unspecified",                                 SocialWorkReferralServiceType.VocationalRehabilitation),
        new(SdohDomain.Education,                "Education",                 "Z55.9",   "Problems related to education and literacy, unspecified",    SocialWorkReferralServiceType.VocationalRehabilitation),
        new(SdohDomain.InterpersonalSafety,      "Interpersonal safety",      "Z65.4",   "Victim of crime and terrorism",                             SocialWorkReferralServiceType.AdultProtectiveServices),
    };

    private static readonly Dictionary<SdohDomain, DomainDef> ByDomain =
        Domains.ToDictionary(d => d.Domain);

    static SdohScreeningCatalog()
    {
        if (ByDomain.Count != Domains.Length)
            throw new InvalidOperationException("SdohScreeningCatalog has duplicate domains.");
    }

    /// <summary>The default AHC-HRSN screening instrument name.</summary>
    public const string DefaultInstrument = "AHC-HRSN";

    /// <summary>All screenable SDOH domains (for building the survey UI).</summary>
    public static IReadOnlyList<SdohDomain> AllDomains { get; } = Domains.Select(d => d.Domain).ToList();

    /// <summary>Human-readable label for a domain.</summary>
    public static string DisplayFor(SdohDomain domain) =>
        ByDomain.TryGetValue(domain, out DomainDef? d) ? d.Display : domain.ToString();

    /// <summary>The mapped billable Z-code for a domain (empty if unmapped).</summary>
    public static string ZCodeFor(SdohDomain domain) =>
        ByDomain.TryGetValue(domain, out DomainDef? d) ? d.ZCode : string.Empty;

    /// <summary>The finding (Z-code + referral suggestion) for a single domain.</summary>
    public static SdohFinding FindingFor(SdohDomain domain)
    {
        DomainDef d = ByDomain[domain];
        return new SdohFinding
        {
            Domain = d.Domain,
            Display = d.Display,
            ZCode = d.ZCode,
            ZCodeDisplay = d.ZCodeDisplay,
            ReferralServiceType = d.ReferralServiceType
        };
    }

    /// <summary>
    /// Evaluates a screening's answers → one <see cref="SdohFinding"/> per POSITIVE domain (with its
    /// Z-code and suggested referral type). Negative and Unknown answers produce no finding — the net
    /// closes only on positives, and "unknown" never becomes a false positive.
    /// </summary>
    public static List<SdohFinding> Evaluate(IEnumerable<SdohScreeningResponse> responses)
    {
        var findings = new List<SdohFinding>();
        if (responses is null)
            return findings;

        foreach (SdohScreeningResponse r in responses)
            if (r.Response == SdohResponse.Positive && ByDomain.ContainsKey(r.Domain))
                findings.Add(FindingFor(r.Domain));

        return findings;
    }
}
