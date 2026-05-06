// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// HL7 Data Segmentation for Privacy (DS4P) sensitivity categories.
/// These map to HL7 ActCode vocabulary (2.16.840.1.113883.1.11.20428).
/// </summary>
public static class Ds4pSensitivityCodes
{
    /// <summary>Substance abuse information (42 CFR Part 2).</summary>
    public const string SubstanceAbuse = "ETH";

    /// <summary>Mental health / behavioral health information.</summary>
    public const string MentalHealth = "PSY";

    /// <summary>HIV/AIDS-related information.</summary>
    public const string Hiv = "HIV";

    /// <summary>Sexual and domestic violence information.</summary>
    public const string SexualAssault = "SDV";

    /// <summary>Sexuality and reproductive health information.</summary>
    public const string Sexuality = "SEX";

    /// <summary>Genetic information (GINA).</summary>
    public const string Genetic = "GDIS";

    /// <summary>Sickle cell disease (considered genetic).</summary>
    public const string SickleCellDisease = "SCA";

    /// <summary>All supported sensitivity category codes.</summary>
    public static readonly IReadOnlyList<string> AllCodes = [SubstanceAbuse, MentalHealth, Hiv, SexualAssault, Sexuality, Genetic, SickleCellDisease];
}

/// <summary>
/// DS4P obligation policy codes — what the receiver MUST do with tagged data.
/// HL7 ActCode vocabulary (2.16.840.1.113883.1.11.20445).
/// </summary>
public static class Ds4pObligationCodes
{
    /// <summary>Encrypt data at rest.</summary>
    public const string EncryptAtRest = "ENCRYPT";

    /// <summary>Encrypt data in transit.</summary>
    public const string EncryptInTransit = "ENCRYPTR";

    /// <summary>Do not redisclose without patient authorization.</summary>
    public const string NoRedisclosure = "NOREDISCLOSURE";
}

/// <summary>
/// DS4P refrain policy codes — what the receiver MUST NOT do with tagged data.
/// HL7 ActCode vocabulary (2.16.840.1.113883.1.11.20446).
/// </summary>
public static class Ds4pRefrainCodes
{
    /// <summary>Do not collect (this data should not be gathered).</summary>
    public const string DoNotCollect = "NOCOLLECT";

    /// <summary>Do not use for research without consent.</summary>
    public const string NoResearchUse = "NOPERSISTP";

    /// <summary>Do not re-use beyond original purpose.</summary>
    public const string NoReuse = "NORDSCLCD";
}

/// <summary>
/// Result of analyzing a C-CDA document for DS4P security tags.
/// Returned by IDs4pProcessorGrain.AnalyzeCcdaAsync.
/// </summary>
[GenerateSerializer]
public class Ds4pAnalysisResult
{
    /// <summary>Whether the document contains any DS4P security tags.</summary>
    [Id(0)]
    public bool HasDs4pTags { get; set; }

    /// <summary>Document-level confidentiality code (N=Normal, R=Restricted, V=Very Restricted).</summary>
    [Id(1)]
    public string DocumentConfidentialityCode { get; set; } = "N";

    /// <summary>Whether the DS4P template ID (2.16.840.1.113883.3.3251.1.1) is present.</summary>
    [Id(2)]
    public bool HasDs4pTemplateId { get; set; }

    /// <summary>Section-level security tags found in the document.</summary>
    [Id(3)]
    public List<Ds4pSectionTag> SectionTags { get; set; } = new();

    /// <summary>Obligation policy codes found at document or section level.</summary>
    [Id(4)]
    public List<string> ObligationPolicies { get; set; } = new();

    /// <summary>Refrain policy codes found at document or section level.</summary>
    [Id(5)]
    public List<string> RefrainPolicies { get; set; } = new();

    /// <summary>Date the analysis was performed.</summary>
    [Id(6)]
    public DateTime AnalyzedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a DS4P security tag found on a specific C-CDA section.
/// </summary>
[GenerateSerializer]
public class Ds4pSectionTag
{
    /// <summary>Section title (e.g., "Medications", "Problems").</summary>
    [Id(0)]
    public string SectionTitle { get; set; } = string.Empty;

    /// <summary>Section LOINC code (e.g., "10160-0" for Medications).</summary>
    [Id(1)]
    public string SectionCode { get; set; } = string.Empty;

    /// <summary>Confidentiality code on this section (R, V, etc.).</summary>
    [Id(2)]
    public string ConfidentialityCode { get; set; } = string.Empty;

    /// <summary>Sensitivity category codes (ETH, PSY, HIV, etc.).</summary>
    [Id(3)]
    public List<string> SensitivityCodes { get; set; } = new();

    /// <summary>Obligation policy codes on this section.</summary>
    [Id(4)]
    public List<string> ObligationPolicies { get; set; } = new();

    /// <summary>Refrain policy codes on this section.</summary>
    [Id(5)]
    public List<string> RefrainPolicies { get; set; } = new();
}

/// <summary>
/// Persisted state for the DS4P Processor Grain.
/// Stores the analysis result for a previously parsed C-CDA.
/// Grain Key: "DS4P-PROC:{messageId}"
/// </summary>
[GenerateSerializer]
public class Ds4pProcessorState
{
    /// <summary>Message ID this analysis belongs to.</summary>
    [Id(0)]
    public string MessageId { get; set; } = string.Empty;

    /// <summary>The analysis result.</summary>
    [Id(1)]
    public Ds4pAnalysisResult? AnalysisResult { get; set; }

    /// <summary>Date the analysis was stored.</summary>
    [Id(2)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
