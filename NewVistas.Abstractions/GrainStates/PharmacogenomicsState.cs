// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

// ── Pharmacogenomics (PGx) — discrete, coded gene results that drive drug-gene CDS ──
// Per the genetics blueprint (Docs/Domain/genetics-and-family-modeling.md): the EHR stores the
// interpreted, coded result (star-allele diplotype + CPIC phenotype) that comes back from the lab —
// NOT raw sequence — so decision support can fire ("CYP2C19 poor metabolizer → avoid clopidogrel").

/// <summary>
/// CPIC-style metabolizer / functional phenotype for a pharmacogene. Spans the metabolizer scale,
/// transporter/enzyme function levels, allele-presence (HLA), and G6PD status.
/// </summary>
public enum PgxPhenotype
{
    Unknown = 0,
    UltrarapidMetabolizer = 1,
    RapidMetabolizer = 2,
    NormalMetabolizer = 3,
    IntermediateMetabolizer = 4,
    PoorMetabolizer = 5,
    IncreasedFunction = 6,
    NormalFunction = 7,
    DecreasedFunction = 8,
    PoorFunction = 9,
    Positive = 10,        // allele present (e.g. HLA-B*57:01 positive)
    Negative = 11,        // allele absent
    Deficient = 12,       // e.g. G6PD deficient
    Variable = 13,        // e.g. G6PD variable
    Indeterminate = 14
}

/// <summary>Lifecycle of a pharmacogenomic result.</summary>
public enum PgxResultStatus
{
    Pending = 0,
    Final = 1,
    Superseded = 2
}

/// <summary>
/// Prescribing action a drug-gene rule indicates, ordered by escalating severity
/// (so the worst applicable action sorts last).
/// </summary>
public enum PgxActionCategory
{
    Standard = 0,            // use label dosing
    UseWithCaution = 1,
    AdjustDose = 2,
    ConsiderAlternative = 3,
    Avoid = 4,
    Contraindicated = 5
}

/// <summary>CPIC recommendation strength.</summary>
public enum PgxRecommendationStrength
{
    NoRecommendation = 0,
    Optional = 1,
    Moderate = 2,
    Strong = 3
}

/// <summary>
/// One coded pharmacogenomic result for a single gene — the interpreted result that comes back from
/// the genotyping lab (star-allele diplotype + phenotype), stored as discrete data for CDS.
/// </summary>
[GenerateSerializer]
public class PgxResultEntry
{
    [Id(0)] public string ResultId { get; set; } = string.Empty;
    /// <summary>Gene symbol, e.g. "CYP2C19", "DPYD", "TPMT", "HLA-B*57:01".</summary>
    [Id(1)] public string Gene { get; set; } = string.Empty;
    /// <summary>Diplotype / star-allele genotype, e.g. "*2/*2", "*1/*17", or "positive".</summary>
    [Id(2)] public string Diplotype { get; set; } = string.Empty;
    [Id(3)] public PgxPhenotype Phenotype { get; set; }
    /// <summary>CYP2D6 activity score, when applicable.</summary>
    [Id(4)] public decimal? ActivityScore { get; set; }
    [Id(5)] public PgxResultStatus Status { get; set; } = PgxResultStatus.Final;
    [Id(6)] public DateTime? TestDate { get; set; }
    [Id(7)] public string Lab { get; set; } = string.Empty;
    /// <summary>Methodology, e.g. "Targeted genotyping", "NGS PGx panel".</summary>
    [Id(8)] public string Method { get; set; } = string.Empty;
    [Id(9)] public string Notes { get; set; } = string.Empty;
    [Id(10)] public string RecordedBy { get; set; } = string.Empty;
    [Id(11)] public DateTime RecordedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A patient's pharmacogenomic profile — one coded result per gene. Key pattern: the patient id
/// (one PGx profile per patient). Read by the DUR engine for drug-gene checking.
/// </summary>
[GenerateSerializer]
public class PharmacogenomicsState
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    /// <summary>One current result per gene (upserted by gene).</summary>
    [Id(1)] public List<PgxResultEntry> Results { get; set; } = new();
    [Id(2)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(3)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
