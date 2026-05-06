// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

// ─── Fee vendor type ───────────────────────────────────────────────────────────

/// <summary>Classification of a fee basis community care vendor.</summary>
[GenerateSerializer]
public enum FeeVendorType
{
    /// <summary>Individual provider (physician, nurse practitioner, etc.).</summary>
    Individual = 0,

    /// <summary>Organization (hospital, clinic, group practice, pharmacy, etc.).</summary>
    Organization = 1,
}

// ─── Fee Vendor — VistA File #162.5 FEE BASIS VENDOR ─────────────────────────

/// <summary>
/// A community care provider or organization authorized to receive fee basis
/// payments from the VA (VistA File #162.5 FEE BASIS VENDOR).
/// Managed by FBSVPR.m MUMPS routines.
/// </summary>
[GenerateSerializer]
public class FeeVendorState
{
    /// <summary>Unique identifier for this vendor (Guid).</summary>
    [Id(0)] public string VendorId { get; set; } = string.Empty;

    /// <summary>Vendor display name.</summary>
    [Id(1)] public string VendorName { get; set; } = string.Empty;

    /// <summary>Whether this is an individual provider or an organization.</summary>
    [Id(2)] public FeeVendorType VendorType { get; set; }

    /// <summary>Medical specialty code (e.g., 207Q00000X — Family Medicine) (optional).</summary>
    [Id(3)] public string? SpecialtyCode { get; set; }

    /// <summary>Human-readable specialty name (optional).</summary>
    [Id(4)] public string? SpecialtyName { get; set; }

    /// <summary>National Provider Identifier (10-digit NPI) (optional).</summary>
    [Id(5)] public string? NPI { get; set; }

    /// <summary>Federal Tax Identification Number / EIN (optional).</summary>
    [Id(6)] public string? TaxId { get; set; }

    /// <summary>Street address (optional).</summary>
    [Id(7)] public string? Address { get; set; }

    /// <summary>Primary contact phone number (optional).</summary>
    [Id(8)] public string? Phone { get; set; }

    /// <summary>Fax number (optional).</summary>
    [Id(9)] public string? Fax { get; set; }

    /// <summary>Whether this vendor is currently active and eligible to receive referrals.</summary>
    [Id(10)] public bool IsActive { get; set; } = true;

    /// <summary>VA contract or agreement number (optional).</summary>
    [Id(11)] public string? ContractNumber { get; set; }

    /// <summary>Contract effective start date (optional).</summary>
    [Id(12)] public DateTime? ContractStartDate { get; set; }

    /// <summary>Contract expiration date (optional).</summary>
    [Id(13)] public DateTime? ContractEndDate { get; set; }

    /// <summary>Free-text notes about this vendor.</summary>
    [Id(14)] public string? Notes { get; set; }

    /// <summary>UTC timestamp when this record was first created.</summary>
    [Id(15)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recent modification.</summary>
    [Id(16)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
