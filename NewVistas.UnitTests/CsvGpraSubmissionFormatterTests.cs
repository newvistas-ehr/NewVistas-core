// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Reporting;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for <see cref="CsvGpraSubmissionFormatter"/> — the default
/// stand-in CSV format. Pin behaviour so the eventual swap to the official
/// IHS GPRA+ submission spec is a deliberate test update.
/// </summary>
[TestFixture]
public class CsvGpraSubmissionFormatterTests
{
    private static GpraReportState BuildCompletedReport(int indicatorCount = 1)
    {
        var state = new GpraReportState
        {
            ReportId = "GPRA-REPORT:fy2026-Q1-tribal",
            Status = GpraReportStatus.Completed,
            FiscalYear = 2026,
            ReportingPeriod = GpraReportingPeriod.Quarter1,
            CurrentPeriodStart = new DateTime(2025, 10, 1),
            CurrentPeriodEnd = new DateTime(2025, 12, 31),
            BaselinePeriodStart = new DateTime(2022, 10, 1),
            BaselinePeriodEnd = new DateTime(2022, 12, 31),
            FacilityId = "TRIBAL-HUB",
            FacilityName = "Tribal Health Authority Hub",
            CommunityTaxonomy = "AUTTAX-DEMO",
            ActiveUserPopulation = 5000,
        };
        for (int i = 1; i <= indicatorCount; i++)
        {
            state.Indicators.Add(new GpraIndicatorResult
            {
                MeasureId = $"GPRA-DM-{i:D2}",
                Title = $"Diabetes indicator #{i}",
                Category = GpraClinicalCategory.Diabetes,
                CurrentDenominator = 200,
                CurrentNumerator = 150,
                CurrentPerformanceRate = 75.0m,
                BaselineDenominator = 180,
                BaselineNumerator = 120,
                BaselinePerformanceRate = 66.67m,
                PercentagePointChange = 8.33m,
                IsImproved = true,
                TargetRate = 80m,
                TargetMet = false,
            });
        }
        return state;
    }

    private static CsvGpraSubmissionFormatter Formatter() => new();

    [Test]
    public void Format_CompletedReportWithIndicators_ProducesNonEmptyContent()
    {
        string output = Formatter().Format(BuildCompletedReport());
        Assert.That(output, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Format_OutputHeaderSection_ContainsFiscalYearAndFacility()
    {
        string output = Formatter().Format(BuildCompletedReport());
        Assert.That(output, Does.Contain("# FiscalYear,2026"));
        Assert.That(output, Does.Contain("# FacilityId,TRIBAL-HUB"));
        Assert.That(output, Does.Contain("# FacilityName,Tribal Health Authority Hub"));
        Assert.That(output, Does.Contain("# ActiveUserPopulation,5000"));
    }

    [Test]
    public void Format_DataSection_HasOneRowPerIndicator()
    {
        string output = Formatter().Format(BuildCompletedReport(indicatorCount: 3));
        // Three data rows beyond the column-header row.
        int dataRows = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Count(line => line.StartsWith("GPRA-DM-"));
        Assert.That(dataRows, Is.EqualTo(3));
    }

    [Test]
    public void Format_ColumnHeaderRow_PrecedesDataRows()
    {
        string output = Formatter().Format(BuildCompletedReport());
        int headerIdx = output.IndexOf("MeasureId,Title,Category", StringComparison.Ordinal);
        int firstDataIdx = output.IndexOf("GPRA-DM-01", StringComparison.Ordinal);
        Assert.That(headerIdx, Is.GreaterThan(0));
        Assert.That(firstDataIdx, Is.GreaterThan(headerIdx));
    }

    [Test]
    public void Format_FieldsWithCommas_AreQuoted()
    {
        GpraReportState report = BuildCompletedReport();
        report.Indicators[0].Title = "Diabetes, foot exam, age 40+";
        string output = Formatter().Format(report);
        Assert.That(output, Does.Contain("\"Diabetes, foot exam, age 40+\""));
    }

    [Test]
    public void Format_FieldsWithQuotes_AreEscapedByDoubling()
    {
        GpraReportState report = BuildCompletedReport();
        report.Indicators[0].Title = "Test \"quoted\" title";
        string output = Formatter().Format(report);
        Assert.That(output, Does.Contain("\"Test \"\"quoted\"\" title\""));
    }

    [Test]
    public void Format_IsDeterministic_SameInputProducesSameOutput()
    {
        GpraReportState report = BuildCompletedReport(2);
        string a = Formatter().Format(report);
        string b = Formatter().Format(report);
        Assert.That(b, Is.EqualTo(a));
    }

    [Test]
    public void Format_ImprovedFlag_RendersAsYOrN()
    {
        GpraReportState report = BuildCompletedReport();
        report.Indicators[0].IsImproved = true;
        report.Indicators[0].TargetMet = false;
        string output = Formatter().Format(report);
        // Last three columns of the data row: PercentagePointChange,Improved,TargetRatePct,TargetMet
        // Just verify "Y" and "N" both appear in the data row (Improved=Y, TargetMet=N).
        string dataRow = output.Split('\n').First(l => l.StartsWith("GPRA-DM-"));
        Assert.That(dataRow, Does.Contain(",Y,"));
        Assert.That(dataRow, Does.EndWith(",N").Or.EndWith(",N\r"));
    }

    [Test]
    public void Format_InProgressReport_Throws()
    {
        GpraReportState report = BuildCompletedReport();
        report.Status = GpraReportStatus.Evaluating;
        Assert.That(() => Formatter().Format(report),
            Throws.ArgumentException.With.Message.Contains("Completed"));
    }

    [Test]
    public void Format_ReportWithNoIndicators_Throws()
    {
        GpraReportState report = BuildCompletedReport(indicatorCount: 0);
        Assert.That(() => Formatter().Format(report),
            Throws.ArgumentException.With.Message.Contains("no indicator"));
    }

    [Test]
    public void Format_NullReport_Throws()
    {
        Assert.That(() => Formatter().Format(null!), Throws.ArgumentNullException);
    }

    [Test]
    public void FileExtensionAndVersion_AreStable()
    {
        var f = Formatter();
        Assert.That(f.FileExtension, Is.EqualTo(".csv"));
        Assert.That(f.FormatVersion, Is.EqualTo("csv-v1"));
    }
}
