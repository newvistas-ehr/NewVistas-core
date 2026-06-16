// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>Enrollment status in the Home-Based Primary Care (HBPC) program. VistA File #750 (.04)</summary>
public enum HBPCProgramStatus
{
    Pending,
    Active,
    Suspended,
    Discharged,
    Deceased
}

/// <summary>Level of care intensity for HBPC. VistA File #750 (.05)</summary>
public enum HBPCLevelOfCare
{
    Basic,
    Enhanced,
    Palliative
}

/// <summary>Reason for discharge from HBPC. VistA File #750 (.08)</summary>
public enum HBPCDischargeReason
{
    GoalsMet,
    PatientDeclined,
    MovedOutOfArea,
    TransferredToFacility,
    SafetyConcern,
    Hospitalized,
    Deceased,
    Other
}

/// <summary>Discipline performing the home health visit. VistA File #750.1 (.03)</summary>
public enum HHCVisitDiscipline
{
    Nursing,
    PhysicalTherapy,
    OccupationalTherapy,
    SpeechLanguagePathology,
    SocialWork,
    Dietitian,
    HomeHealthAide,
    MentalHealth,
    Pharmacy,
    Other
}

/// <summary>Type of home health visit. VistA File #750.1 (.04)</summary>
public enum HHCVisitType
{
    Admission,
    Routine,
    Urgent,
    Supervisory,
    Discharge,
    PhoneContact
}

/// <summary>Status of the home health visit. VistA File #750.1 (.05)</summary>
public enum HHCVisitStatus
{
    Scheduled,
    Completed,
    Cancelled,
    NoAnswer,
    PatientRefused
}

/// <summary>
/// Home-Based Primary Care (HBPC) program record for a patient.
/// VistA File #750 (HOME BASED PRIMARY CARE). HBPC.m, HBHOME.m
/// </summary>
[GenerateSerializer]
public class HBPCPatientState
{
    /// <summary>Patient file number. VistA File #750 (.01)</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name. VistA File #750 (.02)</summary>
    [Id(1)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Date enrolled in HBPC program. VistA File #750 (.03)</summary>
    [Id(2)] public DateTime EnrollmentDate { get; set; }

    /// <summary>Current program status. VistA File #750 (.04)</summary>
    [Id(3)] public HBPCProgramStatus ProgramStatus { get; set; }

    /// <summary>Level of care intensity. VistA File #750 (.05)</summary>
    [Id(4)] public HBPCLevelOfCare LevelOfCare { get; set; }

    /// <summary>Primary admitting diagnosis.</summary>
    [Id(5)] public string PrimaryDiagnosis { get; set; } = string.Empty;

    /// <summary>Secondary diagnoses list.</summary>
    [Id(6)] public List<string> SecondaryDiagnoses { get; set; } = new();

    /// <summary>Primary informal caregiver name.</summary>
    [Id(7)] public string PrimaryCaregiver { get; set; } = string.Empty;

    /// <summary>Patient's home address for visit routing.</summary>
    [Id(8)] public string HomeAddress { get; set; } = string.Empty;

    /// <summary>Care team members (clinician names/roles).</summary>
    [Id(9)] public List<string> CareTeamMembers { get; set; } = new();

    /// <summary>Active care goals for the patient.</summary>
    [Id(10)] public List<string> Goals { get; set; } = new();

    /// <summary>Date of the most recent home visit.</summary>
    [Id(11)] public DateTime? LastVisitDate { get; set; }

    /// <summary>Date of the next scheduled home visit.</summary>
    [Id(12)] public DateTime? NextScheduledVisit { get; set; }

    /// <summary>Total visit count this calendar year.</summary>
    [Id(13)] public int TotalVisitsThisYear { get; set; }

    /// <summary>Date discharged from HBPC.</summary>
    [Id(14)] public DateTime? DischargeDate { get; set; }

    /// <summary>Reason for discharge.</summary>
    [Id(15)] public HBPCDischargeReason? DischargeReason { get; set; }

    /// <summary>Discharge summary notes.</summary>
    [Id(16)] public string DischargeNotes { get; set; } = string.Empty;

    /// <summary>Last modified timestamp.</summary>
    [Id(17)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Summary entry for the HBPC facility registry index.</summary>
[GenerateSerializer]
public class HBPCRegistryEntry
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public string PatientName { get; set; } = string.Empty;
    [Id(2)] public DateTime EnrollmentDate { get; set; }
    [Id(3)] public HBPCProgramStatus ProgramStatus { get; set; }
    [Id(4)] public HBPCLevelOfCare LevelOfCare { get; set; }
    [Id(5)] public string PrimaryDiagnosis { get; set; } = string.Empty;
    [Id(6)] public DateTime? LastVisitDate { get; set; }
    [Id(7)] public DateTime? NextScheduledVisit { get; set; }
    [Id(8)] public int TotalVisitsThisYear { get; set; }
}

/// <summary>
/// A single home health visit record.
/// VistA File #750.1 (HOME HEALTH VISIT). HBVISIT.m
/// </summary>
[GenerateSerializer]
public class HHCVisitState
{
    /// <summary>Unique visit identifier.</summary>
    [Id(0)] public string VisitId { get; set; } = string.Empty;

    /// <summary>Patient file number. VistA File #750.1 (.01)</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name.</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Scheduled or actual visit date/time. VistA File #750.1 (.02)</summary>
    [Id(3)] public DateTime VisitDate { get; set; }

    /// <summary>Discipline performing the visit. VistA File #750.1 (.03)</summary>
    [Id(4)] public HHCVisitDiscipline Discipline { get; set; }

    /// <summary>Type of visit. VistA File #750.1 (.04)</summary>
    [Id(5)] public HHCVisitType VisitType { get; set; }

    /// <summary>Current visit status. VistA File #750.1 (.05)</summary>
    [Id(6)] public HHCVisitStatus Status { get; set; }

    /// <summary>ID of the clinician performing the visit.</summary>
    [Id(7)] public string ClinicianId { get; set; } = string.Empty;

    /// <summary>Name of the clinician performing the visit.</summary>
    [Id(8)] public string ClinicianName { get; set; } = string.Empty;

    /// <summary>Duration of visit in minutes.</summary>
    [Id(9)] public int DurationMinutes { get; set; }

    /// <summary>Brief vital signs notation (e.g., "BP 138/84, HR 72, O2 96%").</summary>
    [Id(10)] public string VitalSigns { get; set; } = string.Empty;

    /// <summary>Clinical interventions performed during the visit.</summary>
    [Id(11)] public List<string> Interventions { get; set; } = new();

    /// <summary>Patient response and progress notes.</summary>
    [Id(12)] public string PatientResponse { get; set; } = string.Empty;

    /// <summary>Goals progress notes.</summary>
    [Id(13)] public string GoalsProgress { get; set; } = string.Empty;

    /// <summary>Date of next planned visit.</summary>
    [Id(14)] public DateTime? NextVisitDate { get; set; }

    /// <summary>Reason for cancellation if visit was cancelled.</summary>
    [Id(15)] public string CancellationReason { get; set; } = string.Empty;

    /// <summary>Additional clinical notes.</summary>
    [Id(16)] public string Notes { get; set; } = string.Empty;

    /// <summary>Last modified timestamp.</summary>
    [Id(17)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Summary entry for home health visit index queries.</summary>
[GenerateSerializer]
public class HHCVisitIndexEntry
{
    [Id(0)] public string VisitId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public string PatientName { get; set; } = string.Empty;
    [Id(3)] public DateTime VisitDate { get; set; }
    [Id(4)] public HHCVisitDiscipline Discipline { get; set; }
    [Id(5)] public HHCVisitType VisitType { get; set; }
    [Id(6)] public HHCVisitStatus Status { get; set; }
    [Id(7)] public string ClinicianName { get; set; } = string.Empty;
    [Id(8)] public int DurationMinutes { get; set; }
}
