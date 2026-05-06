// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for an IFCAP procurement vendor.
/// Corresponds to VistA File #440 (VENDOR).
/// Grain key: "IFCAP-VENDOR:{vendorId}"
/// </summary>
[GenerateSerializer]
public class IfcapVendorState
{
    /// <summary>Unique identifier for this vendor. (.01)</summary>
    [Id(0)] public string VendorId { get; set; } = string.Empty;

    /// <summary>Vendor company or individual name. (.02)</summary>
    [Id(1)] public string Name { get; set; } = string.Empty;

    /// <summary>VA-assigned vendor number. (.03)</summary>
    [Id(2)] public string VendorNumber { get; set; } = string.Empty;

    /// <summary>Street address. (.04)</summary>
    [Id(3)] public string Address { get; set; } = string.Empty;

    /// <summary>City. (.05)</summary>
    [Id(4)] public string City { get; set; } = string.Empty;

    /// <summary>State abbreviation. (.06)</summary>
    [Id(5)] public string State { get; set; } = string.Empty;

    /// <summary>ZIP code. (.07)</summary>
    [Id(6)] public string ZipCode { get; set; } = string.Empty;

    /// <summary>Phone number. (.08)</summary>
    [Id(7)] public string? Phone { get; set; }

    /// <summary>Fax number. (.09)</summary>
    [Id(8)] public string? Fax { get; set; }

    /// <summary>Email address. (.10)</summary>
    [Id(9)] public string? Email { get; set; }

    /// <summary>Whether this vendor is currently active. (.11)</summary>
    [Id(10)] public bool IsActive { get; set; } = true;

    /// <summary>Small Business Administration designation. (.12)</summary>
    [Id(11)] public bool IsSmallBusiness { get; set; }

    /// <summary>Woman-owned small business designation. (.13)</summary>
    [Id(12)] public bool IsWomanOwned { get; set; }

    /// <summary>Service-disabled veteran-owned small business designation. (.14)</summary>
    [Id(13)] public bool IsVeteranOwned { get; set; }

    /// <summary>DUNS/SAM.gov identifier. (.15)</summary>
    [Id(14)] public string? DUNS { get; set; }

    /// <summary>Primary contact name at vendor. (.16)</summary>
    [Id(15)] public string? ContactName { get; set; }

    /// <summary>Date the vendor record was created.</summary>
    [Id(16)] public DateTime CreatedDate { get; set; }

    /// <summary>Date the vendor record was last modified.</summary>
    [Id(17)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Lightweight summary of a vendor for index queries.</summary>
[GenerateSerializer]
public record IfcapVendorIndexEntry(
    [property: Id(0)] string VendorId,
    [property: Id(1)] string Name,
    [property: Id(2)] string VendorNumber,
    [property: Id(3)] bool IsActive,
    [property: Id(4)] bool IsSmallBusiness,
    [property: Id(5)] bool IsVeteranOwned);

/// <summary>Global singleton index state for all IFCAP vendors.</summary>
[GenerateSerializer]
public class IfcapVendorIndexState
{
    [Id(0)] public List<IfcapVendorIndexEntry> Entries { get; set; } = new();
}
