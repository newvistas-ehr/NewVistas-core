// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for a CMOP (Consolidated Mail Outpatient Pharmacy) transmission.
/// Maps to VistA PSX package — manages batches of prescriptions sent to
/// a centralized mail-order pharmacy for automated dispensing and shipping.
/// VistA File #550.2 (CMOP Transmission).
/// </summary>
[GenerateSerializer]
public class CmopTransmissionState
{
    /// <summary>Unique transmission identifier.</summary>
    [Id(0)] public string TransmissionId { get; set; } = string.Empty;

    /// <summary>CMOP facility identifier (e.g., "CMOP-LEAVENWORTH", "CMOP-DALLAS").</summary>
    [Id(1)] public string CmopFacilityId { get; set; } = string.Empty;

    /// <summary>CMOP facility name.</summary>
    [Id(2)] public string CmopFacilityName { get; set; } = string.Empty;

    /// <summary>Originating pharmacy site.</summary>
    [Id(3)] public string OriginatingSiteId { get; set; } = string.Empty;

    /// <summary>Transmission status: PENDING, TRANSMITTED, RECEIVED, IN_PROCESS, DISPENSED, SHIPPED, COMPLETED, CANCELLED, ERROR.</summary>
    [Id(4)] public string Status { get; set; } = "PENDING";

    /// <summary>Prescriptions included in this transmission.</summary>
    [Id(5)] public List<CmopPrescriptionEntry> Prescriptions { get; set; } = new();

    /// <summary>Transmission date/time (when batch was sent to CMOP).</summary>
    [Id(6)] public DateTime? TransmittedDate { get; set; }

    /// <summary>Date CMOP acknowledged receipt.</summary>
    [Id(7)] public DateTime? ReceivedDate { get; set; }

    /// <summary>Date CMOP completed dispensing.</summary>
    [Id(8)] public DateTime? DispensedDate { get; set; }

    /// <summary>Date package was shipped to patient.</summary>
    [Id(9)] public DateTime? ShippedDate { get; set; }

    /// <summary>Shipping tracking number.</summary>
    [Id(10)] public string? TrackingNumber { get; set; }

    /// <summary>Shipping carrier (USPS, UPS, FedEx).</summary>
    [Id(11)] public string? ShippingCarrier { get; set; }

    /// <summary>Total prescriptions in this transmission.</summary>
    [Id(12)] public int PrescriptionCount { get; set; }

    /// <summary>Number of items successfully dispensed.</summary>
    [Id(13)] public int DispensedCount { get; set; }

    /// <summary>Number of items returned/rejected by CMOP.</summary>
    [Id(14)] public int RejectedCount { get; set; }

    /// <summary>Error or rejection reason if applicable.</summary>
    [Id(15)] public string? ErrorMessage { get; set; }

    /// <summary>Created date.</summary>
    [Id(16)] public DateTime CreatedDate { get; set; }

    /// <summary>Last modified date.</summary>
    [Id(17)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Individual prescription entry within a CMOP transmission.
/// </summary>
[GenerateSerializer]
public class CmopPrescriptionEntry
{
    /// <summary>Prescription ID (pointer to File #52).</summary>
    [Id(0)] public string PrescriptionId { get; set; } = string.Empty;

    /// <summary>Patient ID.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name (cached for display).</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Drug name.</summary>
    [Id(3)] public string DrugName { get; set; } = string.Empty;

    /// <summary>Rx number on label.</summary>
    [Id(4)] public string? RxNumber { get; set; }

    /// <summary>Quantity to dispense.</summary>
    [Id(5)] public int? Quantity { get; set; }

    /// <summary>Days supply.</summary>
    [Id(6)] public int? DaysSupply { get; set; }

    /// <summary>Fill type: ORIGINAL, REFILL, PARTIAL.</summary>
    [Id(7)] public string FillType { get; set; } = "ORIGINAL";

    /// <summary>Item status: PENDING, DISPENSED, REJECTED, RETURNED.</summary>
    [Id(8)] public string ItemStatus { get; set; } = "PENDING";

    /// <summary>Rejection/return reason if applicable.</summary>
    [Id(9)] public string? RejectReason { get; set; }

    /// <summary>NDC code dispensed by CMOP.</summary>
    [Id(10)] public string? NdcDispensed { get; set; }
}

/// <summary>
/// State for the CMOP suspense queue — prescriptions queued for mail-order fulfillment.
/// Maps to VistA File #52.5 (Rx Suspense).
/// Keyed per originating site: "CMOP-SUSPENSE:{siteId}".
/// </summary>
[GenerateSerializer]
public class CmopSuspenseState
{
    /// <summary>Site ID this suspense queue belongs to.</summary>
    [Id(0)] public string SiteId { get; set; } = string.Empty;

    /// <summary>Prescriptions queued for CMOP transmission.</summary>
    [Id(1)] public List<CmopSuspenseEntry> QueuedPrescriptions { get; set; } = new();

    /// <summary>Auto-transmit enabled.</summary>
    [Id(2)] public bool AutoTransmitEnabled { get; set; } = true;

    /// <summary>Scheduled auto-transmit time (hour of day, UTC).</summary>
    [Id(3)] public int AutoTransmitHour { get; set; } = 14;

    /// <summary>Last auto-transmit date.</summary>
    [Id(4)] public DateTime? LastAutoTransmitDate { get; set; }
}

/// <summary>
/// Entry in the CMOP suspense queue awaiting transmission.
/// </summary>
[GenerateSerializer]
public class CmopSuspenseEntry
{
    /// <summary>Prescription ID.</summary>
    [Id(0)] public string PrescriptionId { get; set; } = string.Empty;

    /// <summary>Patient ID.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name.</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Drug name.</summary>
    [Id(3)] public string DrugName { get; set; } = string.Empty;

    /// <summary>Rx number.</summary>
    [Id(4)] public string? RxNumber { get; set; }

    /// <summary>Quantity.</summary>
    [Id(5)] public int? Quantity { get; set; }

    /// <summary>Days supply.</summary>
    [Id(6)] public int? DaysSupply { get; set; }

    /// <summary>Fill type: ORIGINAL, REFILL.</summary>
    [Id(7)] public string FillType { get; set; } = "ORIGINAL";

    /// <summary>Date added to suspense queue.</summary>
    [Id(8)] public DateTime QueuedDate { get; set; }

    /// <summary>Priority: ROUTINE, URGENT.</summary>
    [Id(9)] public string Priority { get; set; } = "ROUTINE";

    /// <summary>Mailing address for the patient.</summary>
    [Id(10)] public string? MailingAddress { get; set; }
}

/// <summary>
/// State for the CMOP transmission index — tracks all transmissions for a site.
/// Keyed: "CMOP-TX-INDEX:{siteId}".
/// </summary>
[GenerateSerializer]
public class CmopTransmissionIndexState
{
    /// <summary>All transmission summaries for this site.</summary>
    [Id(0)] public List<CmopTransmissionSummary> Transmissions { get; set; } = new();
}

/// <summary>
/// Lightweight transmission summary for index display.
/// </summary>
[GenerateSerializer]
public class CmopTransmissionSummary
{
    /// <summary>Transmission ID.</summary>
    [Id(0)] public string TransmissionId { get; set; } = string.Empty;

    /// <summary>CMOP facility name.</summary>
    [Id(1)] public string CmopFacilityName { get; set; } = string.Empty;

    /// <summary>Status.</summary>
    [Id(2)] public string Status { get; set; } = string.Empty;

    /// <summary>Transmitted date.</summary>
    [Id(3)] public DateTime? TransmittedDate { get; set; }

    /// <summary>Prescription count.</summary>
    [Id(4)] public int PrescriptionCount { get; set; }

    /// <summary>Dispensed count.</summary>
    [Id(5)] public int DispensedCount { get; set; }

    /// <summary>Rejected count.</summary>
    [Id(6)] public int RejectedCount { get; set; }

    /// <summary>Tracking number.</summary>
    [Id(7)] public string? TrackingNumber { get; set; }
}
