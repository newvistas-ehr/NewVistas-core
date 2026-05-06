// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Site-level configuration for the IFCAP procurement module.
/// Singleton grain — key: "IFCAP-SITE-PARAMS"
/// </summary>
[GenerateSerializer]
public class IfcapSiteParametersState
{
    /// <summary>Unique site identifier. (.01)</summary>
    [Id(0)] public string SiteId { get; set; } = string.Empty;

    /// <summary>Human-readable site name. (.02)</summary>
    [Id(1)] public string SiteName { get; set; } = string.Empty;

    /// <summary>VA facility number. (.03)</summary>
    [Id(2)] public string FacilityNumber { get; set; } = string.Empty;

    /// <summary>Current fiscal year. (.04)</summary>
    [Id(3)] public int FiscalYear { get; set; }

    /// <summary>Default number of days allowed for delivery after PO issuance. (.05)</summary>
    [Id(4)] public int DefaultDeliveryDays { get; set; } = 30;

    /// <summary>Whether purchase requests below the threshold are auto-approved. (.06)</summary>
    [Id(5)] public bool IsAutoApprovalEnabled { get; set; }

    /// <summary>Dollar threshold for auto-approval. (.07)</summary>
    [Id(6)] public decimal AutoApprovalThreshold { get; set; }

    /// <summary>Prefix used when generating purchase order numbers (e.g. "549-25-"). (.08)</summary>
    [Id(7)] public string PONumberPrefix { get; set; } = string.Empty;

    /// <summary>Whether the IFCAP module is active at this site. (.09)</summary>
    [Id(8)] public bool IsActive { get; set; } = true;

    /// <summary>User ID of the last person to update site parameters. (.10)</summary>
    [Id(9)] public string? UpdatedByUserId { get; set; }

    /// <summary>Date site parameters were last updated. (.11)</summary>
    [Id(10)] public DateTime? LastUpdatedDate { get; set; }
}
