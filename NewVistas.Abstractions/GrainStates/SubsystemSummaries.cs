// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

[GenerateSerializer]
public class SurgerySummary
{
    [Id(0)] public string SurgeryId { get; set; } = string.Empty;
    [Id(1)] public string PrincipalProcedure { get; set; } = string.Empty;
    [Id(2)] public string? CptCode { get; set; }
    [Id(3)] public DateTime DateOfOperation { get; set; }
    [Id(4)] public string? SurgeonName { get; set; }
    [Id(5)] public string Status { get; set; } = string.Empty;
    [Id(6)] public string? SurgicalSpecialty { get; set; }
}

[GenerateSerializer]
public class RadiologySummary
{
    [Id(0)] public string RadiologyId { get; set; } = string.Empty;
    [Id(1)] public string ProcedureName { get; set; } = string.Empty;
    [Id(2)] public string? ImagingType { get; set; }
    [Id(3)] public string Status { get; set; } = string.Empty;
    [Id(4)] public DateTime? ExamDateTime { get; set; }
    [Id(5)] public string? RequestingProviderName { get; set; }
    [Id(6)] public bool HasReport { get; set; }
}

[GenerateSerializer]
public class BcmaSummary
{
    [Id(0)] public string BcmaId { get; set; } = string.Empty;
    [Id(1)] public string DrugName { get; set; } = string.Empty;
    [Id(2)] public string? Dosage { get; set; }
    [Id(3)] public string ActionStatus { get; set; } = string.Empty;
    [Id(4)] public DateTime AdministrationDateTime { get; set; }
    [Id(5)] public string? AdministeredByName { get; set; }
}

[GenerateSerializer]
public class ImagingSummary
{
    [Id(0)] public string ImageId { get; set; } = string.Empty;
    [Id(1)] public string ObjectType { get; set; } = string.Empty;
    [Id(2)] public string? ProcedureDescription { get; set; }
    [Id(3)] public string Status { get; set; } = string.Empty;
    [Id(4)] public DateTime CaptureDate { get; set; }
    [Id(5)] public int ImageCount { get; set; }
}

[GenerateSerializer]
public class ImmunizationSummary
{
    [Id(0)] public string ImmunizationId { get; set; } = string.Empty;
    [Id(1)] public string ImmunizationName { get; set; } = string.Empty;
    [Id(2)] public string? CvxCode { get; set; }
    [Id(3)] public DateTime EventDateTime { get; set; }
    [Id(4)] public string? Series { get; set; }
    [Id(5)] public string? AdministeredByName { get; set; }
}

[GenerateSerializer]
public class HealthFactorSummary
{
    [Id(0)] public string HealthFactorId { get; set; } = string.Empty;
    [Id(1)] public string HealthFactorName { get; set; } = string.Empty;
    [Id(2)] public string? Category { get; set; }
    [Id(3)] public DateTime EventDateTime { get; set; }
    [Id(4)] public string? LevelSeverity { get; set; }
}

[GenerateSerializer]
public class MentalHealthSummary
{
    [Id(0)] public string InstrumentId { get; set; } = string.Empty;
    [Id(1)] public string InstrumentName { get; set; } = string.Empty;
    [Id(2)] public DateTime AdministrationDateTime { get; set; }
    [Id(3)] public decimal? TotalScore { get; set; }
    [Id(4)] public string? ScoreInterpretation { get; set; }
    [Id(5)] public bool? IsPositiveScreen { get; set; }
    [Id(6)] public string Status { get; set; } = string.Empty;
}

[GenerateSerializer]
public class DieteticsSummary
{
    [Id(0)] public string DietOrderId { get; set; } = string.Empty;
    [Id(1)] public string DietType { get; set; } = string.Empty;
    [Id(2)] public string? CurrentDiet { get; set; }
    [Id(3)] public string Status { get; set; } = string.Empty;
    [Id(4)] public DateTime StartDateTime { get; set; }
}

[GenerateSerializer]
public class ProstheticsSummary
{
    [Id(0)] public string ProstheticsId { get; set; } = string.Empty;
    [Id(1)] public string ItemDescription { get; set; } = string.Empty;
    [Id(2)] public string? HcpcsCode { get; set; }
    [Id(3)] public string Status { get; set; } = string.Empty;
    [Id(4)] public DateTime DateIssued { get; set; }
    [Id(5)] public bool IsServiceConnected { get; set; }
}

[GenerateSerializer]
public class MeansTestSummary
{
    [Id(0)] public string MeansTestId { get; set; } = string.Empty;
    [Id(1)] public string TestType { get; set; } = string.Empty;
    [Id(2)] public DateTime DateOfTest { get; set; }
    [Id(3)] public string Status { get; set; } = string.Empty;
    [Id(4)] public string? EligibilityStatus { get; set; }
    [Id(5)] public string? PriorityGroup { get; set; }
}

[GenerateSerializer]
public class ServiceConnectedSummary
{
    [Id(0)] public string ConditionId { get; set; } = string.Empty;
    [Id(1)] public string Condition { get; set; } = string.Empty;
    [Id(2)] public string? DiagnosisCode { get; set; }
    [Id(3)] public int? DisabilityPercentage { get; set; }
    [Id(4)] public bool IsServiceConnected { get; set; }
    [Id(5)] public string Status { get; set; } = string.Empty;
}

[GenerateSerializer]
public class AdtSummary
{
    [Id(0)] public string MovementId { get; set; } = string.Empty;
    [Id(1)] public string MovementType { get; set; } = string.Empty;
    [Id(2)] public DateTime MovementDateTime { get; set; }
    [Id(3)] public string? WardLocationName { get; set; }
    [Id(4)] public string? RoomBed { get; set; }
    [Id(5)] public string? AttendingPhysicianName { get; set; }
    [Id(6)] public string Status { get; set; } = string.Empty;
}
