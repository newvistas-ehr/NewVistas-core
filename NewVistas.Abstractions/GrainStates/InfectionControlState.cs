// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

// ── Enums ─────────────────────────────────────────────────────────────────────

[GenerateSerializer]
public enum HAIType
{
    CLABSI,
    CAUTI,
    VAP,
    SSI_Superficial,
    SSI_Deep,
    SSI_Organ,
    CDiff,
    MRSA,
    VRE,
    CRE,
    Other,
}

[GenerateSerializer]
public enum HAICaseStatus
{
    Suspected,
    Confirmed,
    RuledOut,
    Closed,
}

[GenerateSerializer]
public enum AntibioticSusceptibility
{
    Susceptible,
    Intermediate,
    Resistant,
    NotTested,
}

[GenerateSerializer]
public enum OutbreakStatus
{
    Active,
    Controlled,
    Closed,
}

// ── Supporting Types ──────────────────────────────────────────────────────────

[GenerateSerializer]
public class AntibioticSusceptibilityResult
{
    /// <summary>Name of the antibiotic tested (e.g., "Vancomycin", "Ceftriaxone").</summary>
    [Id(0)] public string AntibioticName { get; set; } = string.Empty;

    /// <summary>Susceptibility result from culture/sensitivity testing.</summary>
    [Id(1)] public AntibioticSusceptibility Susceptibility { get; set; }

    /// <summary>Minimum inhibitory concentration value (e.g., "0.5 mcg/mL"). Optional.</summary>
    [Id(2)] public string? MIC { get; set; }
}

[GenerateSerializer]
public class HAICaseSummary
{
    /// <summary>Unique identifier for this HAI case record.</summary>
    [Id(0)] public string CaseId { get; set; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient full name.</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Type of hospital-acquired infection.</summary>
    [Id(3)] public HAIType HAIType { get; set; }

    /// <summary>Current case status (Suspected, Confirmed, RuledOut, Closed).</summary>
    [Id(4)] public HAICaseStatus Status { get; set; }

    /// <summary>Date infection was identified or onset.</summary>
    [Id(5)] public DateTime? InfectionDate { get; set; }

    /// <summary>Location/unit identifier where infection occurred.</summary>
    [Id(6)] public string LocationId { get; set; } = string.Empty;

    /// <summary>Display name of the location/unit.</summary>
    [Id(7)] public string LocationName { get; set; } = string.Empty;

    /// <summary>Causative organism (e.g., "S. aureus", "E. coli").</summary>
    [Id(8)] public string Pathogen { get; set; } = string.Empty;

    /// <summary>Linked outbreak identifier, if any.</summary>
    [Id(9)] public string? OutbreakId { get; set; }
}

[GenerateSerializer]
public class OutbreakSummary
{
    /// <summary>Unique identifier for this outbreak.</summary>
    [Id(0)] public string OutbreakId { get; set; } = string.Empty;

    /// <summary>Short descriptive name for the outbreak.</summary>
    [Id(1)] public string Name { get; set; } = string.Empty;

    /// <summary>Primary HAI type associated with this outbreak.</summary>
    [Id(2)] public HAIType HAIType { get; set; }

    /// <summary>Current outbreak status.</summary>
    [Id(3)] public OutbreakStatus Status { get; set; }

    /// <summary>Date the outbreak was identified/started.</summary>
    [Id(4)] public DateTime? StartDate { get; set; }

    /// <summary>Location/unit identifier where outbreak originated.</summary>
    [Id(5)] public string LocationId { get; set; } = string.Empty;

    /// <summary>Display name of the location/unit.</summary>
    [Id(6)] public string LocationName { get; set; } = string.Empty;

    /// <summary>Number of cases linked to this outbreak.</summary>
    [Id(7)] public int CaseCount { get; set; }
}

// ── Primary State ─────────────────────────────────────────────────────────────

[GenerateSerializer]
public class HAICaseState
{
    // Identity (0–5)
    /// <summary>Unique identifier for this HAI case (matches grain key suffix).</summary>
    [Id(0)] public string CaseId { get; set; } = string.Empty;

    /// <summary>Identifier of the affected patient.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Full name of the affected patient.</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Patient date of birth.</summary>
    [Id(3)] public DateTime? DateOfBirth { get; set; }

    /// <summary>Location/unit identifier where the infection occurred.</summary>
    [Id(4)] public string LocationId { get; set; } = string.Empty;

    /// <summary>Display name of the location/unit.</summary>
    [Id(5)] public string LocationName { get; set; } = string.Empty;

    // Classification (6–10)
    /// <summary>Type of hospital-acquired infection.</summary>
    [Id(6)] public HAIType HAIType { get; set; }

    /// <summary>Current case status.</summary>
    [Id(7)] public HAICaseStatus Status { get; set; } = HAICaseStatus.Suspected;

    /// <summary>Date the infection was identified or onset occurred.</summary>
    [Id(8)] public DateTime? InfectionDate { get; set; }

    /// <summary>Date the infection was confirmed by laboratory/clinical criteria.</summary>
    [Id(9)] public DateTime? ConfirmedDate { get; set; }

    /// <summary>Causative organism or pathogen (e.g., "MRSA", "Klebsiella pneumoniae").</summary>
    [Id(10)] public string Pathogen { get; set; } = string.Empty;

    // Clinical (11–17)
    /// <summary>Source of culture specimen (e.g., "Blood", "Urine", "BAL").</summary>
    [Id(11)] public string CultureSource { get; set; } = string.Empty;

    /// <summary>Date the culture specimen was obtained.</summary>
    [Id(12)] public DateTime? CultureDate { get; set; }

    /// <summary>Number of device-days (relevant for CLABSI, CAUTI, VAP).</summary>
    [Id(13)] public int? DeviceInDays { get; set; }

    /// <summary>Date of surgery (relevant for SSI).</summary>
    [Id(14)] public DateTime? SurgeryDate { get; set; }

    /// <summary>Surgical procedure performed (relevant for SSI).</summary>
    [Id(15)] public string SurgeryProcedure { get; set; } = string.Empty;

    /// <summary>Type of invasive device in use (e.g., "Central Line", "Urinary Catheter").</summary>
    [Id(16)] public string DeviceType { get; set; } = string.Empty;

    /// <summary>Free-text clinical notes.</summary>
    [Id(17)] public string Notes { get; set; } = string.Empty;

    // Lab (18–20)
    /// <summary>Culture and sensitivity results for each antibiotic tested.</summary>
    [Id(18)] public List<AntibioticSusceptibilityResult> SusceptibilityResults { get; set; } = new();

    /// <summary>Gram stain result (e.g., "Gram-positive cocci in clusters").</summary>
    [Id(19)] public string GramStain { get; set; } = string.Empty;

    /// <summary>Final culture result/interpretation.</summary>
    [Id(20)] public string CultureResult { get; set; } = string.Empty;

    // Outbreak (21)
    /// <summary>Linked outbreak identifier. Null if not associated with an outbreak.</summary>
    [Id(21)] public string? OutbreakId { get; set; }

    // Reporter (22–24)
    /// <summary>Identifier of the staff member who reported this case.</summary>
    [Id(22)] public string ReportedById { get; set; } = string.Empty;

    /// <summary>Name of the staff member who reported this case.</summary>
    [Id(23)] public string ReportedByName { get; set; } = string.Empty;

    /// <summary>Date and time this case was reported.</summary>
    [Id(24)] public DateTime ReportedDate { get; set; }

    // Audit (25)
    /// <summary>Date and time this record was last modified.</summary>
    [Id(25)] public DateTime LastModifiedDate { get; set; }
}

// ── Outbreak State ────────────────────────────────────────────────────────────

[GenerateSerializer]
public class OutbreakState
{
    /// <summary>Unique identifier for this outbreak.</summary>
    [Id(0)] public string OutbreakId { get; set; } = string.Empty;

    /// <summary>Short descriptive name for this outbreak.</summary>
    [Id(1)] public string Name { get; set; } = string.Empty;

    /// <summary>Detailed description of the outbreak event.</summary>
    [Id(2)] public string Description { get; set; } = string.Empty;

    /// <summary>Primary type of HAI associated with this outbreak.</summary>
    [Id(3)] public HAIType HAIType { get; set; }

    /// <summary>Current outbreak status.</summary>
    [Id(4)] public OutbreakStatus Status { get; set; } = OutbreakStatus.Active;

    /// <summary>Date the outbreak was identified.</summary>
    [Id(5)] public DateTime? StartDate { get; set; }

    /// <summary>Date the outbreak was brought under control.</summary>
    [Id(6)] public DateTime? ControlDate { get; set; }

    /// <summary>Date the outbreak was officially closed.</summary>
    [Id(7)] public DateTime? CloseDate { get; set; }

    /// <summary>Location/unit identifier where outbreak originated.</summary>
    [Id(8)] public string LocationId { get; set; } = string.Empty;

    /// <summary>Display name of the location/unit.</summary>
    [Id(9)] public string LocationName { get; set; } = string.Empty;

    /// <summary>Primary pathogen associated with this outbreak.</summary>
    [Id(10)] public string Pathogen { get; set; } = string.Empty;

    /// <summary>List of HAI case IDs linked to this outbreak.</summary>
    [Id(11)] public List<string> LinkedCaseIds { get; set; } = new();

    /// <summary>Whether public health authorities have been notified.</summary>
    [Id(12)] public bool NotifiedPublicHealth { get; set; }

    /// <summary>Date public health notification was sent.</summary>
    [Id(13)] public DateTime? NotificationDate { get; set; }

    /// <summary>Date and time this record was last modified.</summary>
    [Id(14)] public DateTime LastModifiedDate { get; set; }
}
