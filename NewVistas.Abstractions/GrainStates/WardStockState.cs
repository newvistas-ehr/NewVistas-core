// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for a ward stock item — one drug at one ward location.
/// Maps to VistA PSGW (Ward Stock) package.
/// Key: "WARD-STOCK:{wardId}:{drugId}"
/// </summary>
[GenerateSerializer]
public class WardStockItemState
{
    /// <summary>Drug identifier (pointer to Drug File #50).</summary>
    [Id(0)] public string DrugId { get; set; } = string.Empty;

    /// <summary>Drug name.</summary>
    [Id(1)] public string DrugName { get; set; } = string.Empty;

    /// <summary>Ward/location ID.</summary>
    [Id(2)] public string WardId { get; set; } = string.Empty;

    /// <summary>Ward name.</summary>
    [Id(3)] public string WardName { get; set; } = string.Empty;

    /// <summary>Current quantity on hand.</summary>
    [Id(4)] public int QuantityOnHand { get; set; }

    /// <summary>Par level — target quantity to maintain.</summary>
    [Id(5)] public int ParLevel { get; set; }

    /// <summary>Reorder point — triggers replenishment when on-hand drops to or below this.</summary>
    [Id(6)] public int ReorderPoint { get; set; }

    /// <summary>Reorder quantity — amount to request when replenishing.</summary>
    [Id(7)] public int ReorderQuantity { get; set; }

    /// <summary>Unit of measure (TAB, CAP, ML, etc.).</summary>
    [Id(8)] public string UnitOfMeasure { get; set; } = "TAB";

    /// <summary>Whether auto-replenishment is enabled for this item.</summary>
    [Id(9)] public bool AutoReplenishEnabled { get; set; } = true;

    /// <summary>True if a replenishment request is currently pending.</summary>
    [Id(10)] public bool ReplenishmentPending { get; set; }

    /// <summary>Date of last replenishment.</summary>
    [Id(11)] public DateTime? LastReplenishmentDate { get; set; }

    /// <summary>Date of last inventory count.</summary>
    [Id(12)] public DateTime? LastInventoryDate { get; set; }

    /// <summary>Is this a controlled substance.</summary>
    [Id(13)] public bool IsControlledSubstance { get; set; }

    /// <summary>Last modified.</summary>
    [Id(14)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Lightweight ward stock summary for list display.
/// </summary>
[GenerateSerializer]
public class WardStockSummaryEntry
{
    /// <summary>Drug ID.</summary>
    [Id(0)] public string DrugId { get; set; } = string.Empty;

    /// <summary>Drug name.</summary>
    [Id(1)] public string DrugName { get; set; } = string.Empty;

    /// <summary>Current quantity on hand.</summary>
    [Id(2)] public int QuantityOnHand { get; set; }

    /// <summary>Par level.</summary>
    [Id(3)] public int ParLevel { get; set; }

    /// <summary>Reorder point.</summary>
    [Id(4)] public int ReorderPoint { get; set; }

    /// <summary>Unit of measure.</summary>
    [Id(5)] public string UnitOfMeasure { get; set; } = string.Empty;

    /// <summary>Whether below reorder point.</summary>
    [Id(6)] public bool NeedsReplenishment { get; set; }

    /// <summary>Replenishment pending.</summary>
    [Id(7)] public bool ReplenishmentPending { get; set; }

    /// <summary>Is controlled substance.</summary>
    [Id(8)] public bool IsControlledSubstance { get; set; }
}

/// <summary>
/// State for the ward stock index — all drugs stocked at a ward.
/// Key: "WARD-STOCK-INDEX:{wardId}"
/// </summary>
[GenerateSerializer]
public class WardStockIndexState
{
    /// <summary>Ward ID.</summary>
    [Id(0)] public string WardId { get; set; } = string.Empty;

    /// <summary>All stocked items at this ward.</summary>
    [Id(1)] public List<WardStockSummaryEntry> Items { get; set; } = new();
}

/// <summary>
/// Auto-replenishment request record.
/// </summary>
[GenerateSerializer]
public class ReplenishmentRequest
{
    /// <summary>Request ID.</summary>
    [Id(0)] public string RequestId { get; set; } = string.Empty;

    /// <summary>Ward ID.</summary>
    [Id(1)] public string WardId { get; set; } = string.Empty;

    /// <summary>Drug ID.</summary>
    [Id(2)] public string DrugId { get; set; } = string.Empty;

    /// <summary>Drug name.</summary>
    [Id(3)] public string DrugName { get; set; } = string.Empty;

    /// <summary>Quantity requested.</summary>
    [Id(4)] public int QuantityRequested { get; set; }

    /// <summary>Status: PENDING, FILLED, CANCELLED.</summary>
    [Id(5)] public string Status { get; set; } = "PENDING";

    /// <summary>Request date.</summary>
    [Id(6)] public DateTime RequestDate { get; set; }

    /// <summary>Filled date.</summary>
    [Id(7)] public DateTime? FilledDate { get; set; }
}

/// <summary>
/// State for replenishment request log per ward.
/// Key: "WARD-REPLENISH-LOG:{wardId}"
/// </summary>
[GenerateSerializer]
public class WardReplenishmentLogState
{
    /// <summary>Replenishment requests.</summary>
    [Id(0)] public List<ReplenishmentRequest> Requests { get; set; } = new();
}
