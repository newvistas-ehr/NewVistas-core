// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Globalization;
using System.Text;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Reporting;

/// <summary>
/// Default CSV formatter for GPRA submission files. Produces a two-section
/// CSV: a single-line header row of report metadata, followed by one row per
/// indicator with current/baseline counts and percentages.
///
/// <para>
/// Column layout follows IHS conventions documented in past CIMGAGP/BQIGPRA
/// extracts; it is a reasonable starting point but is <b>not</b> guaranteed
/// to be byte-identical to the current IHS GPRA+ submission spec. A
/// deployment that has obtained the authoritative spec from the IHS Office
/// of Information Technology should register its own
/// <see cref="IGpraSubmissionFormatter"/> in DI (<see cref="IGpraSubmissionFormatter"/>).
/// </para>
///
/// <para>
/// Idempotent: same <see cref="GpraReportState"/> in → same string out, byte
/// for byte. Tests can pin the entire output if needed.
/// </para>
/// </summary>
public sealed class CsvGpraSubmissionFormatter : IGpraSubmissionFormatter
{
    public string FileExtension => ".csv";
    public string FormatVersion => "csv-v1";

    public string Format(GpraReportState report)
    {
        if (report is null)
            throw new ArgumentNullException(nameof(report));
        if (report.Status != GpraReportStatus.Completed)
            throw new ArgumentException(
                $"Report {report.ReportId} is not in Completed status (currently {report.Status}); cannot package for submission.",
                nameof(report));
        if (report.Indicators.Count == 0)
            throw new ArgumentException(
                $"Report {report.ReportId} has no indicator results; cannot package for submission.",
                nameof(report));

        var sb = new StringBuilder();

        // ── Header section: report-level metadata as a labelled key/value list.
        // IHS extracts traditionally embed metadata in a comment-prefixed
        // header; we use a leading "#" so the data rows are an unambiguous
        // CSV body. Submission consumers strip "#" lines before parsing.
        sb.AppendLine($"# GPRA Submission File ({FormatVersion})");
        sb.AppendLine($"# ReportId,{Esc(report.ReportId)}");
        sb.AppendLine($"# FiscalYear,{report.FiscalYear}");
        sb.AppendLine($"# ReportingPeriod,{report.ReportingPeriod}");
        sb.AppendLine($"# CurrentPeriodStart,{Iso(report.CurrentPeriodStart)}");
        sb.AppendLine($"# CurrentPeriodEnd,{Iso(report.CurrentPeriodEnd)}");
        sb.AppendLine($"# BaselinePeriodStart,{Iso(report.BaselinePeriodStart)}");
        sb.AppendLine($"# BaselinePeriodEnd,{Iso(report.BaselinePeriodEnd)}");
        sb.AppendLine($"# FacilityId,{Esc(report.FacilityId)}");
        sb.AppendLine($"# FacilityName,{Esc(report.FacilityName)}");
        sb.AppendLine($"# CommunityTaxonomy,{Esc(report.CommunityTaxonomy ?? string.Empty)}");
        sb.AppendLine($"# ActiveUserPopulation,{report.ActiveUserPopulation}");
        sb.AppendLine($"# IndicatorCount,{report.Indicators.Count}");

        // ── Data section: one row per indicator. Header row of column names
        // followed by data rows in the same order as report.Indicators.
        sb.AppendLine(
            "MeasureId,Title,Category," +
            "CurrentDenominator,CurrentNumerator,CurrentRatePct," +
            "BaselineDenominator,BaselineNumerator,BaselineRatePct," +
            "PercentagePointChange,Improved,TargetRatePct,TargetMet");

        foreach (GpraIndicatorResult ind in report.Indicators)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                Esc(ind.MeasureId),
                Esc(ind.Title),
                ind.Category.ToString(),
                ind.CurrentDenominator.ToString(CultureInfo.InvariantCulture),
                ind.CurrentNumerator.ToString(CultureInfo.InvariantCulture),
                Pct(ind.CurrentPerformanceRate),
                ind.BaselineDenominator.ToString(CultureInfo.InvariantCulture),
                ind.BaselineNumerator.ToString(CultureInfo.InvariantCulture),
                Pct(ind.BaselinePerformanceRate),
                Pct(ind.PercentagePointChange),
                ind.IsImproved ? "Y" : "N",
                ind.TargetRate.HasValue ? Pct(ind.TargetRate.Value) : string.Empty,
                ind.TargetMet ? "Y" : "N",
            }));
        }

        return sb.ToString();
    }

    /// <summary>CSV-escape a string field. Quotes wrap any field containing comma, quote, or newline; embedded quotes double.</summary>
    private static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        bool needsQuotes = s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
        if (!needsQuotes) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private static string Iso(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Pct(decimal rate) => rate.ToString("0.##", CultureInfo.InvariantCulture);
}
