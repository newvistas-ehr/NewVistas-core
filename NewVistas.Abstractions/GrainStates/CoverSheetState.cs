// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Cover sheet data for a patient, derived from VistA CPRS cover sheet
/// (ORWCV.m POLL sections: PROB, CWAD, MEDS, RMND, LABS, VITL, VSIT)
/// </summary>
[GenerateSerializer]
public class CoverSheetState
{
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    [Id(1)]
    public PatientDemographicsSummary Demographics { get; set; } = new();

    /// <summary>
    /// CWAD flags from ORWPT SELECT — Crises, Warnings, Allergies, Directives
    /// </summary>
    [Id(2)]
    public CwadFlags Cwad { get; set; } = new();

    [Id(3)]
    public List<ProblemSummary> ActiveProblems { get; set; } = [];

    [Id(4)]
    public List<MedicationSummary> ActiveMedications { get; set; } = [];

    [Id(5)]
    public List<AllergySummary> Allergies { get; set; } = [];

    [Id(6)]
    public List<ReminderSummary> ClinicalReminders { get; set; } = [];

    [Id(7)]
    public List<LabResultSummary> RecentLabs { get; set; } = [];

    [Id(8)]
    public List<VitalSummary> RecentVitals { get; set; } = [];

    [Id(9)]
    public List<VisitSummary> RecentVisits { get; set; } = [];

    [Id(10)]
    public List<OrderSummary> ActiveOrders { get; set; } = [];

    [Id(11)]
    public DateTime LastRefreshed { get; set; }

    [Id(12)]
    public List<TiuNoteSummary> RecentNotes { get; set; } = [];

    [Id(13)]
    public List<ConsultSummary> ActiveConsults { get; set; } = [];
}

/// <summary>
/// Derived from ORWPT SELECT (ORWPT.m):
/// NAME^SEX^DOB^SSN^LOCIEN^LOCNM^RMBD^CWAD^SENSITIVE^ADMITTED^CONV^SC^SC%^ICN^AGE^TS^TSSVC
/// </summary>
[GenerateSerializer]
public class PatientDemographicsSummary
{
    [Id(0)]
    public string Name { get; set; } = string.Empty;

    [Id(1)]
    public string Sex { get; set; } = string.Empty;

    [Id(2)]
    public DateTime? DateOfBirth { get; set; }

    [Id(3)]
    public string? Ssn { get; set; }

    [Id(4)]
    public int? Age { get; set; }

    [Id(5)]
    public string? LocationName { get; set; }

    [Id(6)]
    public string? RoomBed { get; set; }

    [Id(7)]
    public bool IsAdmitted { get; set; }

    [Id(8)]
    public bool IsSensitive { get; set; }

    [Id(9)]
    public bool IsServiceConnected { get; set; }

    [Id(10)]
    public int? ServiceConnectedPercent { get; set; }

    [Id(11)]
    public bool IsVeteran { get; set; }
}

/// <summary>
/// Crises/Warnings/Allergies/Directives flags from $$CWAD^ORQPT2
/// </summary>
[GenerateSerializer]
public class CwadFlags
{
    [Id(0)]
    public bool HasCrisisNotes { get; set; }

    [Id(1)]
    public bool HasWarnings { get; set; }

    [Id(2)]
    public bool HasAllergies { get; set; }

    [Id(3)]
    public bool HasAdvanceDirectives { get; set; }

    public override string ToString()
    {
        var flags = "";
        if (HasCrisisNotes) flags += "C";
        if (HasWarnings) flags += "W";
        if (HasAllergies) flags += "A";
        if (HasAdvanceDirectives) flags += "D";
        return flags;
    }
}

[GenerateSerializer]
public class ProblemSummary
{
    [Id(0)]
    public string ProblemId { get; set; } = string.Empty;

    [Id(1)]
    public string Diagnosis { get; set; } = string.Empty;

    [Id(2)]
    public string? DiagnosisCode { get; set; }

    [Id(3)]
    public string Status { get; set; } = string.Empty;

    [Id(4)]
    public DateTime? DateOfOnset { get; set; }

    [Id(5)]
    public string? Condition { get; set; }

    [Id(6)]
    public bool IsServiceConnected { get; set; }
}

[GenerateSerializer]
public class MedicationSummary
{
    [Id(0)]
    public string PrescriptionId { get; set; } = string.Empty;

    [Id(1)]
    public string DrugName { get; set; } = string.Empty;

    [Id(2)]
    public string? Sig { get; set; }

    [Id(3)]
    public string Status { get; set; } = string.Empty;

    [Id(4)]
    public DateTime? FillDate { get; set; }

    [Id(5)]
    public int? RefillsRemaining { get; set; }

    /// <summary>
    /// Facility drug IEN (File #50) this prescription was written for, when known.
    /// Lets class-based DUR checks (duplicate therapy) resolve the drug's full VA
    /// therapeutic class set via <c>IDrugGrain.GetDrugClassAsync</c>. Null for
    /// free-text or legacy prescriptions with no linked drug-file entry.
    /// </summary>
    [Id(6)]
    public string? DrugId { get; set; }
}

[GenerateSerializer]
public class AllergySummary
{
    [Id(0)]
    public string AllergyId { get; set; } = string.Empty;

    [Id(1)]
    public string Allergen { get; set; } = string.Empty;

    [Id(2)]
    public string? Severity { get; set; }

    [Id(3)]
    public List<string> Reactions { get; set; } = [];

    [Id(4)]
    public string AllergenType { get; set; } = string.Empty;

    [Id(5)]
    public string? ObservedHistorical { get; set; }
}

[GenerateSerializer]
public class ReminderSummary
{
    [Id(0)]
    public string ReminderId { get; set; } = string.Empty;

    [Id(1)]
    public string ReminderName { get; set; } = string.Empty;

    [Id(2)]
    public string Status { get; set; } = string.Empty;

    [Id(3)]
    public DateTime? DueDate { get; set; }
}

[GenerateSerializer]
public class LabResultSummary
{
    [Id(0)]
    public string LabTestId { get; set; } = string.Empty;

    [Id(1)]
    public string TestName { get; set; } = string.Empty;

    [Id(2)]
    public string? ResultValue { get; set; }

    [Id(3)]
    public string? Units { get; set; }

    [Id(4)]
    public string? Flag { get; set; }

    [Id(5)]
    public string Status { get; set; } = string.Empty;

    [Id(6)]
    public DateTime? CollectionDate { get; set; }
}

[GenerateSerializer]
public class VitalSummary
{
    [Id(0)]
    public string VitalId { get; set; } = string.Empty;

    [Id(1)]
    public string VitalType { get; set; } = string.Empty;

    [Id(2)]
    public string Value { get; set; } = string.Empty;

    [Id(3)]
    public string? Units { get; set; }

    [Id(4)]
    public DateTime DateTimeTaken { get; set; }

    [Id(5)]
    public string? AbnormalFlag { get; set; }
}

[GenerateSerializer]
public class VisitSummary
{
    [Id(0)]
    public string AppointmentId { get; set; } = string.Empty;

    [Id(1)]
    public string? ClinicName { get; set; }

    [Id(2)]
    public DateTime AppointmentDateTime { get; set; }

    [Id(3)]
    public string Status { get; set; } = string.Empty;

    [Id(4)]
    public string? ProviderName { get; set; }
}

[GenerateSerializer]
public class OrderSummary
{
    [Id(0)]
    public string OrderId { get; set; } = string.Empty;

    [Id(1)]
    public string OrderText { get; set; } = string.Empty;

    [Id(2)]
    public string OrderType { get; set; } = string.Empty;

    [Id(3)]
    public string Status { get; set; } = string.Empty;

    [Id(4)]
    public DateTime? StartDate { get; set; }

    [Id(5)]
    public string? ProviderName { get; set; }
}
