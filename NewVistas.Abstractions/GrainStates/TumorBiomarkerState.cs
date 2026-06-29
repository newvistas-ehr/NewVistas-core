// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>Actionability status of a molecular biomarker result.</summary>
[GenerateSerializer]
public enum BiomarkerStatus
{
    Pending = 0,
    Positive = 1,
    Negative = 2,
    Equivocal = 3
}

/// <summary>Assay method used to determine a biomarker.</summary>
[GenerateSerializer]
public enum BiomarkerMethod
{
    Unknown = 0,
    NGS = 1,    // next-generation sequencing
    IHC = 2,    // immunohistochemistry
    FISH = 3,   // fluorescence in-situ hybridization
    PCR = 4,
    Other = 5
}

/// <summary>
/// A single molecular biomarker / genomic test result on a tumor — the input to
/// precision-oncology therapy matching (EGFR, ALK, ROS1, BRAF, KRAS, HER2, PD-L1, MSI,
/// TMB, NTRK, BRCA, RET, …). This is the precision-medicine layer the updated tumor-
/// registry posture is missing; it drives <c>PrecisionOncology.Match</c>.
/// </summary>
[GenerateSerializer]
public record TumorBiomarker
{
    /// <summary>Unique id within the tumor's biomarker panel.</summary>
    [Id(0)] public string BiomarkerId { get; set; } = string.Empty;

    /// <summary>Gene / marker symbol, e.g. "EGFR", "ALK", "PD-L1", "MSI", "TMB", "BRCA1".</summary>
    [Id(1)] public string Gene { get; set; } = string.Empty;

    /// <summary>Actionability status of the result.</summary>
    [Id(2)] public BiomarkerStatus Status { get; set; } = BiomarkerStatus.Pending;

    /// <summary>Specific finding, e.g. "exon 19 deletion", "TPS 60%", "MSI-High", "amplified".</summary>
    [Id(3)] public string Result { get; set; } = string.Empty;

    /// <summary>Assay method.</summary>
    [Id(4)] public BiomarkerMethod Method { get; set; } = BiomarkerMethod.Unknown;

    /// <summary>Date the test resulted.</summary>
    [Id(5)] public DateTime TestDate { get; set; }

    /// <summary>Performing laboratory.</summary>
    [Id(6)] public string Lab { get; set; } = string.Empty;

    /// <summary>Free-text comments.</summary>
    [Id(7)] public string Comments { get; set; } = string.Empty;
}
