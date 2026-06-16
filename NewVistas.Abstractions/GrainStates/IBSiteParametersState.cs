// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Site-wide Integrated Billing configuration (VistA File #350.9 IB SITE PARAMETERS).
/// A singleton grain holding the facility billing settings, copay thresholds, and rate schedules.
/// Grain key: "IB-SITE-PARAMS"
/// </summary>
[GenerateSerializer]
public class IBSiteParametersState
{
    /// <summary>Site identifier. Always "IB-SITE-PARAMS" for the singleton. (.001)</summary>
    [Id(0)] public string SiteId { get; set; } = "IB-SITE-PARAMS";

    /// <summary>Facility name for billing correspondence. (.01)</summary>
    [Id(1)] public string SiteName { get; set; } = string.Empty;

    /// <summary>Mailing address used on billing statements. (.02)</summary>
    [Id(2)] public string BillingMailingAddress { get; set; } = string.Empty;

    /// <summary>Billing department email address. (.03)</summary>
    [Id(3)] public string? BillingEmail { get; set; }

    /// <summary>Dollar threshold above which outpatient visits trigger a copay charge. (.04)</summary>
    [Id(4)] public decimal? OutpatientCopayThreshold { get; set; }

    /// <summary>Per diem rate applied to inpatient stays (DG INPT PER DIEM). (.05)</summary>
    [Id(5)] public decimal? InpatientPerDiemRate { get; set; }

    /// <summary>Per diem rate applied to NHCU stays. (.06)</summary>
    [Id(6)] public decimal? NhcuPerDiemRate { get; set; }

    /// <summary>
    /// Tier 1 (generic) pharmacy copay amount — Priority Groups 1–3 or SC exempt.
    /// Based on VistA PSO copay tier schedule. (.07)
    /// </summary>
    [Id(7)] public decimal PharmacyCopayTier1Amount { get; set; }

    /// <summary>
    /// Tier 2 (preferred brand) pharmacy copay amount — Priority Groups 4–6. (.08)
    /// </summary>
    [Id(8)] public decimal PharmacyCopayTier2Amount { get; set; }

    /// <summary>
    /// Tier 3 (non-preferred) pharmacy copay amount — Priority Groups 7–8. (.09)
    /// </summary>
    [Id(9)] public decimal PharmacyCopayTier3Amount { get; set; }

    /// <summary>Medicare Part A inpatient deductible amount applied during billing. (.10)</summary>
    [Id(10)] public decimal? MedicareDeductibleAmount { get; set; }

    /// <summary>
    /// Annual copay cap amount. Once a patient's year-to-date balance reaches this figure,
    /// further copay charges are automatically exempt (File #354.75 IB COPAY CAPS).
    /// </summary>
    [Id(11)] public decimal? CopayCapAmount { get; set; }

    /// <summary>
    /// Billing type category codes active at this site (File #350.9005P sub-file).
    /// Examples: "65" (outpatient), "55" (inpatient), "68" (pharmacy).
    /// </summary>
    [Id(12)] public List<string> BillingTypes { get; set; } = new();

    /// <summary>Whether this site participates in the TRICARE insurance billing program. (.11)</summary>
    [Id(13)] public bool IsTricareSiteEnabled { get; set; }

    /// <summary>Date and time these parameters were last updated.</summary>
    [Id(14)] public DateTime? LastUpdatedDate { get; set; }

    /// <summary>User ID of the person who last updated these parameters.</summary>
    [Id(15)] public string? LastUpdatedByUserId { get; set; }
}
