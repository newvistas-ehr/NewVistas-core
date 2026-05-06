// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lightweight projection of a clinic for index/search purposes.
/// </summary>
[GenerateSerializer]
public class ClinicEntry
{
    /// <summary>Clinic IEN</summary>
    [Id(0)]
    public string ClinicId { get; set; } = string.Empty;

    /// <summary>Clinic name</summary>
    [Id(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Division or facility</summary>
    [Id(2)]
    public string? Division { get; set; }

    /// <summary>DSS Stop Code</summary>
    [Id(3)]
    public string? StopCode { get; set; }

    /// <summary>Default appointment slot length in minutes</summary>
    [Id(4)]
    public int AppointmentLength { get; set; } = 30;

    /// <summary>ACTIVE or INACTIVE</summary>
    [Id(5)]
    public string Status { get; set; } = "ACTIVE";

    /// <summary>Whether this clinic accepts patient self-scheduling from the portal.</summary>
    [Id(6)]
    public bool AcceptsPatientSelfSchedule { get; set; }
}

/// <summary>
/// Index grain state for all clinic definitions.
/// VistA equivalent: "B" cross-reference on HOSPITAL LOCATION file (#44).
/// </summary>
[GenerateSerializer]
public class ClinicIndexState
{
    [Id(0)]
    public List<ClinicEntry> Clinics { get; set; } = new();
}
