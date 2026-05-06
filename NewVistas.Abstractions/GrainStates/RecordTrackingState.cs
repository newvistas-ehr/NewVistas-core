// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>Physical location type for a paper chart. VistA File #190 (.05)</summary>
public enum ChartLocationType
{
    FileRoom,
    ClinicOutpatient,
    InpatientWard,
    ProviderOffice,
    ROI,
    Scanning,
    Radiology,
    Lab,
    Administrative,
    Lost,
    Destroyed,
    Other
}

/// <summary>Type of chart movement action. VistA File #190.1</summary>
public enum ChartMovementAction
{
    Initialized,
    CheckedOut,
    CheckedIn,
    Transferred,
    Requested,
    Located,
    Lost,
    Found,
    VolumeAdded
}

/// <summary>Reason a chart is being requested. VistA File #190.2 (.03)</summary>
public enum ChartRequestType
{
    PatientCare,
    ROI,
    Research,
    Administrative,
    Legal,
    QualityReview,
    Other
}

/// <summary>Current status of a chart request. VistA File #190.2 (.04)</summary>
public enum ChartRequestStatus
{
    Pending,
    Pulled,
    InTransit,
    Delivered,
    NotFound,
    Cancelled
}

/// <summary>Priority of the chart request. VistA File #190.2 (.05)</summary>
public enum ChartRequestPriority
{
    Routine,
    Urgent,
    STAT
}

/// <summary>An individual volume of a paper chart.</summary>
[GenerateSerializer]
public class ChartVolume
{
    /// <summary>Volume identifier.</summary>
    [Id(0)] public string VolumeId { get; set; } = string.Empty;

    /// <summary>Volume number (1, 2, 3...).</summary>
    [Id(1)] public int VolumeNumber { get; set; }

    /// <summary>Date range of records in this volume (e.g., "01/2015 - 12/2019").</summary>
    [Id(2)] public string DateRange { get; set; } = string.Empty;

    /// <summary>Whether this volume is currently active (has the most recent records).</summary>
    [Id(3)] public bool IsActive { get; set; }

    /// <summary>Current physical location of this volume.</summary>
    [Id(4)] public string CurrentLocation { get; set; } = string.Empty;
}

/// <summary>A single chart movement or action in the audit trail.</summary>
[GenerateSerializer]
public class ChartMovement
{
    /// <summary>Unique movement record identifier.</summary>
    [Id(0)] public string MovementId { get; set; } = string.Empty;

    /// <summary>Date and time of the movement.</summary>
    [Id(1)] public DateTime MovementDate { get; set; }

    /// <summary>Type of action performed.</summary>
    [Id(2)] public ChartMovementAction Action { get; set; }

    /// <summary>Location chart moved from.</summary>
    [Id(3)] public string FromLocation { get; set; } = string.Empty;

    /// <summary>Location chart moved to.</summary>
    [Id(4)] public string ToLocation { get; set; } = string.Empty;

    /// <summary>ID of the person who borrowed/received the chart.</summary>
    [Id(5)] public string BorrowerId { get; set; } = string.Empty;

    /// <summary>Name of the person who borrowed/received the chart.</summary>
    [Id(6)] public string BorrowerName { get; set; } = string.Empty;

    /// <summary>Staff member who performed the movement action.</summary>
    [Id(7)] public string HandledBy { get; set; } = string.Empty;

    /// <summary>Additional notes about this movement.</summary>
    [Id(8)] public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Physical paper chart record for a patient.
/// VistA File #190 (RECORD TRACKING). RTOUT.m, RTIN.m
/// </summary>
[GenerateSerializer]
public class ChartState
{
    /// <summary>Patient file number. VistA File #190 (.01)</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name. VistA File #190 (.02)</summary>
    [Id(1)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Chart/record number (may differ from patient ID in some facilities). VistA File #190 (.03)</summary>
    [Id(2)] public string ChartNumber { get; set; } = string.Empty;

    /// <summary>Current physical location (clinic, ward, office). VistA File #190 (.04)</summary>
    [Id(3)] public string CurrentLocation { get; set; } = string.Empty;

    /// <summary>Type of current location. VistA File #190 (.05)</summary>
    [Id(4)] public ChartLocationType CurrentLocationType { get; set; }

    /// <summary>Whether the chart is currently checked out (not in file room). VistA File #190 (.06)</summary>
    [Id(5)] public bool IsCheckedOut { get; set; }

    /// <summary>ID of the current borrower. VistA File #190 (.07)</summary>
    [Id(6)] public string CurrentBorrowerId { get; set; } = string.Empty;

    /// <summary>Name of the current borrower. VistA File #190 (.08)</summary>
    [Id(7)] public string CurrentBorrowerName { get; set; } = string.Empty;

    /// <summary>Date the chart was last checked out. VistA File #190 (.09)</summary>
    [Id(8)] public DateTime? CheckOutDate { get; set; }

    /// <summary>Expected return date. VistA File #190 (.10)</summary>
    [Id(9)] public DateTime? ExpectedReturnDate { get; set; }

    /// <summary>Whether a chart request is currently pending. VistA File #190 (.11)</summary>
    [Id(10)] public bool IsOnRequest { get; set; }

    /// <summary>Whether the chart has been marked as lost. VistA File #190 (.12)</summary>
    [Id(11)] public bool IsLost { get; set; }

    /// <summary>Chart volumes. VistA File #190.1</summary>
    [Id(12)] public List<ChartVolume> Volumes { get; set; } = new();

    /// <summary>Full movement/audit history. VistA File #190.2</summary>
    [Id(13)] public List<ChartMovement> MovementHistory { get; set; } = new();

    /// <summary>Home/default file room location. VistA File #190 (.13)</summary>
    [Id(14)] public string HomeLocation { get; set; } = string.Empty;

    /// <summary>Date chart was created/initialized in the tracking system.</summary>
    [Id(15)] public DateTime InitializedDate { get; set; }

    /// <summary>Last modified timestamp.</summary>
    [Id(16)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Summary entry for chart index queries.</summary>
[GenerateSerializer]
public class ChartIndexEntry
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public string PatientName { get; set; } = string.Empty;
    [Id(2)] public string ChartNumber { get; set; } = string.Empty;
    [Id(3)] public string CurrentLocation { get; set; } = string.Empty;
    [Id(4)] public ChartLocationType CurrentLocationType { get; set; }
    [Id(5)] public bool IsCheckedOut { get; set; }
    [Id(6)] public bool IsOnRequest { get; set; }
    [Id(7)] public bool IsLost { get; set; }
    [Id(8)] public DateTime? CheckOutDate { get; set; }
    [Id(9)] public string CurrentBorrowerName { get; set; } = string.Empty;
    [Id(10)] public DateTime? ExpectedReturnDate { get; set; }
    [Id(11)] public int VolumeCount { get; set; }
}

/// <summary>
/// A request to pull and deliver a paper chart.
/// VistA File #190.2 (RECORD TRACKING REQUEST). RTREQ.m
/// </summary>
[GenerateSerializer]
public class ChartRequestState
{
    /// <summary>Unique request identifier.</summary>
    [Id(0)] public string RequestId { get; set; } = string.Empty;

    /// <summary>Patient whose chart is requested.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name.</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>ID of the staff member requesting the chart.</summary>
    [Id(3)] public string RequestedById { get; set; } = string.Empty;

    /// <summary>Name of the staff member requesting the chart.</summary>
    [Id(4)] public string RequestedByName { get; set; } = string.Empty;

    /// <summary>Date and time the request was created.</summary>
    [Id(5)] public DateTime RequestDate { get; set; }

    /// <summary>When the chart is needed by.</summary>
    [Id(6)] public DateTime NeededBy { get; set; }

    /// <summary>Priority of the request.</summary>
    [Id(7)] public ChartRequestPriority Priority { get; set; }

    /// <summary>Where the chart should be delivered.</summary>
    [Id(8)] public string RequestedForLocation { get; set; } = string.Empty;

    /// <summary>Type/reason for the request.</summary>
    [Id(9)] public ChartRequestType RequestType { get; set; }

    /// <summary>Current status of the request.</summary>
    [Id(10)] public ChartRequestStatus Status { get; set; }

    /// <summary>Additional notes or instructions.</summary>
    [Id(11)] public string Notes { get; set; } = string.Empty;

    /// <summary>Date and time the request was fulfilled.</summary>
    [Id(12)] public DateTime? FulfilledDate { get; set; }

    /// <summary>Staff member who pulled/delivered the chart.</summary>
    [Id(13)] public string FulfilledBy { get; set; } = string.Empty;

    /// <summary>Reason for cancellation if cancelled.</summary>
    [Id(14)] public string CancellationReason { get; set; } = string.Empty;

    /// <summary>Last modified timestamp.</summary>
    [Id(15)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Summary entry for chart request index queries.</summary>
[GenerateSerializer]
public class ChartRequestIndexEntry
{
    [Id(0)] public string RequestId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public string PatientName { get; set; } = string.Empty;
    [Id(3)] public string RequestedByName { get; set; } = string.Empty;
    [Id(4)] public DateTime RequestDate { get; set; }
    [Id(5)] public DateTime NeededBy { get; set; }
    [Id(6)] public ChartRequestPriority Priority { get; set; }
    [Id(7)] public ChartRequestStatus Status { get; set; }
    [Id(8)] public string RequestedForLocation { get; set; } = string.Empty;
    [Id(9)] public ChartRequestType RequestType { get; set; }
}
