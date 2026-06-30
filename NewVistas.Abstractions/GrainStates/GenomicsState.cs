// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

// ── Genomics — first-class genetic test results with coded reportable variants ──
// Per the genetics blueprint (Docs/Domain/genetics-and-family-modeling.md): the EHR stores the
// interpreted clinical report + reportable variants as discrete, coded data (HGVS nomenclature,
// ClinVar/ACMG classification, panel/lab metadata) — NOT raw sequence — so they are queryable and
// drive decision support, rather than falling through the generic lab machinery as opaque text.

/// <summary>Methodology of a genetic test.</summary>
public enum GeneticTestMethod
{
    Unknown = 0,
    NextGenSequencing = 1,    // NGS panel / exome / genome
    SangerSequencing = 2,
    Mlpa = 3,                 // deletion/duplication
    Microarray = 4,           // CMA
    Karyotype = 5,
    FishAnalysis = 6,
    PolymeraseChainReaction = 7,
    Other = 8
}

/// <summary>Overall interpreted result of a genetic test report.</summary>
public enum GeneticReportResult
{
    Pending = 0,
    Negative = 1,                       // no reportable variant
    PositivePathogenic = 2,             // pathogenic / likely-pathogenic variant found
    VariantOfUncertainSignificance = 3, // VUS only
    CarrierDetected = 4,                // recessive carrier
    Indeterminate = 5
}

/// <summary>ACMG/ClinVar 5-tier variant classification.</summary>
public enum VariantClassification
{
    Unknown = 0,
    Pathogenic = 1,
    LikelyPathogenic = 2,
    UncertainSignificance = 3,
    LikelyBenign = 4,
    Benign = 5
}

/// <summary>Zygosity of a reported variant.</summary>
public enum VariantZygosity
{
    Unknown = 0,
    Heterozygous = 1,
    Homozygous = 2,
    Hemizygous = 3
}

/// <summary>Whether a variant is constitutional (germline) or acquired (somatic/tumor).</summary>
public enum VariantOrigin
{
    Unknown = 0,
    Germline = 1,
    Somatic = 2
}

/// <summary>
/// One reportable variant in HGVS nomenclature with its ACMG/ClinVar classification — the discrete,
/// coded core of a genomics report.
/// </summary>
[GenerateSerializer]
public class GeneticVariant
{
    [Id(0)] public string VariantId { get; set; } = string.Empty;
    /// <summary>Gene symbol, e.g. "BRCA1", "MLH1".</summary>
    [Id(1)] public string Gene { get; set; } = string.Empty;
    /// <summary>HGVS coding-DNA change, e.g. "c.68_69delAG".</summary>
    [Id(2)] public string HgvsCoding { get; set; } = string.Empty;
    /// <summary>HGVS protein change, e.g. "p.Glu23ValfsTer17".</summary>
    [Id(3)] public string HgvsProtein { get; set; } = string.Empty;
    /// <summary>Reference transcript, e.g. "NM_007294.4".</summary>
    [Id(4)] public string Transcript { get; set; } = string.Empty;
    [Id(5)] public VariantClassification Classification { get; set; }
    [Id(6)] public VariantZygosity Zygosity { get; set; }
    [Id(7)] public VariantOrigin Origin { get; set; } = VariantOrigin.Germline;
    /// <summary>ClinVar variation id, e.g. "VCV000017661".</summary>
    [Id(8)] public string ClinVarId { get; set; } = string.Empty;
    /// <summary>dbSNP rsID, e.g. "rs80357914".</summary>
    [Id(9)] public string DbSnpId { get; set; } = string.Empty;
    [Id(10)] public string Notes { get; set; } = string.Empty;
}

/// <summary>An interpreted genetic test report (panel) with its reportable variants.</summary>
[GenerateSerializer]
public class GeneticTestReport
{
    [Id(0)] public string ReportId { get; set; } = string.Empty;
    /// <summary>Test / panel name, e.g. "Hereditary Cancer Panel (Invitae)".</summary>
    [Id(1)] public string TestName { get; set; } = string.Empty;
    [Id(2)] public string Lab { get; set; } = string.Empty;
    [Id(3)] public GeneticTestMethod Method { get; set; }
    /// <summary>Clinical indication for the test.</summary>
    [Id(4)] public string Indication { get; set; } = string.Empty;
    [Id(5)] public DateTime? CollectionDate { get; set; }
    [Id(6)] public DateTime? ReportDate { get; set; }
    [Id(7)] public GeneticReportResult OverallResult { get; set; }
    [Id(8)] public string OrderingProvider { get; set; } = string.Empty;
    [Id(9)] public List<GeneticVariant> Variants { get; set; } = new();
    [Id(10)] public string Notes { get; set; } = string.Empty;
    [Id(11)] public string RecordedBy { get; set; } = string.Empty;
    [Id(12)] public DateTime RecordedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A patient's genomics record — interpreted genetic test reports + their reportable variants.
/// Key pattern: the patient id (one genomics record per patient).
/// </summary>
[GenerateSerializer]
public class GenomicsState
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public List<GeneticTestReport> Reports { get; set; } = new();
    [Id(2)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(3)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
