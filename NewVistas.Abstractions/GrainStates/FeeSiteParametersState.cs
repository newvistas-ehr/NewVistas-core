// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

// ─── Fee Site Parameters — VistA File #162 (site configuration subfields) ─────

/// <summary>
/// Site-level configuration for the Fee Basis module, controlling authorization
/// limits, payment defaults, and budget tracking (VistA File #162 site fields).
/// Grain key: "FEE-SITE-PARAMS" (singleton).
/// </summary>
[GenerateSerializer]
public class FeeSiteParametersState
{
    /// <summary>Station identifier for this VA facility.</summary>
    [Id(0)] public string SiteId { get; set; } = string.Empty;

    /// <summary>Human-readable name of this VA facility.</summary>
    [Id(1)] public string SiteName { get; set; } = string.Empty;

    /// <summary>Whether the Fee Basis program is currently active at this site.</summary>
    [Id(2)] public bool IsFeeBasisEnabled { get; set; } = true;

    /// <summary>Current fiscal year for budget tracking (e.g., 2024).</summary>
    [Id(3)] public int FiscalYear { get; set; }

    /// <summary>Total annual budget allocated for Fee Basis community care (optional).</summary>
    [Id(4)] public decimal? AnnualBudget { get; set; }

    /// <summary>Maximum number of days an authorization may remain active (default 365).</summary>
    [Id(5)] public int MaxAuthorizationDays { get; set; } = 365;

    /// <summary>Whether pre-authorization is required before services are rendered.</summary>
    [Id(6)] public bool RequiresPreAuthorization { get; set; } = true;

    /// <summary>
    /// Dollar amount below which invoices may be auto-approved without manual review (optional).
    /// Null means all invoices require manual review.
    /// </summary>
    [Id(7)] public decimal? AutoApprovalLimit { get; set; }

    /// <summary>Default payment method for vendor disbursements (e.g., "EFT", "Check").</summary>
    [Id(8)] public string DefaultPaymentMethod { get; set; } = "EFT";

    /// <summary>UTC timestamp of the most recent update to these parameters.</summary>
    [Id(9)] public DateTime? LastUpdatedDate { get; set; }

    /// <summary>User ID of the staff member who last updated these parameters.</summary>
    [Id(10)] public string? LastUpdatedByUserId { get; set; }
}
