// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Globalization;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Clinical;

/// <summary>
/// Deterministic, explainable matcher: evaluates a patient's <see cref="PatientFeatureSnapshot"/>
/// against a proto-condition's case definition. NO machine learning — "why is this patient here"
/// must be answerable with a feature list, so every feature yields a <see cref="FeatureContribution"/>
/// with quoted evidence, and unparseable / unmeasured values are reported as "not assessed" (never
/// guessed).
///
/// Scoring uses a FIXED denominator (satisfied weighted-feature weight ÷ ALL weighted-feature
/// weight), so scores are comparable across patients regardless of how many features a given patient
/// could actually be assessed for — an unassessed feature counts as 0, not as "excluded from the
/// average". HardInclude features must all be satisfied; a satisfied HardExclude disqualifies
/// outright.
/// </summary>
public static class ProtoConditionMatcher
{
    public static ProtoMatchResult Evaluate(ProtoConditionState proto, PatientFeatureSnapshot snapshot)
    {
        var contributions = new List<FeatureContribution>(proto.Features.Count);
        double weightedTotal = 0, weightedSatisfied = 0;
        bool hardExcluded = false, hardIncludeMissing = false;

        foreach (ProtoFeature f in proto.Features)
        {
            (bool satisfied, bool assessed, string evidence) = EvaluateFeature(f, snapshot);
            contributions.Add(new FeatureContribution
            {
                FeatureId = f.FeatureId,
                Display = f.Display,
                Kind = f.Kind,
                Satisfied = satisfied,
                Assessed = assessed,
                Weight = f.Weight,
                Evidence = evidence
            });

            switch (f.Rule)
            {
                case ProtoFeatureRule.Weighted:
                    weightedTotal += f.Weight;
                    if (satisfied) weightedSatisfied += f.Weight;
                    break;
                case ProtoFeatureRule.HardInclude:
                    if (!satisfied) hardIncludeMissing = true;
                    break;
                case ProtoFeatureRule.HardExclude:
                    if (satisfied) hardExcluded = true;
                    break;
            }
        }

        // An empty definition matches no one (a degenerate config should never sweep everyone in).
        double score;
        bool matches;
        if (proto.Features.Count == 0)
        {
            score = 0;
            matches = false;
        }
        else
        {
            score = weightedTotal > 0 ? weightedSatisfied / weightedTotal : (hardIncludeMissing ? 0.0 : 1.0);
            matches = !hardExcluded && !hardIncludeMissing && score >= proto.MatchThreshold;
        }

        return new ProtoMatchResult
        {
            PatientId = snapshot.PatientId,
            ProtoConditionId = proto.ProtoConditionId,
            DefinitionVersion = proto.DefinitionVersion,
            Score = score,
            Matches = matches,
            HardExcluded = hardExcluded,
            Contributions = contributions
        };
    }

    private static (bool satisfied, bool assessed, string evidence) EvaluateFeature(ProtoFeature f, PatientFeatureSnapshot s) =>
        f.Kind switch
        {
            ProtoFeatureKind.Symptom => EvalSymptom(f, s),
            ProtoFeatureKind.Diagnosis => EvalDiagnosis(f, s),
            ProtoFeatureKind.LabResult => EvalLab(f, s),
            ProtoFeatureKind.Vital => EvalVital(f, s),
            ProtoFeatureKind.Demographic => EvalDemographic(f, s),
            ProtoFeatureKind.Exposure => EvalExposure(f, s),
            _ => (false, false, "unsupported feature kind")
        };

    // ── Symptom (trinary) ────────────────────────────────────────────────
    private static (bool, bool, string) EvalSymptom(ProtoFeature f, PatientFeatureSnapshot s)
    {
        if (!s.Symptoms.TryGetValue(f.Code, out SymptomPresence presence) || presence == SymptomPresence.Unknown)
            return (false, false, "not asked");

        bool satisfied = f.Operator switch
        {
            ProtoFeatureOperator.Present => presence == SymptomPresence.Present,
            ProtoFeatureOperator.Absent => presence == SymptomPresence.Absent,
            _ => false
        };
        return (satisfied, true, $"{SymptomCatalog.DisplayFor(f.Code)}: {presence}");
    }

    // ── Diagnosis (problem list; supports "B05.*" prefix wildcards) ───────
    private static (bool, bool, string) EvalDiagnosis(ProtoFeature f, PatientFeatureSnapshot s)
    {
        string? matched = s.Problems.FirstOrDefault(p => CodeMatches(p, f.Code));
        bool has = matched is not null;
        bool satisfied = f.Operator == ProtoFeatureOperator.Absent ? !has : has;
        return (satisfied, true, has ? $"dx {matched}" : "no matching diagnosis");
    }

    // ── Lab result (LOINC) ───────────────────────────────────────────────
    private static (bool, bool, string) EvalLab(ProtoFeature f, PatientFeatureSnapshot s)
    {
        SnapshotLab? lab = s.Labs.FirstOrDefault(l => Norm(l.Loinc) == Norm(f.Code));
        if (lab is null)
            return (false, false, "no result");
        if (IsStale(lab.ResultedDate, f.RecencyWindowDays, s.AssembledAt))
            return (false, false, $"stale result ({Fmt(lab.ResultedDate)})");

        switch (f.Operator)
        {
            case ProtoFeatureOperator.Present:
                return (true, true, $"result '{lab.Value}'");
            case ProtoFeatureOperator.Absent:
                return (false, true, $"result present '{lab.Value}'");
            case ProtoFeatureOperator.Equals:
                bool eq = string.Equals(lab.Value.Trim(), f.Value?.Trim(), StringComparison.OrdinalIgnoreCase);
                return (eq, true, $"'{lab.Value}'");
            default:
                double? n = ParseNum(lab.Value);
                if (n is null)
                    return (false, false, $"'{lab.Value}' not numeric");
                return CompareNumeric(n.Value, f, lab.Value);
        }
    }

    // ── Vital (BP pre-split into BP_SYS / BP_DIA) ────────────────────────
    private static (bool, bool, string) EvalVital(ProtoFeature f, PatientFeatureSnapshot s)
    {
        SnapshotVital? v = s.Vitals.FirstOrDefault(x => Norm(x.Type) == Norm(f.Code));
        if (v is null)
            return (false, false, "not measured");
        if (IsStale(v.Measured, f.RecencyWindowDays, s.AssembledAt))
            return (false, false, $"stale vital ({Fmt(v.Measured)})");
        if (f.Operator == ProtoFeatureOperator.Present)
            return (true, true, v.Raw);
        if (v.Numeric is null)
            return (false, false, $"'{v.Raw}' not numeric");
        return CompareNumeric(v.Numeric.Value, f, v.Raw);
    }

    // ── Demographic (AGE / SEX / CITY / RACE) ────────────────────────────
    private static (bool, bool, string) EvalDemographic(ProtoFeature f, PatientFeatureSnapshot s)
    {
        switch (f.Code.Trim().ToUpperInvariant())
        {
            case "AGE":
                if (s.Age is null) return (false, false, "age unknown");
                return CompareNumeric(s.Age.Value, f, $"age {s.Age}");
            case "SEX":
                return EqualsDemographic(s.Sex, f.Value, "sex");
            case "CITY":
                return EqualsDemographic(s.City, f.Value, "city");
            case "RACE":
                return EqualsDemographic(s.Race, f.Value, "race");
            default:
                return (false, false, $"unknown demographic '{f.Code}'");
        }
    }

    private static (bool, bool, string) EqualsDemographic(string? actual, string? expected, string label)
    {
        if (string.IsNullOrWhiteSpace(actual))
            return (false, false, $"{label} unknown");
        bool eq = string.Equals(actual.Trim(), expected?.Trim(), StringComparison.OrdinalIgnoreCase);
        return (eq, true, $"{label} {actual}");
    }

    // ── Exposure (facility) ──────────────────────────────────────────────
    private static (bool, bool, string) EvalExposure(ProtoFeature f, PatientFeatureSnapshot s)
    {
        bool has = s.Facilities.Any(x => Norm(x) == Norm(f.Code));
        bool satisfied = f.Operator == ProtoFeatureOperator.Absent ? !has : has;
        return (satisfied, true, has ? $"treated at {f.Code}" : "no such exposure");
    }

    // ── Numeric comparison ───────────────────────────────────────────────
    private static (bool, bool, string) CompareNumeric(double actual, ProtoFeature f, string rawEvidence)
    {
        double? t1 = ParseNum(f.Value);
        if (t1 is null)
            return (false, false, $"'{rawEvidence}' vs bad threshold '{f.Value}'");

        bool satisfied;
        string opText;
        switch (f.Operator)
        {
            case ProtoFeatureOperator.GreaterThan: satisfied = actual > t1; opText = ">"; break;
            case ProtoFeatureOperator.GreaterOrEqual: satisfied = actual >= t1; opText = "≥"; break;
            case ProtoFeatureOperator.LessThan: satisfied = actual < t1; opText = "<"; break;
            case ProtoFeatureOperator.LessOrEqual: satisfied = actual <= t1; opText = "≤"; break;
            case ProtoFeatureOperator.Equals: satisfied = Math.Abs(actual - t1.Value) < 1e-9; opText = "="; break;
            case ProtoFeatureOperator.InRange:
                double? t2 = ParseNum(f.Value2);
                if (t2 is null) return (false, false, $"'{rawEvidence}' vs bad range");
                satisfied = actual >= t1 && actual <= t2;
                return (satisfied, true, $"{Fmt(actual)} in [{f.Value}, {f.Value2}]");
            default:
                return (false, false, $"'{rawEvidence}' — operator not valid for numeric");
        }
        return (satisfied, true, $"{Fmt(actual)} {opText} {f.Value}");
    }

    // ── Helpers ──────────────────────────────────────────────────────────
    private static string Norm(string code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    private static bool CodeMatches(string patientCode, string featureCode)
    {
        string fc = Norm(featureCode);
        string pc = Norm(patientCode);
        if (fc.EndsWith('*'))
            return pc.StartsWith(fc[..^1], StringComparison.Ordinal);
        return pc == fc;
    }

    private static double? ParseNum(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        // Keep the leading numeric token: "88 %", "> 100", "37.5°C" → 88 / 100 / 37.5.
        var chars = new List<char>();
        bool started = false;
        foreach (char c in raw.Trim())
        {
            if (char.IsDigit(c) || c == '.' || (c == '-' && !started) || (c == '+' && !started))
            {
                chars.Add(c);
                started = true;
            }
            else if (started)
            {
                break;
            }
        }
        return double.TryParse(new string(chars.ToArray()), NumberStyles.Float, CultureInfo.InvariantCulture, out double d)
            ? d : null;
    }

    private static bool IsStale(DateTime? measured, int? windowDays, DateTime asOf)
    {
        if (windowDays is null)
            return false;
        if (measured is null)
            return true; // recency required but unknown — cannot confirm, treat as not assessed
        return (asOf - measured.Value).TotalDays > windowDays.Value;
    }

    private static string Fmt(double d) => d.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Fmt(DateTime? d) => d?.ToString("yyyy-MM-dd") ?? "no date";
}
