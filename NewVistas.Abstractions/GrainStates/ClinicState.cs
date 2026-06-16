// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents a clinic/hospital location.
/// VistA HOSPITAL LOCATION file (#44).
/// Fields map to: NAME (.01), TYPE (3), STOP CODE NUMBER (8), TELEPHONE (99)
/// </summary>
[GenerateSerializer]
public class ClinicState
{
    /// <summary>
    /// Clinic IEN — unique identifier
    /// </summary>
    [Id(0)]
    public string ClinicId { get; set; } = string.Empty;

    /// <summary>
    /// Clinic Name (.01)
    /// </summary>
    [Id(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Division or facility this clinic belongs to
    /// </summary>
    [Id(2)]
    public string? Division { get; set; }

    /// <summary>
    /// DSS/Stop Code number (3.5) — used for workload credit
    /// </summary>
    [Id(3)]
    public string? StopCode { get; set; }

    /// <summary>
    /// Clinic telephone number (99)
    /// </summary>
    [Id(4)]
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Physical location / room (e.g., "Building 12, Room 204")
    /// </summary>
    [Id(5)]
    public string? PhysicalLocation { get; set; }

    /// <summary>
    /// Default appointment slot length in minutes (2)
    /// </summary>
    [Id(6)]
    public int AppointmentLength { get; set; } = 30;

    /// <summary>
    /// Maximum patients per day (1902)
    /// </summary>
    [Id(7)]
    public int MaxPatientsPerDay { get; set; } = 20;

    /// <summary>
    /// Whether overbooking is permitted (2500)
    /// </summary>
    [Id(8)]
    public bool AllowOverbooking { get; set; }

    /// <summary>
    /// Clinic type: C=CLINIC, M=MODULE, W=WARD, Z=OTHER LOCATION, etc.
    /// </summary>
    [Id(9)]
    public string ClinicType { get; set; } = "C";

    /// <summary>
    /// Clinic status: ACTIVE or INACTIVE
    /// </summary>
    [Id(10)]
    public string Status { get; set; } = "ACTIVE";

    /// <summary>
    /// Primary provider IEN
    /// </summary>
    [Id(11)]
    public string? PrimaryProviderId { get; set; }

    /// <summary>
    /// Primary provider name
    /// </summary>
    [Id(12)]
    public string? PrimaryProviderName { get; set; }

    /// <summary>
    /// Date record created
    /// </summary>
    [Id(13)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date record last modified
    /// </summary>
    [Id(14)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this clinic accepts patient self-scheduling from the portal.
    /// When false, only staff can book appointments at this clinic.
    /// </summary>
    [Id(15)]
    public bool AcceptsPatientSelfSchedule { get; set; }
}
