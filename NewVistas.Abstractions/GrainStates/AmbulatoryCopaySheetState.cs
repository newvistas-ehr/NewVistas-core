// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// A single item on the ambulatory copay check-off checklist.
/// Based on VistA File #350.71 AMBULATORY SURG. CHECK-OFF SHEET PRINT FIELDS.
/// </summary>
[GenerateSerializer]
public record CopayChecklistItem
{
    /// <summary>Item/procedure code to be checked off.</summary>
    [Id(0)] public string ItemCode { get; init; } = string.Empty;

    /// <summary>Human-readable description of the item.</summary>
    [Id(1)] public string ItemDescription { get; init; } = string.Empty;

    /// <summary>Whether this item has been marked as billable on the sheet.</summary>
    [Id(2)] public bool IsChecked { get; init; }

    /// <summary>User ID of the person who checked this item. Null if not yet checked.</summary>
    [Id(3)] public string? CheckedByUserId { get; init; }

    /// <summary>Display name of the person who checked this item.</summary>
    [Id(4)] public string? CheckedByUserName { get; init; }

    /// <summary>Date and time this item was checked.</summary>
    [Id(5)] public DateTime? CheckedDateTime { get; init; }
}

/// <summary>
/// Ambulatory Copay Check-Off Sheet record (VistA File #350.7 AMBULATORY CHECK-OFF SHEET).
/// Documents which clinical procedures and services were delivered during an outpatient visit,
/// used to ensure all billable items are captured for copay generation.
/// Grain key: "IB-SHEET:{guid}"
/// </summary>
[GenerateSerializer]
public class AmbulatoryCopaySheetState
{
    /// <summary>Unique identifier for this check-off sheet (GUID string). (.01)</summary>
    [Id(0)] public string SheetId { get; set; } = string.Empty;

    /// <summary>Patient DFN/identifier this sheet is for. (.02)</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Encounter or visit identifier this sheet is linked to. (.03)</summary>
    [Id(2)] public string? EncounterId { get; set; }

    /// <summary>Date of the ambulatory visit. (.04)</summary>
    [Id(3)] public DateTime VisitDate { get; set; } = DateTime.UtcNow;

    /// <summary>Clinic location identifier where the visit occurred. (.05)</summary>
    [Id(4)] public string? ClinicId { get; set; }

    /// <summary>Display name of the clinic. (.06)</summary>
    [Id(5)] public string? ClinicName { get; set; }

    /// <summary>List of items on this check-off sheet with their completion status.</summary>
    [Id(6)] public List<CopayChecklistItem> ChecklistItems { get; set; } = new();

    /// <summary>Whether the sheet has been marked complete (all applicable items reviewed). (.07)</summary>
    [Id(7)] public bool IsComplete { get; set; }

    /// <summary>User ID of the person who completed the sheet.</summary>
    [Id(8)] public string? CompletedByUserId { get; set; }

    /// <summary>Display name of the person who completed the sheet.</summary>
    [Id(9)] public string? CompletedByUserName { get; set; }

    /// <summary>Date and time the sheet was marked complete.</summary>
    [Id(10)] public DateTime? CompletedDateTime { get; set; }

    /// <summary>Count of items checked as billable on this sheet.</summary>
    [Id(11)] public int TotalBillableItems { get; set; }

    /// <summary>IDs of billing actions that were generated from this sheet.</summary>
    [Id(12)] public List<string> BillingActionIds { get; set; } = new();

    /// <summary>Free-text notes about this check-off sheet.</summary>
    [Id(13)] public string? Notes { get; set; }

    /// <summary>Date this grain record was first created.</summary>
    [Id(14)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date this grain record was last modified.</summary>
    [Id(15)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
