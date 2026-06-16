// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Nursing unit configuration and real-time census — VistA File #210 (NURSING UNIT).
/// Tracks bed assignments, occupancy, and charge nurse for a single inpatient unit.
/// </summary>
[GenerateSerializer]
public class NursingUnitState
{
    [Id(0)] public string UnitId { get; set; } = string.Empty;
    [Id(1)] public string UnitName { get; set; } = string.Empty;

    /// <summary>Unit clinical specialty: MedSurg, ICU, PCU, PACU, ER, Psych, OB, Oncology, LTC.</summary>
    [Id(2)] public string? UnitType { get; set; }

    /// <summary>Total licensed bed count.</summary>
    [Id(3)] public int TotalBeds { get; set; }

    /// <summary>Current bed assignments (occupied and vacant).</summary>
    [Id(4)] public List<NursingBedAssignment> BedAssignments { get; set; } = new();

    [Id(5)] public string? ChargeNurseId { get; set; }
    [Id(6)] public string? ChargeNurseName { get; set; }

    [Id(7)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>Current occupancy state of a single bed within a nursing unit.</summary>
[GenerateSerializer]
public class NursingBedAssignment
{
    /// <summary>Bed identifier (e.g., "101A", "ICU-3", "2B").</summary>
    [Id(0)] public string Bed { get; set; } = string.Empty;

    [Id(1)] public string? PatientId { get; set; }
    [Id(2)] public string? PatientName { get; set; }
    [Id(3)] public DateTime? AdmissionDateTime { get; set; }

    /// <summary>Most recently recorded acuity for this bed occupant.</summary>
    [Id(4)] public AcuityLevel? AcuityLevel { get; set; }

    [Id(5)] public bool IsOccupied { get; set; }

    [Id(6)] public string? AttendingNurseId { get; set; }
    [Id(7)] public string? AttendingNurseName { get; set; }
}
