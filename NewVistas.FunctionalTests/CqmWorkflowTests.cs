// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Grains;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Clinical Quality Measures (CQM) / QRDA reporting workflow.
/// §170.315(c)(1) — Record and export CQM data (QRDA Category I)
/// §170.315(c)(2) — Import and calculate quality measures
/// §170.315(c)(3) — Report quality measures (QRDA Category III)
/// §170.315(c)(4) — Filter by demographics (age, sex, race, ethnicity, payer)
///
/// Tests the full lifecycle:
///   1. Define eCQM measures with criteria
///   2. Register patients with clinical data (problems, labs, vitals, meds)
///   3. Evaluate measures across patient populations
///   4. Export QRDA Category I (patient-level) and Category III (aggregate)
///   5. Filter results by demographics and re-export
/// </summary>
[TestFixture]
public class CqmWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Full CQM Lifecycle ──────────────────────────────────────────────────

    [Test]
    public async Task FullWorkflow_DefineMeasure_EvaluatePatients_ExportQrda()
    {
        // 1. Define a diabetes HbA1c measure (simplified CMS122)
        string measureId = "CMS122-TEST";
        ICqmMeasureGrain measureGrain = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");
        await measureGrain.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureId,
            Title = "Diabetes: HbA1c Poor Control (>9%)",
            Description = "Percentage of patients 18-75 with diabetes whose HbA1c > 9%",
            NqfNumber = "0059",
            Version = "12",
            Steward = "NCQA",
            MeasureType = "proportion",
            ClinicalDomain = "diabetes",
            IsActive = true,
            ReportingPrograms = new List<string> { "MIPS", "CPC+" },
            InitialPopulation = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "between",
                    ComparisonValue = "18", ComparisonValue2 = "75", Description = "Age 18-75" },
                new() { DataSource = "Problem", ValueSetOrCode = "E11.*", Operator = "exists",
                    Description = "Type 2 diabetes diagnosis" }
            },
            Numerator = new List<CqmCriterion>
            {
                new() { DataSource = "Lab", ValueSetOrCode = "4548-4", Operator = "less-than-or-equal",
                    ComparisonValue = "9", Description = "HbA1c ≤ 9% (good control)" }
            }
        });

        // 2. Register in index
        ICqmMeasureIndexGrain index = _cluster.GrainFactory.GetGrain<ICqmMeasureIndexGrain>("CQM-INDEX");
        await index.AddMeasureAsync(new CqmMeasureSummary
        {
            MeasureId = measureId,
            Title = "Diabetes: HbA1c Poor Control (>9%)",
            ClinicalDomain = "diabetes",
            MeasureType = "proportion",
            IsActive = true,
            NqfNumber = "0059"
        });

        // 3. Create patients with diabetes and lab data
        DateTime periodEnd = DateTime.UtcNow;
        DateTime periodStart = periodEnd.AddYears(-1);

        // Patient A: diabetic, HbA1c 7.2% — IN numerator (good control)
        string pidA = $"PATIENT-CQM-A-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wA = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pidA);
        await wA.UpdateDemographicsAsync("DIABETES,GOOD", "M", DateTime.UtcNow.AddYears(-55), "111-11-1111");
        await wA.AddProblemAsync("Type 2 Diabetes", "E11.65", "active", null, null,
            null, null, null, null, false, null);
        IPatientLabSummary labA = _cluster.GrainFactory.GetGrain<IPatientLabSummary>($"PatientLabSummary/{pidA}");
        await labA.RecordNewResult(new LabResultEvent
        {
            PatientIcn = pidA, ResultId = Guid.NewGuid().ToString(), LoincCode = "4548-4",
            TestName = "HbA1c", Value = "7.2", Units = "%", ReferenceRange = "4.0-5.6",
            AbnormalFlag = LabAbnormalFlag.High, ResultDate = DateTimeOffset.UtcNow.AddDays(-30), FacilityCode = "FACILITY-1"
        });

        // Patient B: diabetic, HbA1c 10.5% — NOT in numerator (poor control)
        string pidB = $"PATIENT-CQM-B-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wB = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pidB);
        await wB.UpdateDemographicsAsync("DIABETES,POOR", "F", DateTime.UtcNow.AddYears(-45), "222-22-2222");
        await wB.AddProblemAsync("Type 2 Diabetes", "E11.9", "active", null, null,
            null, null, null, null, false, null);
        IPatientLabSummary labB = _cluster.GrainFactory.GetGrain<IPatientLabSummary>($"PatientLabSummary/{pidB}");
        await labB.RecordNewResult(new LabResultEvent
        {
            PatientIcn = pidB, ResultId = Guid.NewGuid().ToString(), LoincCode = "4548-4",
            TestName = "HbA1c", Value = "10.5", Units = "%", ReferenceRange = "4.0-5.6",
            AbnormalFlag = LabAbnormalFlag.CriticalHigh, ResultDate = DateTimeOffset.UtcNow.AddDays(-15), FacilityCode = "FACILITY-1"
        });

        // Patient C: NO diabetes — NOT in initial population
        string pidC = $"PATIENT-CQM-C-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wC = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pidC);
        await wC.UpdateDemographicsAsync("HEALTHY,CONTROL", "M", DateTime.UtcNow.AddYears(-35), "333-33-3333");

        // 4. Evaluate
        string reportId = $"CQM-REPORT:{Guid.NewGuid():N}";
        ICqmReportGrain reportGrain = _cluster.GrainFactory.GetGrain<ICqmReportGrain>(reportId);
        await reportGrain.EvaluateAsync(measureId,
            new List<string> { pidA, pidB, pidC },
            periodStart, periodEnd, "quality-team");

        CqmReportState report = await reportGrain.GetReportAsync();
        Assert.That(report.Status, Is.EqualTo("completed"));
        Assert.That(report.MeasureId, Is.EqualTo(measureId));
        Assert.That(report.InitialPopulationCount, Is.EqualTo(2)); // A + B (diabetics)
        Assert.That(report.DenominatorCount, Is.EqualTo(2));
        Assert.That(report.NumeratorCount, Is.EqualTo(1)); // Only A (HbA1c ≤ 9)
        Assert.That(report.PerformanceRate, Is.EqualTo(50.0));
        Assert.That(report.EvaluatedBy, Is.EqualTo("quality-team"));

        // Patient C not in initial population
        CqmPatientResult resultC = report.PatientResults.First(r => r.PatientId == pidC);
        Assert.That(resultC.InInitialPopulation, Is.False);

        // 5. Export QRDA Category I for Patient A
        string qrdaI = await reportGrain.ExportQrdaCategoryIAsync(pidA);
        Assert.That(qrdaI, Does.Contain("ClinicalDocument"));
        Assert.That(qrdaI, Does.Contain("2.16.840.1.113883.10.20.24.1.1"));
        Assert.That(qrdaI, Does.Contain(pidA));
        Assert.That(qrdaI, Does.Contain("HbA1c"));

        // 6. Export QRDA Category III
        string qrdaIII = await reportGrain.ExportQrdaCategoryIIIAsync();
        Assert.That(qrdaIII, Does.Contain("ClinicalDocument"));
        Assert.That(qrdaIII, Does.Contain("2.16.840.1.113883.10.20.27.1.1"));
        Assert.That(qrdaIII, Does.Contain("Performance Rate"));

        // 7. Filter by sex (females only) — §170.315(c)(4)
        CqmReportState femaleReport = await reportGrain.GetFilteredReportAsync(new CqmFilterCriteria { Sex = "F" });
        Assert.That(femaleReport.PatientResults, Has.Count.EqualTo(1));
        Assert.That(femaleReport.PatientResults[0].PatientName, Does.Contain("POOR"));
        Assert.That(femaleReport.NumeratorCount, Is.EqualTo(0)); // Patient B not in numerator

        // 8. Verify index listing
        List<CqmMeasureSummary> measures = await index.GetAllMeasuresAsync();
        Assert.That(measures.Any(m => m.MeasureId == measureId), Is.True);
    }

    // ─── Problem-Based Evaluation ────────────────────────────────────────────

    [Test]
    public async Task ProblemBasedMeasure_WildcardMatching()
    {
        string measureId = $"CMS-PROB-{Guid.NewGuid():N}";
        ICqmMeasureGrain measureGrain = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");
        await measureGrain.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureId,
            Title = "Hypertension Control Test",
            InitialPopulation = new List<CqmCriterion>
            {
                new() { DataSource = "Problem", ValueSetOrCode = "I10.*", Operator = "exists",
                    Description = "Essential hypertension" }
            },
            Numerator = new List<CqmCriterion>
            {
                new() { DataSource = "Problem", ValueSetOrCode = "I10.*", Operator = "exists",
                    Description = "Has hypertension (everyone in IP qualifies)" }
            }
        });

        string pid = $"PATIENT-CQM-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid);
        await w.UpdateDemographicsAsync("HYPER,TENSION", "M", DateTime.UtcNow.AddYears(-60), "444-44-4444");
        await w.AddProblemAsync("Essential Hypertension", "I10", "active", null, null,
            null, null, null, null, false, null);

        string reportId = $"CQM-REPORT:{Guid.NewGuid():N}";
        ICqmReportGrain reportGrain = _cluster.GrainFactory.GetGrain<ICqmReportGrain>(reportId);
        await reportGrain.EvaluateAsync(measureId, new List<string> { pid },
            DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, "tester");

        CqmReportState report = await reportGrain.GetReportAsync();
        Assert.That(report.InitialPopulationCount, Is.EqualTo(1));
        Assert.That(report.PatientResults[0].Evidence.Any(e => e.Contains("Hypertension")), Is.True);
    }

    // ─── Multiple Measures ───────────────────────────────────────────────────

    [Test]
    public async Task MultipleMeasures_IndependentEvaluation()
    {
        // Measure A: age > 18
        string measureIdA = $"CMS-MULTI-A-{Guid.NewGuid():N}";
        ICqmMeasureGrain measureA = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureIdA}");
        await measureA.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureIdA, Title = "Measure A",
            InitialPopulation = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "greater-than",
                    ComparisonValue = "18" }
            },
            Numerator = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "greater-than",
                    ComparisonValue = "18" }
            }
        });

        // Measure B: age > 50
        string measureIdB = $"CMS-MULTI-B-{Guid.NewGuid():N}";
        ICqmMeasureGrain measureB = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureIdB}");
        await measureB.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureIdB, Title = "Measure B",
            InitialPopulation = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "greater-than",
                    ComparisonValue = "50" }
            },
            Numerator = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "greater-than",
                    ComparisonValue = "50" }
            }
        });

        // Patient age 30 — qualifies for A, not B
        string pid = $"PATIENT-CQM-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid);
        await w.UpdateDemographicsAsync("MULTI,PATIENT", "F", DateTime.UtcNow.AddYears(-30), "555-55-5555");

        string reportIdA = $"CQM-REPORT:{Guid.NewGuid():N}";
        ICqmReportGrain reportGrainA = _cluster.GrainFactory.GetGrain<ICqmReportGrain>(reportIdA);
        await reportGrainA.EvaluateAsync(measureIdA, new List<string> { pid },
            DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, "tester");

        string reportIdB = $"CQM-REPORT:{Guid.NewGuid():N}";
        ICqmReportGrain reportGrainB = _cluster.GrainFactory.GetGrain<ICqmReportGrain>(reportIdB);
        await reportGrainB.EvaluateAsync(measureIdB, new List<string> { pid },
            DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, "tester");

        CqmReportState reportA = await reportGrainA.GetReportAsync();
        CqmReportState reportB = await reportGrainB.GetReportAsync();

        Assert.That(reportA.InitialPopulationCount, Is.EqualTo(1));
        Assert.That(reportB.InitialPopulationCount, Is.EqualTo(0));
    }

    // ─── Measure Index Workflow ──────────────────────────────────────────────

    [Test]
    public async Task MeasureIndex_ActivateDeactivate_ReflectsInListing()
    {
        string indexKey = $"CQM-INDEX-FT-{Guid.NewGuid():N}";
        ICqmMeasureIndexGrain index = _cluster.GrainFactory.GetGrain<ICqmMeasureIndexGrain>(indexKey);

        await index.AddMeasureAsync(new CqmMeasureSummary
        {
            MeasureId = "CMS122", Title = "Diabetes HbA1c", IsActive = true
        });
        await index.AddMeasureAsync(new CqmMeasureSummary
        {
            MeasureId = "CMS165", Title = "Blood Pressure Control", IsActive = true
        });
        await index.AddMeasureAsync(new CqmMeasureSummary
        {
            MeasureId = "CMS69", Title = "BMI Screening", IsActive = false
        });

        List<CqmMeasureSummary> active = await index.GetActiveMeasuresAsync();
        Assert.That(active, Has.Count.EqualTo(2));
        Assert.That(active.Any(m => m.MeasureId == "CMS69"), Is.False);

        List<CqmMeasureSummary> all = await index.GetAllMeasuresAsync();
        Assert.That(all, Has.Count.EqualTo(3));
    }

    // ─── Demographic Filtering Combinations ──────────────────────────────────

    [Test]
    public async Task DemographicFiltering_MultipleFilters_Intersect()
    {
        string measureId = $"CMS-DEMOFILT-{Guid.NewGuid():N}";
        ICqmMeasureGrain measureGrain = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");
        await measureGrain.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureId, Title = "Demo Filter Test",
            InitialPopulation = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "greater-than",
                    ComparisonValue = "0" }
            },
            Numerator = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "greater-than",
                    ComparisonValue = "0" }
            }
        });

        // Create 4 patients with different demographics
        string pid1 = $"P-{Guid.NewGuid():N}";
        string pid2 = $"P-{Guid.NewGuid():N}";
        string pid3 = $"P-{Guid.NewGuid():N}";
        string pid4 = $"P-{Guid.NewGuid():N}";

        IPatientWorkflowGrain w1 = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid1);
        IPatientWorkflowGrain w2 = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid2);
        IPatientWorkflowGrain w3 = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid3);
        IPatientWorkflowGrain w4 = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid4);

        await w1.UpdateDemographicsAsync("DEMO,ONE", "M", DateTime.UtcNow.AddYears(-30), "600-00-0001");
        await w2.UpdateDemographicsAsync("DEMO,TWO", "F", DateTime.UtcNow.AddYears(-40), "600-00-0002");
        await w3.UpdateDemographicsAsync("DEMO,THREE", "M", DateTime.UtcNow.AddYears(-60), "600-00-0003");
        await w4.UpdateDemographicsAsync("DEMO,FOUR", "F", DateTime.UtcNow.AddYears(-70), "600-00-0004");

        string reportId = $"CQM-REPORT:{Guid.NewGuid():N}";
        ICqmReportGrain reportGrain = _cluster.GrainFactory.GetGrain<ICqmReportGrain>(reportId);
        await reportGrain.EvaluateAsync(measureId, new List<string> { pid1, pid2, pid3, pid4 },
            DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, "tester");

        // Filter: Males over 50 — should only return patient 3 (age 60, M)
        CqmReportState filtered = await reportGrain.GetFilteredReportAsync(
            new CqmFilterCriteria { Sex = "M", MinAge = 50 });

        Assert.That(filtered.PatientResults, Has.Count.EqualTo(1));
        Assert.That(filtered.PatientResults[0].Age, Is.EqualTo(60));
        Assert.That(filtered.PatientResults[0].Sex, Is.EqualTo("M"));
    }

    // ─── QRDA Category I Evidence ────────────────────────────────────────────

    [Test]
    public async Task QrdaCategoryI_ContainsClinicalEvidence()
    {
        string measureId = $"CMS-EV-{Guid.NewGuid():N}";
        ICqmMeasureGrain measureGrain = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");
        await measureGrain.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureId, Title = "Evidence Test",
            InitialPopulation = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "greater-than",
                    ComparisonValue = "0", Description = "Any adult" }
            },
            Numerator = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "greater-than",
                    ComparisonValue = "0", Description = "Qualifies" }
            }
        });

        string pid = $"PATIENT-CQM-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid);
        await w.UpdateDemographicsAsync("EVIDENCE,TEST", "F", DateTime.UtcNow.AddYears(-42), "700-00-0001");

        string reportId = $"CQM-REPORT:{Guid.NewGuid():N}";
        ICqmReportGrain reportGrain = _cluster.GrainFactory.GetGrain<ICqmReportGrain>(reportId);
        await reportGrain.EvaluateAsync(measureId, new List<string> { pid },
            DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, "tester");

        string xml = await reportGrain.ExportQrdaCategoryIAsync(pid);

        // Verify QRDA I structure
        Assert.That(xml, Does.Contain("ClinicalDocument"));
        Assert.That(xml, Does.Contain("recordTarget"));
        Assert.That(xml, Does.Contain("EVIDENCE"));
        Assert.That(xml, Does.Contain("structuredBody"));
    }

    // ─── Filtered QRDA III Export ────────────────────────────────────────────

    [Test]
    public async Task FilteredQrdaIII_GeneratesValidXml()
    {
        string measureId = $"CMS-FILT3-{Guid.NewGuid():N}";
        ICqmMeasureGrain measureGrain = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");
        await measureGrain.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureId, Title = "Filtered Export Test",
            InitialPopulation = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "greater-than",
                    ComparisonValue = "0" }
            },
            Numerator = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "greater-than",
                    ComparisonValue = "40" }
            }
        });

        string pid1 = $"P-{Guid.NewGuid():N}";
        string pid2 = $"P-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w1 = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid1);
        IPatientWorkflowGrain w2 = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid2);
        await w1.UpdateDemographicsAsync("FILT,YOUNG", "M", DateTime.UtcNow.AddYears(-25), "800-00-0001");
        await w2.UpdateDemographicsAsync("FILT,OLD", "F", DateTime.UtcNow.AddYears(-55), "800-00-0002");

        string reportId = $"CQM-REPORT:{Guid.NewGuid():N}";
        ICqmReportGrain reportGrain = _cluster.GrainFactory.GetGrain<ICqmReportGrain>(reportId);
        await reportGrain.EvaluateAsync(measureId, new List<string> { pid1, pid2 },
            DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, "tester");

        // Filter to females only, then export QRDA III
        string xml = await reportGrain.ExportFilteredQrdaCategoryIIIAsync(new CqmFilterCriteria { Sex = "F" });
        Assert.That(xml, Does.Contain("ClinicalDocument"));
        Assert.That(xml, Does.Contain("2.16.840.1.113883.10.20.27.1.1"));
        Assert.That(xml, Does.Contain(measureId));
    }

    // ─── Denominator Exclusion ───────────────────────────────────────────────

    [Test]
    public async Task DenominatorExclusion_ExcludesFromPerformanceRate()
    {
        string measureId = $"CMS-DENEX-{Guid.NewGuid():N}";
        ICqmMeasureGrain measureGrain = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");
        await measureGrain.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureId,
            Title = "Denominator Exclusion Test",
            InitialPopulation = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "greater-than",
                    ComparisonValue = "0" }
            },
            DenominatorExclusions = new List<CqmCriterion>
            {
                new() { DataSource = "Problem", ValueSetOrCode = "Z51.5", Operator = "exists",
                    Description = "Hospice care (palliative)" }
            },
            Numerator = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "greater-than",
                    ComparisonValue = "0" }
            }
        });

        // Patient A: no exclusion — IN denominator and numerator
        string pidA = $"P-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wA = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pidA);
        await wA.UpdateDemographicsAsync("DENEX,NORMAL", "M", DateTime.UtcNow.AddYears(-50), "900-00-0001");

        // Patient B: hospice — EXCLUDED from denominator
        string pidB = $"P-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wB = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pidB);
        await wB.UpdateDemographicsAsync("DENEX,HOSPICE", "F", DateTime.UtcNow.AddYears(-80), "900-00-0002");
        await wB.AddProblemAsync("Encounter for palliative care", "Z51.5", "active", null, null,
            null, null, null, null, false, null);

        string reportId = $"CQM-REPORT:{Guid.NewGuid():N}";
        ICqmReportGrain reportGrain = _cluster.GrainFactory.GetGrain<ICqmReportGrain>(reportId);
        await reportGrain.EvaluateAsync(measureId, new List<string> { pidA, pidB },
            DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, "tester");

        CqmReportState report = await reportGrain.GetReportAsync();
        Assert.That(report.InitialPopulationCount, Is.EqualTo(2));
        Assert.That(report.DenominatorExclusionCount, Is.EqualTo(1));
        Assert.That(report.DenominatorCount, Is.EqualTo(1)); // Only A
        Assert.That(report.NumeratorCount, Is.EqualTo(1));
        Assert.That(report.PerformanceRate, Is.EqualTo(100.0));

        // Verify exclusion reason recorded
        CqmPatientResult resultB = report.PatientResults.First(r => r.PatientId == pidB);
        Assert.That(resultB.IsDenominatorExclusion, Is.True);
        Assert.That(resultB.ExclusionReason, Does.Contain("palliative"));
    }
}
