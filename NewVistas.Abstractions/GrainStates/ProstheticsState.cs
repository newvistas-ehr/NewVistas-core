// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents a single prosthetics item entry embedded in the patient grain.
/// Based on VistA PROSTHETICS file (#669.1).
/// No PatientId — the patient grain owns this data.
/// </summary>
[GenerateSerializer]
public class ProstheticsEntry
{
    [Id(0)]
    public string ProstheticsId { get; set; } = string.Empty;

    /// <summary>
    /// Item Description (.01)
    /// </summary>
    [Id(2)]
    public string ItemDescription { get; set; } = string.Empty;

    /// <summary>
    /// HCPCS Code � Healthcare Common Procedure Coding System
    /// </summary>
    [Id(3)]
    public string? HcpcsCode { get; set; }

    /// <summary>
    /// Item Category � SENSORY AIDS, ORTHOTIC, PROSTHETIC, HOME OXYGEN, etc.
    /// </summary>
    [Id(4)]
    public string? ItemCategory { get; set; }

    /// <summary>
    /// Status � ISSUED, RETURNED, REPAIRED, REPLACED, ON ORDER
    /// </summary>
    [Id(5)]
    public string Status { get; set; } = "ON ORDER";

    /// <summary>
    /// Date Issued
    /// </summary>
    [Id(6)]
    public DateTime? DateIssued { get; set; }

    /// <summary>
    /// Date Returned
    /// </summary>
    [Id(7)]
    public DateTime? DateReturned { get; set; }

    /// <summary>
    /// Quantity
    /// </summary>
    [Id(8)]
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Cost
    /// </summary>
    [Id(9)]
    public decimal? Cost { get; set; }

    /// <summary>
    /// Vendor
    /// </summary>
    [Id(10)]
    public string? Vendor { get; set; }

    /// <summary>
    /// Serial Number
    /// </summary>
    [Id(11)]
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Ordering Provider
    /// </summary>
    [Id(12)]
    public string? ProviderId { get; set; }

    [Id(13)]
    public string? ProviderName { get; set; }

    [Id(14)]
    public string? LocationId { get; set; }

    [Id(15)]
    public string? LocationName { get; set; }

    [Id(16)]
    public string? OrderId { get; set; }

    /// <summary>
    /// Service Connected � whether item is related to a SC condition
    /// </summary>
    [Id(17)]
    public bool IsServiceConnected { get; set; }

    [Id(18)]
    public string? Comments { get; set; }

    [Id(19)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(20)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Vendor ID
    /// </summary>
    [Id(21)]
    public string? VendorId { get; set; }

    /// <summary>
    /// Warranty Start Date
    /// </summary>
    [Id(22)]
    public DateTime? WarrantyStartDate { get; set; }

    /// <summary>
    /// Warranty End Date
    /// </summary>
    [Id(23)]
    public DateTime? WarrantyEndDate { get; set; }

    /// <summary>
    /// Delivery Date
    /// </summary>
    [Id(24)]
    public DateTime? DeliveryDate { get; set; }

    /// <summary>
    /// Delivery Method — IN_PERSON, MAIL, SHIPPING
    /// </summary>
    [Id(25)]
    public string? DeliveryMethod { get; set; }

    /// <summary>
    /// Fitting/adjustment notes
    /// </summary>
    [Id(26)]
    public string? FittingNotes { get; set; }

    /// <summary>
    /// Fitting Date
    /// </summary>
    [Id(27)]
    public DateTime? FittingDate { get; set; }

    /// <summary>
    /// Name of person who performed fitting
    /// </summary>
    [Id(28)]
    public string? FittedByName { get; set; }

    /// <summary>
    /// Patient satisfaction rating (1-5)
    /// </summary>
    [Id(29)]
    public int? PatientSatisfaction { get; set; }

    /// <summary>
    /// Next scheduled maintenance date
    /// </summary>
    [Id(30)]
    public DateTime? NextMaintenanceDate { get; set; }

    /// <summary>
    /// Maintenance notes
    /// </summary>
    [Id(31)]
    public string? MaintenanceNotes { get; set; }

    /// <summary>
    /// Shipping tracking number
    /// </summary>
    [Id(32)]
    public string? TrackingNumber { get; set; }

    /// <summary>
    /// Maintenance history log
    /// </summary>
    [Id(33)]
    public List<ProstheticsMaintenanceRecord> MaintenanceHistory { get; set; } = new();
}

/// <summary>
/// Record of a prosthetics maintenance event
/// </summary>
[GenerateSerializer]
public class ProstheticsMaintenanceRecord
{
    /// <summary>
    /// Date maintenance was performed
    /// </summary>
    [Id(0)]
    public DateTime MaintenanceDate { get; set; }

    /// <summary>
    /// Type of maintenance — ROUTINE, REPAIR, ADJUSTMENT
    /// </summary>
    [Id(1)]
    public string MaintenanceType { get; set; } = string.Empty;

    /// <summary>
    /// Technician who performed maintenance
    /// </summary>
    [Id(2)]
    public string? TechnicianName { get; set; }

    /// <summary>
    /// Notes about the maintenance
    /// </summary>
    [Id(3)]
    public string? Notes { get; set; }

    /// <summary>
    /// Cost of maintenance
    /// </summary>
    [Id(4)]
    public decimal? Cost { get; set; }
}
