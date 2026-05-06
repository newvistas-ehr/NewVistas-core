// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// DS4P C-CDA Generator Grain — generates C-CDA documents tagged with HL7 Data Segmentation
/// for Privacy (DS4P) security labels per §170.315(b)(7).
///
/// DS4P marks sensitive clinical data (substance abuse, mental health, HIV, STI, genetic, etc.)
/// with confidentialityCode "R" (Restricted) and section-level security observation entries
/// containing obligation and refrain policy codes.
///
/// Template: 2.16.840.1.113883.3.3251.1.1 (DS4P)
/// Grain Key: "DS4P-GEN:{patientId}"
/// </summary>
public interface IDs4pCcdaGeneratorGrain : IGrainWithStringKey
{
    /// <summary>
    /// Generate a C-CDA CCD document with DS4P security tags applied to sensitive sections.
    /// The document-level confidentialityCode is set to "R" (Restricted) and individual
    /// sections containing sensitive data receive security observation entries.
    /// </summary>
    /// <param name="documentType">CCD, Referral, or Discharge.</param>
    /// <param name="sensitivityCategories">Categories of sensitive data to tag (e.g., ETH, PSY, HIV, SDV, SEX).</param>
    Task<string> GenerateDs4pCcdAsync(string documentType, List<string> sensitivityCategories);
}

/// <summary>
/// DS4P Processor Grain — parses received C-CDA documents for DS4P security tags
/// per §170.315(b)(8).
///
/// Extracts document-level and section-level confidentiality codes, obligation policies,
/// and refrain policies from incoming C-CDAs. Returns structured DS4P analysis so the
/// UI can enforce appropriate display restrictions.
///
/// Grain Key: "DS4P-PROC:{messageId}"
/// </summary>
public interface IDs4pProcessorGrain : IGrainWithStringKey
{
    /// <summary>
    /// Parse a C-CDA document and extract all DS4P security tags.
    /// Persists the analysis result for subsequent retrieval.
    /// </summary>
    Task<Ds4pAnalysisResult> AnalyzeCcdaAsync(string ccdaXml);

    /// <summary>
    /// Retrieve the previously stored DS4P analysis result.
    /// </summary>
    Task<Ds4pAnalysisResult> GetAnalysisAsync();
}
