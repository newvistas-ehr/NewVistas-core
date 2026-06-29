// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Clinical;

/// <summary>
/// Well-known OASIS item codes used by the home-health module. A representative subset of the
/// full OASIS-E2 instrument — primarily the functional items that drive the PDGM functional level,
/// plus a few key diagnosis / risk / vaccination items. The complete instrument has ~100 items.
/// </summary>
public static class OasisItems
{
    public const string PrimaryDiagnosis = "M1021";       // Primary diagnosis
    public const string OtherDiagnoses = "M1023";         // Other diagnoses
    public const string RiskOfHospitalization = "M1033";  // Risk for hospitalization (count of factors)
    public const string Grooming = "M1800";
    public const string UpperBodyDressing = "M1810";
    public const string LowerBodyDressing = "M1820";
    public const string Bathing = "M1830";
    public const string ToiletTransferring = "M1840";
    public const string Transferring = "M1850";
    public const string Ambulation = "M1860";

    /// <summary>The PDGM functional items required at SOC/ROC/Recert.</summary>
    public static readonly IReadOnlyList<string> FunctionalItems = new[]
    {
        Grooming, UpperBodyDressing, LowerBodyDressing, Bathing, ToiletTransferring, Transferring, Ambulation
    };
}

/// <summary>The result of "scrubbing" (validating) an OASIS data set before submission.</summary>
[GenerateSerializer]
public class OasisScrubResult
{
    /// <summary>True when no blocking issues were found.</summary>
    [Id(0)] public bool IsClean { get; set; }
    /// <summary>Human-readable validation issues (empty when clean).</summary>
    [Id(1)] public List<string> Issues { get; set; } = new();
}

/// <summary>The outcome of recording an OASIS assessment: the new assessment id + its scrub result.</summary>
[GenerateSerializer]
public class OasisRecordResult
{
    [Id(0)] public string AssessmentId { get; set; } = string.Empty;
    [Id(1)] public OasisScrubResult Scrub { get; set; } = new();
}

/// <summary>
/// Deterministic OASIS "scrubber" — the pre-submission validation pass every home-health EHR runs
/// so an assessment is not rejected by CMS/iQIES. <b>Representative</b>: it checks version,
/// required-functional-item presence at the relevant time points, and response-range validity. It
/// is NOT the full CMS edit specification. The grounded clinical-AI verifier is the natural place
/// to layer richer, narrative-grounded consistency checks on top of these structural rules.
/// </summary>
public static class OasisScrubber
{
    public static OasisScrubResult Scrub(OasisDataSet? data, HomeCareAssessmentType timePoint)
    {
        var result = new OasisScrubResult { IsClean = true };
        if (data is null)
        {
            result.IsClean = false;
            result.Issues.Add("No OASIS data set present.");
            return result;
        }

        if (string.IsNullOrWhiteSpace(data.Version))
            result.Issues.Add("OASIS version is not set.");

        bool requiresFunctional = timePoint is HomeCareAssessmentType.OasisStartOfCare
            or HomeCareAssessmentType.OasisResumption
            or HomeCareAssessmentType.OasisRecertification;

        if (requiresFunctional)
        {
            foreach (string item in OasisItems.FunctionalItems)
                if (!data.Items.ContainsKey(item))
                    result.Issues.Add($"Required functional item {item} is missing.");

            if (!data.Items.ContainsKey(OasisItems.PrimaryDiagnosis))
                result.Issues.Add($"Primary diagnosis item {OasisItems.PrimaryDiagnosis} is missing.");
        }

        // Functional response values must be small non-negative integers (0..6 on the OASIS scales).
        foreach (string item in OasisItems.FunctionalItems)
        {
            if (data.Items.TryGetValue(item, out string? v))
            {
                if (!int.TryParse(v, out int n) || n < 0 || n > 6)
                    result.Issues.Add($"Item {item} has an out-of-range value '{v}' (expected 0-6).");
            }
        }

        result.IsClean = result.Issues.Count == 0;
        return result;
    }
}
