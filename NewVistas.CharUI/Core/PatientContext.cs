// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.CharUI.Core;

/// <summary>
/// Holds currently selected patient information.
/// Populated from CoverSheetState.Demographics after patient selection.
/// </summary>
public class PatientContext
{
    public string? PatientId { get; set; }
    public string? PatientName { get; set; }
    public string? Ssn4 { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Sex { get; set; }
    public int? Age { get; set; }
    public bool IsAdmitted { get; set; }
    public string? RoomBed { get; set; }
    public bool IsServiceConnected { get; set; }
    public int? ServiceConnectedPercent { get; set; }
    public string CwadFlags { get; set; } = string.Empty;

    public bool HasPatient => !string.IsNullOrWhiteSpace(PatientId);

    public void Clear()
    {
        PatientId = null;
        PatientName = null;
        Ssn4 = null;
        DateOfBirth = null;
        Sex = null;
        Age = null;
        IsAdmitted = false;
        RoomBed = null;
        IsServiceConnected = false;
        ServiceConnectedPercent = null;
        CwadFlags = string.Empty;
    }

    public void SetFromCoverSheet(CoverSheetState cs)
    {
        PatientDemographicsSummary d = cs.Demographics;
        PatientName = d.Name;
        Sex = d.Sex;
        DateOfBirth = d.DateOfBirth;
        Age = d.Age;
        IsAdmitted = d.IsAdmitted;
        RoomBed = d.RoomBed;
        IsServiceConnected = d.IsServiceConnected;
        ServiceConnectedPercent = d.ServiceConnectedPercent;
        CwadFlags = cs.Cwad.ToString();
    }

    public void SetFromIndexEntry(PatientIndexEntry entry)
    {
        PatientId = entry.PatientId;
        PatientName = entry.Name;
        DateOfBirth = entry.DateOfBirth;
        Sex = entry.Sex;
        Ssn4 = entry.SsnLast4;
    }
}
