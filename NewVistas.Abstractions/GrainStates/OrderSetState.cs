// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for Order Set (Quick Order) templates.
/// Maps to VistA ORDER DIALOG file (#101.41) and QUICK ORDER (#101.44).
/// Order sets group related orders for efficient placement (e.g., "Admission Orders",
/// "Post-Op Orders", "CHF Protocol"). Each item in the set is a pre-configured
/// order template that can be placed with a single action.
/// </summary>
[GenerateSerializer]
public class OrderSetState
{
    /// <summary>Order set unique identifier.</summary>
    [Id(0)] public string OrderSetId { get; set; } = string.Empty;

    /// <summary>Display name (e.g., "ADMISSION ORDERS - MEDICINE").</summary>
    [Id(1)] public string Name { get; set; } = string.Empty;

    /// <summary>Descriptive text for the order set.</summary>
    [Id(2)] public string Description { get; set; } = string.Empty;

    /// <summary>Category: "ADMISSION", "TRANSFER", "DISCHARGE", "PROTOCOL", "GENERAL".</summary>
    [Id(3)] public string Category { get; set; } = string.Empty;

    /// <summary>Service/specialty this set belongs to (e.g., "MEDICINE", "SURGERY").</summary>
    [Id(4)] public string ServiceSection { get; set; } = string.Empty;

    /// <summary>Whether this order set is active and available for use.</summary>
    [Id(5)] public bool IsActive { get; set; } = true;

    /// <summary>Individual order templates within this set.</summary>
    [Id(6)] public List<OrderTemplate> Items { get; set; } = new();

    /// <summary>Creator of the order set.</summary>
    [Id(7)] public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Date created.</summary>
    [Id(8)] public DateTime CreatedDate { get; set; }

    /// <summary>Last modified date.</summary>
    [Id(9)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// A single pre-configured order template within an order set.
/// Mirrors VistA QUICK ORDER (#101.44) — pre-populated dialog with defaults.
/// </summary>
[GenerateSerializer]
public class OrderTemplate
{
    /// <summary>Unique template ID within the set.</summary>
    [Id(0)] public string TemplateId { get; set; } = string.Empty;

    /// <summary>Order type: "Lab", "Pharmacy", "Radiology", "Consult", "Nursing", "Diet", "Vitals".</summary>
    [Id(1)] public string OrderType { get; set; } = string.Empty;

    /// <summary>Orderable item text (e.g., "CBC WITH DIFFERENTIAL").</summary>
    [Id(2)] public string OrderableItem { get; set; } = string.Empty;

    /// <summary>Orderable item ID reference.</summary>
    [Id(3)] public string? OrderableItemId { get; set; }

    /// <summary>Default urgency: "ROUTINE", "URGENT", "STAT".</summary>
    [Id(4)] public string Urgency { get; set; } = "ROUTINE";

    /// <summary>Pre-filled instructions text.</summary>
    [Id(5)] public string? Instructions { get; set; }

    /// <summary>Display sequence within the order set.</summary>
    [Id(6)] public int Sequence { get; set; }

    /// <summary>Whether this item is selected by default when the set is opened.</summary>
    [Id(7)] public bool IsDefaultSelected { get; set; } = true;
}

/// <summary>
/// Lightweight index entry for order set searches.
/// </summary>
[GenerateSerializer]
public class OrderSetEntry
{
    /// <summary>Order set ID.</summary>
    [Id(0)] public string OrderSetId { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    [Id(1)] public string Name { get; set; } = string.Empty;

    /// <summary>Category.</summary>
    [Id(2)] public string Category { get; set; } = string.Empty;

    /// <summary>Service section.</summary>
    [Id(3)] public string ServiceSection { get; set; } = string.Empty;

    /// <summary>Whether active.</summary>
    [Id(4)] public bool IsActive { get; set; } = true;

    /// <summary>Number of order templates in this set.</summary>
    [Id(5)] public int ItemCount { get; set; }
}

/// <summary>
/// State for the order set index singleton.
/// </summary>
[GenerateSerializer]
public class OrderSetIndexState
{
    /// <summary>All registered order sets.</summary>
    [Id(0)] public List<OrderSetEntry> OrderSets { get; set; } = new();
}

/// <summary>
/// Result of an order check — a single warning or alert.
/// Mirrors VistA Order Checking (ORCHECK.m) alerts.
/// </summary>
[GenerateSerializer]
public class OrderCheckResult
{
    /// <summary>Check type: "DRUG_DRUG", "DRUG_ALLERGY", "DUPLICATE", "CRITICAL_LAB".</summary>
    [Id(0)] public string CheckType { get; set; } = string.Empty;

    /// <summary>Severity: "HIGH" (must acknowledge), "MODERATE" (warning), "LOW" (informational).</summary>
    [Id(1)] public string Severity { get; set; } = string.Empty;

    /// <summary>Human-readable message describing the finding.</summary>
    [Id(2)] public string Message { get; set; } = string.Empty;

    /// <summary>The order being checked (new order text).</summary>
    [Id(3)] public string OrderText { get; set; } = string.Empty;

    /// <summary>The conflicting item (existing drug/allergy/order).</summary>
    [Id(4)] public string? ConflictingItem { get; set; }
}
