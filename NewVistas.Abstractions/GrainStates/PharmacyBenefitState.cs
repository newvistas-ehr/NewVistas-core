// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// A single drug's formulary entry within a benefit plan — mirrors VistA File #59.9.
/// </summary>
[GenerateSerializer]
public class FormularyEntry
{
    [Id(0)]
    public string DrugId { get; set; } = string.Empty;

    [Id(1)]
    public string DrugName { get; set; } = string.Empty;

    /// <summary>1=preferred generic, 2=preferred brand, 3=non-preferred, 0=non-formulary</summary>
    [Id(2)]
    public int Tier { get; set; }

    /// <summary>COVERED | NOT_COVERED | REQUIRES_PA | NON_FORMULARY</summary>
    [Id(3)]
    public string CoverageStatus { get; set; } = string.Empty;

    [Id(4)]
    public bool RequiresPriorAuth { get; set; }

    /// <summary>null = use plan tier copay</summary>
    [Id(5)]
    public decimal? CopayOverride { get; set; }

    /// <summary>Tablets (or equivalent units) per fill limit</summary>
    [Id(6)]
    public int? QuantityLimit { get; set; }

    [Id(7)]
    public int? DaySupplyLimit { get; set; }

    [Id(8)]
    public bool StepTherapyRequired { get; set; }

    [Id(9)]
    public string? StepTherapyNotes { get; set; }

    [Id(10)]
    public string? Notes { get; set; }
}

/// <summary>
/// Persistent state for a patient's active insurance benefit plan.
/// Grain key: "PBM-PATIENT:{patientId}"
/// </summary>
[GenerateSerializer]
public class PatientBenefitPlanState
{
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    [Id(1)]
    public string PlanId { get; set; } = string.Empty;

    [Id(2)]
    public string PlanName { get; set; } = string.Empty;

    [Id(3)]
    public string InsuranceName { get; set; } = string.Empty;

    [Id(4)]
    public string? GroupNumber { get; set; }

    [Id(5)]
    public string? MemberId { get; set; }

    [Id(6)]
    public DateTime? EffectiveDate { get; set; }

    [Id(7)]
    public DateTime? TerminationDate { get; set; }

    [Id(8)]
    public bool IsActive { get; set; }

    /// <summary>Preferred generic copay ($)</summary>
    [Id(9)]
    public decimal CopayTier1 { get; set; }

    /// <summary>Preferred brand copay ($)</summary>
    [Id(10)]
    public decimal CopayTier2 { get; set; }

    /// <summary>Non-preferred copay ($)</summary>
    [Id(11)]
    public decimal CopayTier3 { get; set; }

    [Id(12)]
    public int DaySupplyLimit { get; set; } = 90;

    [Id(13)]
    public decimal AnnualDeductible { get; set; }

    [Id(14)]
    public bool DeductibleMet { get; set; }

    [Id(15)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(16)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Formulary drug list for a specific benefit plan.
/// Grain key: "PBM-FORMULARY:{planId}"
/// </summary>
[GenerateSerializer]
public class FormularyIndexState
{
    [Id(0)]
    public string PlanId { get; set; } = string.Empty;

    [Id(1)]
    public List<FormularyEntry> Entries { get; set; } = new();
}

/// <summary>
/// Full state for a single prior-authorization request — mirrors VistA File #59.9 PA sub-file.
/// Grain key: "PA:{guid}"
/// </summary>
[GenerateSerializer]
public class PriorAuthorizationState
{
    [Id(0)]
    public string PaId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    [Id(2)]
    public string? DrugId { get; set; }

    [Id(3)]
    public string DrugName { get; set; } = string.Empty;

    [Id(4)]
    public string? PlanId { get; set; }

    [Id(5)]
    public string? ProviderId { get; set; }

    [Id(6)]
    public string? ProviderName { get; set; }

    /// <summary>PENDING | APPROVED | DENIED | EXPIRED | CANCELLED</summary>
    [Id(7)]
    public string Status { get; set; } = "PENDING";

    [Id(8)]
    public DateTime RequestDate { get; set; } = DateTime.UtcNow;

    [Id(9)]
    public DateTime? ReviewDate { get; set; }

    [Id(10)]
    public string? DecisionById { get; set; }

    [Id(11)]
    public string? DecisionByName { get; set; }

    [Id(12)]
    public string? DecisionNotes { get; set; }

    [Id(13)]
    public DateTime? ExpirationDate { get; set; }

    [Id(14)]
    public List<string> DiagnosisCodes { get; set; } = new();

    [Id(15)]
    public string? ClinicalJustification { get; set; }

    [Id(16)]
    public string? DenialReason { get; set; }

    [Id(17)]
    public int AppealsCount { get; set; }

    [Id(18)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(19)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Lightweight summary of a PA stored in the per-patient index.
/// </summary>
[GenerateSerializer]
public class PriorAuthIndexEntry
{
    [Id(0)]
    public string PaId { get; set; } = string.Empty;

    [Id(1)]
    public string DrugName { get; set; } = string.Empty;

    [Id(2)]
    public string Status { get; set; } = string.Empty;

    [Id(3)]
    public DateTime RequestDate { get; set; }

    [Id(4)]
    public DateTime? ExpirationDate { get; set; }

    [Id(5)]
    public string? ProviderName { get; set; }

    [Id(6)]
    public string? DrugId { get; set; }
}

/// <summary>
/// Per-patient PA index state.
/// Grain key: "PA-INDEX:{patientId}"
/// </summary>
[GenerateSerializer]
public class PriorAuthIndexState
{
    [Id(0)]
    public List<PriorAuthIndexEntry> Entries { get; set; } = new();
}
