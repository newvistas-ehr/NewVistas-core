// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Code status for clinical care decisions.
/// </summary>
[GenerateSerializer]
public enum CodeStatus
{
    FullCode = 0,
    DNR = 1,
    DNI = 2,
    DNR_DNI = 3,
    ComfortCareOnly = 4,
}

/// <summary>
/// Type of advance directive document.
/// </summary>
[GenerateSerializer]
public enum AdvanceDirectiveType
{
    LivingWill = 0,
    HealthcarePowerOfAttorney = 1,
    DoNotResuscitate = 2,
    PolstMolst = 3,
    OrganDonation = 4,
    MentalHealthDirective = 5,
}

/// <summary>
/// An individual advance directive document on file.
/// </summary>
[GenerateSerializer]
public record AdvanceDirectiveDocument
{
    [Id(0)] public string DocumentId { get; init; } = string.Empty;
    [Id(1)] public AdvanceDirectiveType DirectiveType { get; init; }
    [Id(2)] public DateTime DocumentDate { get; init; }
    [Id(3)] public string? DocumentSource { get; init; }
    [Id(4)] public bool IsOnFile { get; init; }
    [Id(5)] public DateTime? ExpirationDate { get; init; }
    [Id(6)] public string? Notes { get; init; }
}

/// <summary>
/// State for patient advance directive recording.
/// Maps to VistA DG advance directives (CWAD 'D' flag).
/// Grain key: "ADV-DIR:{patientId}"
/// </summary>
[GenerateSerializer]
public class AdvanceDirectiveState
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Current code status.</summary>
    [Id(1)] public CodeStatus CodeStatus { get; set; } = CodeStatus.FullCode;

    /// <summary>Date code status was last updated.</summary>
    [Id(2)] public DateTime? CodeStatusDate { get; set; }

    /// <summary>Who updated the code status.</summary>
    [Id(3)] public string? CodeStatusUpdatedBy { get; set; }

    /// <summary>Whether patient has any advance directives on file.</summary>
    [Id(4)] public bool HasAdvanceDirectives { get; set; }

    /// <summary>Healthcare proxy / power of attorney name.</summary>
    [Id(5)] public string? HealthcareProxyName { get; set; }

    /// <summary>Healthcare proxy phone.</summary>
    [Id(6)] public string? HealthcareProxyPhone { get; set; }

    /// <summary>Healthcare proxy relationship.</summary>
    [Id(7)] public string? HealthcareProxyRelationship { get; set; }

    /// <summary>Advance directive documents on file.</summary>
    [Id(8)] public List<AdvanceDirectiveDocument> Documents { get; set; } = new();

    /// <summary>Free-text notes about advance directives.</summary>
    [Id(9)] public string? Notes { get; set; }

    /// <summary>When last modified.</summary>
    [Id(10)] public DateTime LastModifiedDate { get; set; }
}
