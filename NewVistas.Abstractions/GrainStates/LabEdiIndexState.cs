// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the Lab EDI Index grain. Singleton index of all reference lab
/// configurations and EDI order tracking across the system.
/// </summary>
[GenerateSerializer]
public class LabEdiIndexState
{
    /// <summary>
    /// All registered reference laboratory summaries
    /// </summary>
    [Id(0)]
    public List<LabEdiLabSummary> ReferenceLabs { get; set; } = new();

    /// <summary>
    /// EDI orders indexed by patient ID (most recent first per patient)
    /// </summary>
    [Id(1)]
    public Dictionary<string, List<LabEdiOrderSummary>> OrdersByPatient { get; set; } = new();

    /// <summary>
    /// Total EDI orders across all reference labs (lifetime counter)
    /// </summary>
    [Id(2)]
    public long TotalOrders { get; set; }

    /// <summary>
    /// Total EDI results received across all reference labs (lifetime counter)
    /// </summary>
    [Id(3)]
    public long TotalResults { get; set; }
}

/// <summary>
/// Summary of a reference laboratory for index/list display.
/// </summary>
[GenerateSerializer]
public class LabEdiLabSummary
{
    /// <summary>
    /// Reference lab identifier
    /// </summary>
    [Id(0)]
    public string ReferenceLabId { get; set; } = string.Empty;

    /// <summary>
    /// Reference lab display name
    /// </summary>
    [Id(1)]
    public string LabName { get; set; } = string.Empty;

    /// <summary>
    /// CLIA number
    /// </summary>
    [Id(2)]
    public string CliaNumber { get; set; } = string.Empty;

    /// <summary>
    /// Connection type (TCP/MLLP, SFTP, HTTPS)
    /// </summary>
    [Id(3)]
    public string ConnectionType { get; set; } = string.Empty;

    /// <summary>
    /// Whether the reference lab is currently active
    /// </summary>
    [Id(4)]
    public bool IsActive { get; set; }

    /// <summary>
    /// Total orders sent to this lab
    /// </summary>
    [Id(5)]
    public long TotalOrders { get; set; }

    /// <summary>
    /// Total results received from this lab
    /// </summary>
    [Id(6)]
    public long TotalResults { get; set; }
}

/// <summary>
/// Summary of an EDI order for index/list display.
/// </summary>
[GenerateSerializer]
public class LabEdiOrderSummary
{
    /// <summary>
    /// Order identifier
    /// </summary>
    [Id(0)]
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// Patient identifier
    /// </summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Patient display name
    /// </summary>
    [Id(2)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>
    /// Reference lab name
    /// </summary>
    [Id(3)]
    public string ReferenceLab { get; set; } = string.Empty;

    /// <summary>
    /// Reference lab identifier
    /// </summary>
    [Id(4)]
    public string ReferenceLabId { get; set; } = string.Empty;

    /// <summary>
    /// Current order status
    /// </summary>
    [Id(5)]
    public LabEdiOrderStatus Status { get; set; }

    /// <summary>
    /// Number of tests in this order
    /// </summary>
    [Id(6)]
    public int TestCount { get; set; }

    /// <summary>
    /// Date/time order was created
    /// </summary>
    [Id(7)]
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Date/time order was last modified
    /// </summary>
    [Id(8)]
    public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Aggregate dashboard statistics for the Lab EDI subsystem.
/// </summary>
[GenerateSerializer]
public class LabEdiDashboard
{
    /// <summary>
    /// Total active reference labs
    /// </summary>
    [Id(0)]
    public int ActiveReferenceLabs { get; set; }

    /// <summary>
    /// Total EDI orders (lifetime)
    /// </summary>
    [Id(1)]
    public long TotalOrders { get; set; }

    /// <summary>
    /// Total EDI results received (lifetime)
    /// </summary>
    [Id(2)]
    public long TotalResults { get; set; }

    /// <summary>
    /// Number of orders currently in Draft status
    /// </summary>
    [Id(3)]
    public int PendingOrders { get; set; }

    /// <summary>
    /// Number of orders currently in Transmitted status (awaiting results)
    /// </summary>
    [Id(4)]
    public int AwaitingResults { get; set; }

    /// <summary>
    /// Reference lab summaries for per-lab breakdown
    /// </summary>
    [Id(5)]
    public List<LabEdiLabSummary> LabSummaries { get; set; } = new();
}
