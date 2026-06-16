// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for Clinical Quality Measure (CQM) grains.
/// §170.315(c)(1-4) — CQM recording, calculation, reporting, and filtering.
/// </summary>
[TestFixture]
public class CqmGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Measure CRUD ────────────────────────────────────────────────────────

    [Test]
    public async Task CqmMeasure_SaveAndRetrieve()
    {
        string measureId = $"CMS-{Guid.NewGuid():N}";
        ICqmMeasureGrain grain = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");

        await grain.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureId,
            Title = "Test Measure",
            Description = "A test quality measure",
            MeasureType = "proportion",
            ClinicalDomain = "testing",
            Version = "1",
            Steward = "Test Org",
            IsActive = true,
            InitialPopulation = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "between", ComparisonValue = "18", ComparisonValue2 = "85" }
            }
        });

        CqmMeasureState result = await grain.GetMeasureAsync();
        Assert.That(result.MeasureId, Is.EqualTo(measureId));
        Assert.That(result.Title, Is.EqualTo("Test Measure"));
        Assert.That(result.MeasureType, Is.EqualTo("proportion"));
        Assert.That(result.ClinicalDomain, Is.EqualTo("testing"));
        Assert.That(result.IsActive, Is.True);
        Assert.That(result.InitialPopulation, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task CqmMeasure_SetActive_TogglesState()
    {
        string measureId = $"CMS-{Guid.NewGuid():N}";
        ICqmMeasureGrain grain = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");

        await grain.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureId, Title = "Active Toggle Test", IsActive = true
        });

        await grain.SetActiveAsync(false);
        CqmMeasureState result = await grain.GetMeasureAsync();
        Assert.That(result.IsActive, Is.False);

        await grain.SetActiveAsync(true);
        result = await grain.GetMeasureAsync();
        Assert.That(result.IsActive, Is.True);
    }

    [Test]
    public async Task CqmMeasure_SaveUpdatesExisting()
    {
        string measureId = $"CMS-{Guid.NewGuid():N}";
        ICqmMeasureGrain grain = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");

        await grain.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureId, Title = "Version 1", Version = "1"
        });

        await grain.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureId, Title = "Version 2", Version = "2"
        });

        CqmMeasureState result = await grain.GetMeasureAsync();
        Assert.That(result.Title, Is.EqualTo("Version 2"));
        Assert.That(result.Version, Is.EqualTo("2"));
    }

    // ─── Measure Index ───────────────────────────────────────────────────────

    [Test]
    public async Task CqmMeasureIndex_AddAndListMeasures()
    {
        string indexKey = $"CQM-INDEX-{Guid.NewGuid():N}";
        ICqmMeasureIndexGrain index = _cluster.GrainFactory.GetGrain<ICqmMeasureIndexGrain>(indexKey);

        for (int i = 0; i < 4; i++)
        {
            await index.AddMeasureAsync(new CqmMeasureSummary
            {
                MeasureId = $"CMS-TEST-{i}",
                Title = $"Test Measure {i}",
                ClinicalDomain = "testing",
                MeasureType = "proportion",
                IsActive = i != 2
            });
        }

        List<CqmMeasureSummary> all = await index.GetAllMeasuresAsync();
        Assert.That(all, Has.Count.EqualTo(4));

        List<CqmMeasureSummary> active = await index.GetActiveMeasuresAsync();
        Assert.That(active, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task CqmMeasureIndex_RemoveMeasure()
    {
        string indexKey = $"CQM-INDEX-{Guid.NewGuid():N}";
        ICqmMeasureIndexGrain index = _cluster.GrainFactory.GetGrain<ICqmMeasureIndexGrain>(indexKey);

        await index.AddMeasureAsync(new CqmMeasureSummary { MeasureId = "A", Title = "Measure A" });
        await index.AddMeasureAsync(new CqmMeasureSummary { MeasureId = "B", Title = "Measure B" });

        await index.RemoveMeasureAsync("A");
        List<CqmMeasureSummary> remaining = await index.GetAllMeasuresAsync();
        Assert.That(remaining, Has.Count.EqualTo(1));
        Assert.That(remaining[0].MeasureId, Is.EqualTo("B"));
    }

    [Test]
    public async Task CqmMeasureIndex_AddDuplicate_ReplacesExisting()
    {
        string indexKey = $"CQM-INDEX-{Guid.NewGuid():N}";
        ICqmMeasureIndexGrain index = _cluster.GrainFactory.GetGrain<ICqmMeasureIndexGrain>(indexKey);

        await index.AddMeasureAsync(new CqmMeasureSummary { MeasureId = "DUP", Title = "Original" });
        await index.AddMeasureAsync(new CqmMeasureSummary { MeasureId = "DUP", Title = "Updated" });

        List<CqmMeasureSummary> all = await index.GetAllMeasuresAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Title, Is.EqualTo("Updated"));
    }

    // ─── Report — Basic Evaluation ───────────────────────────────────────────

    [Test]
    public async Task CqmReport_EvaluateWithNoPatients_CompletesEmpty()
    {
        string measureId = $"CMS-EMPTY-{Guid.NewGuid():N}";
        ICqmMeasureGrain measureGrain = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");
        await measureGrain.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureId,
            Title = "Empty Measure",
            InitialPopulation = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "between", ComparisonValue = "18", ComparisonValue2 = "85" }
            }
        });

        string reportId = $"CQM-REPORT:{Guid.NewGuid():N}";
        ICqmReportGrain reportGrain = _cluster.GrainFactory.GetGrain<ICqmReportGrain>(reportId);

        await reportGrain.EvaluateAsync(measureId, new List<string>(), DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, "tester");

        CqmReportState report = await reportGrain.GetReportAsync();
        Assert.That(report.Status, Is.EqualTo("completed"));
        Assert.That(report.PatientResults, Has.Count.EqualTo(0));
        Assert.That(report.InitialPopulationCount, Is.EqualTo(0));
    }

    [Test]
    public async Task CqmReport_EvaluateMissingMeasure_SetsError()
    {
        string reportId = $"CQM-REPORT:{Guid.NewGuid():N}";
        ICqmReportGrain reportGrain = _cluster.GrainFactory.GetGrain<ICqmReportGrain>(reportId);

        await reportGrain.EvaluateAsync("NONEXISTENT", new List<string> { "P1" },
            DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, "tester");

        CqmReportState report = await reportGrain.GetReportAsync();
        Assert.That(report.Status, Is.EqualTo("error"));
        Assert.That(report.ErrorMessage, Does.Contain("not found"));
    }

    [Test]
    public async Task CqmReport_EvaluateAgeDemographic_FiltersCorrectly()
    {
        // Create a measure that requires age 18-65
        string measureId = $"CMS-AGE-{Guid.NewGuid():N}";
        ICqmMeasureGrain measureGrain = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");
        await measureGrain.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureId,
            Title = "Age Filter Test",
            InitialPopulation = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "between",
                    ComparisonValue = "18", ComparisonValue2 = "65", Description = "Age 18-65" }
            },
            Numerator = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "greater-than",
                    ComparisonValue = "0", Description = "Any age (all qualify)" }
            }
        });

        // Create patient with DOB making them 40
        string patientId = $"PATIENT-CQM-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("CQM,TEST PATIENT", "M", DateTime.UtcNow.AddYears(-40), "000-00-0000");

        DateTime periodEnd = DateTime.UtcNow;
        DateTime periodStart = periodEnd.AddYears(-1);

        string reportId = $"CQM-REPORT:{Guid.NewGuid():N}";
        ICqmReportGrain reportGrain = _cluster.GrainFactory.GetGrain<ICqmReportGrain>(reportId);
        await reportGrain.EvaluateAsync(measureId, new List<string> { patientId }, periodStart, periodEnd, "tester");

        CqmReportState report = await reportGrain.GetReportAsync();
        Assert.That(report.Status, Is.EqualTo("completed"));
        Assert.That(report.InitialPopulationCount, Is.EqualTo(1));
        Assert.That(report.PatientResults[0].InInitialPopulation, Is.True);
        Assert.That(report.PatientResults[0].Age, Is.EqualTo(40));
    }

    [Test]
    public async Task CqmReport_PatientOutsideAgeRange_NotInPopulation()
    {
        string measureId = $"CMS-AGEOUT-{Guid.NewGuid():N}";
        ICqmMeasureGrain measureGrain = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");
        await measureGrain.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureId,
            Title = "Age Range Exclusion Test",
            InitialPopulation = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "between",
                    ComparisonValue = "18", ComparisonValue2 = "65" }
            }
        });

        // Patient is 10 years old — outside range
        string patientId = $"PATIENT-CQM-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("CQM,CHILD", "F", DateTime.UtcNow.AddYears(-10), "000-00-0001");

        string reportId = $"CQM-REPORT:{Guid.NewGuid():N}";
        ICqmReportGrain reportGrain = _cluster.GrainFactory.GetGrain<ICqmReportGrain>(reportId);
        await reportGrain.EvaluateAsync(measureId, new List<string> { patientId },
            DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, "tester");

        CqmReportState report = await reportGrain.GetReportAsync();
        Assert.That(report.InitialPopulationCount, Is.EqualTo(0));
        Assert.That(report.PatientResults[0].InInitialPopulation, Is.False);
    }

    // ─── Filtering (§170.315(c)(4)) ──────────────────────────────────────────

    [Test]
    public async Task CqmReport_FilterBySex()
    {
        string measureId = $"CMS-FILTER-{Guid.NewGuid():N}";
        ICqmMeasureGrain measureGrain = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");
        await measureGrain.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureId,
            Title = "Filter Test",
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

        // Create male and female patients
        string maleId = $"PATIENT-CQM-M-{Guid.NewGuid():N}";
        string femaleId = $"PATIENT-CQM-F-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wM = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(maleId);
        IPatientWorkflowGrain wF = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(femaleId);
        await wM.UpdateDemographicsAsync("CQM,MALE", "M", DateTime.UtcNow.AddYears(-50), "000-00-0002");
        await wF.UpdateDemographicsAsync("CQM,FEMALE", "F", DateTime.UtcNow.AddYears(-45), "000-00-0003");

        string reportId = $"CQM-REPORT:{Guid.NewGuid():N}";
        ICqmReportGrain reportGrain = _cluster.GrainFactory.GetGrain<ICqmReportGrain>(reportId);
        await reportGrain.EvaluateAsync(measureId, new List<string> { maleId, femaleId },
            DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, "tester");

        // Filter to males only
        CqmReportState filtered = await reportGrain.GetFilteredReportAsync(new CqmFilterCriteria { Sex = "M" });
        Assert.That(filtered.PatientResults, Has.Count.EqualTo(1));
        Assert.That(filtered.PatientResults[0].Sex, Is.EqualTo("M"));
    }

    [Test]
    public async Task CqmReport_FilterByAgeRange()
    {
        string measureId = $"CMS-AGEFILT-{Guid.NewGuid():N}";
        ICqmMeasureGrain measureGrain = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");
        await measureGrain.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureId,
            Title = "Age Filter Range Test",
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

        string youngId = $"PATIENT-CQM-Y-{Guid.NewGuid():N}";
        string oldId = $"PATIENT-CQM-O-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wY = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(youngId);
        IPatientWorkflowGrain wO = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(oldId);
        await wY.UpdateDemographicsAsync("CQM,YOUNG", "M", DateTime.UtcNow.AddYears(-25), "000-00-0004");
        await wO.UpdateDemographicsAsync("CQM,OLD", "M", DateTime.UtcNow.AddYears(-70), "000-00-0005");

        string reportId = $"CQM-REPORT:{Guid.NewGuid():N}";
        ICqmReportGrain reportGrain = _cluster.GrainFactory.GetGrain<ICqmReportGrain>(reportId);
        await reportGrain.EvaluateAsync(measureId, new List<string> { youngId, oldId },
            DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, "tester");

        CqmReportState filtered = await reportGrain.GetFilteredReportAsync(new CqmFilterCriteria { MinAge = 60, MaxAge = 80 });
        Assert.That(filtered.PatientResults, Has.Count.EqualTo(1));
        Assert.That(filtered.PatientResults[0].Age, Is.EqualTo(70));
    }

    // ─── QRDA Export ─────────────────────────────────────────────────────────

    [Test]
    public async Task CqmReport_QrdaCategoryI_ContainsPatientData()
    {
        string measureId = $"CMS-QRDA1-{Guid.NewGuid():N}";
        ICqmMeasureGrain measureGrain = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");
        await measureGrain.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureId,
            Title = "QRDA I Test",
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

        string patientId = $"PATIENT-CQM-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("QRDA,TEST", "M", DateTime.UtcNow.AddYears(-55), "000-00-0006");

        string reportId = $"CQM-REPORT:{Guid.NewGuid():N}";
        ICqmReportGrain reportGrain = _cluster.GrainFactory.GetGrain<ICqmReportGrain>(reportId);
        await reportGrain.EvaluateAsync(measureId, new List<string> { patientId },
            DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, "tester");

        string xml = await reportGrain.ExportQrdaCategoryIAsync(patientId);
        Assert.That(xml, Does.Contain("ClinicalDocument"));
        Assert.That(xml, Does.Contain("2.16.840.1.113883.10.20.24.1.1")); // QRDA I template
        Assert.That(xml, Does.Contain(patientId));
        Assert.That(xml, Does.Contain("QRDA"));
        Assert.That(xml, Does.Contain("IPP")); // Population entries
        Assert.That(xml, Does.Contain("NUMER"));
    }

    [Test]
    public async Task CqmReport_QrdaCategoryI_MissingPatient_Throws()
    {
        string measureId = $"CMS-QRDA1ERR-{Guid.NewGuid():N}";
        ICqmMeasureGrain measureGrain = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");
        await measureGrain.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureId,
            Title = "QRDA I Error Test",
            InitialPopulation = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "greater-than",
                    ComparisonValue = "0" }
            }
        });

        string reportId = $"CQM-REPORT:{Guid.NewGuid():N}";
        ICqmReportGrain reportGrain = _cluster.GrainFactory.GetGrain<ICqmReportGrain>(reportId);

        string patientId = $"PATIENT-CQM-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("QRDA,ERR", "F", DateTime.UtcNow.AddYears(-30), "000-00-0007");

        await reportGrain.EvaluateAsync(measureId, new List<string> { patientId },
            DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, "tester");

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await reportGrain.ExportQrdaCategoryIAsync("NONEXISTENT-PATIENT"));
    }

    [Test]
    public async Task CqmReport_QrdaCategoryIII_ContainsAggregates()
    {
        string measureId = $"CMS-QRDA3-{Guid.NewGuid():N}";
        ICqmMeasureGrain measureGrain = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");
        await measureGrain.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureId,
            Title = "QRDA III Test",
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

        // Create 3 patients
        List<string> patientIds = new();
        for (int i = 0; i < 3; i++)
        {
            string pid = $"PATIENT-CQM-{Guid.NewGuid():N}";
            IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid);
            await w.UpdateDemographicsAsync($"QRDA3,PATIENT {i}", "M", DateTime.UtcNow.AddYears(-(30 + i * 10)), $"000-00-100{i}");
            patientIds.Add(pid);
        }

        string reportId = $"CQM-REPORT:{Guid.NewGuid():N}";
        ICqmReportGrain reportGrain = _cluster.GrainFactory.GetGrain<ICqmReportGrain>(reportId);
        await reportGrain.EvaluateAsync(measureId, patientIds,
            DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, "tester");

        string xml = await reportGrain.ExportQrdaCategoryIIIAsync();
        Assert.That(xml, Does.Contain("ClinicalDocument"));
        Assert.That(xml, Does.Contain("2.16.840.1.113883.10.20.27.1.1")); // QRDA III template
        Assert.That(xml, Does.Contain(measureId));
        Assert.That(xml, Does.Contain("Performance Rate"));
        Assert.That(xml, Does.Contain("IPP"));
        Assert.That(xml, Does.Contain("DENOM"));
        Assert.That(xml, Does.Contain("NUMER"));
    }

    [Test]
    public async Task CqmReport_FilteredQrdaCategoryIII_GeneratesXml()
    {
        string measureId = $"CMS-FQRDA3-{Guid.NewGuid():N}";
        ICqmMeasureGrain measureGrain = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");
        await measureGrain.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureId,
            Title = "Filtered QRDA III Test",
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

        string pid1 = $"PATIENT-CQM-{Guid.NewGuid():N}";
        string pid2 = $"PATIENT-CQM-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w1 = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid1);
        IPatientWorkflowGrain w2 = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid2);
        await w1.UpdateDemographicsAsync("FQRDA,MALE", "M", DateTime.UtcNow.AddYears(-40), "000-00-0010");
        await w2.UpdateDemographicsAsync("FQRDA,FEMALE", "F", DateTime.UtcNow.AddYears(-50), "000-00-0011");

        string reportId = $"CQM-REPORT:{Guid.NewGuid():N}";
        ICqmReportGrain reportGrain = _cluster.GrainFactory.GetGrain<ICqmReportGrain>(reportId);
        await reportGrain.EvaluateAsync(measureId, new List<string> { pid1, pid2 },
            DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, "tester");

        string xml = await reportGrain.ExportFilteredQrdaCategoryIIIAsync(new CqmFilterCriteria { Sex = "F" });
        Assert.That(xml, Does.Contain("ClinicalDocument"));
        Assert.That(xml, Does.Contain("2.16.840.1.113883.10.20.27.1.1"));
    }

    // ─── Performance Rate Calculation ────────────────────────────────────────

    [Test]
    public async Task CqmReport_PerformanceRate_CalculatedCorrectly()
    {
        string measureId = $"CMS-PERF-{Guid.NewGuid():N}";
        ICqmMeasureGrain measureGrain = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");
        await measureGrain.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureId,
            Title = "Performance Rate Test",
            InitialPopulation = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "greater-than",
                    ComparisonValue = "0" }
            },
            // Numerator: age > 40 (only some patients will qualify)
            Numerator = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "greater-than",
                    ComparisonValue = "40" }
            }
        });

        // 2 patients: one age 50 (in numerator), one age 30 (not in numerator)
        string pid1 = $"PATIENT-CQM-{Guid.NewGuid():N}";
        string pid2 = $"PATIENT-CQM-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w1 = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid1);
        IPatientWorkflowGrain w2 = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid2);
        await w1.UpdateDemographicsAsync("PERF,OLD", "M", DateTime.UtcNow.AddYears(-50), "000-00-0020");
        await w2.UpdateDemographicsAsync("PERF,YOUNG", "M", DateTime.UtcNow.AddYears(-30), "000-00-0021");

        string reportId = $"CQM-REPORT:{Guid.NewGuid():N}";
        ICqmReportGrain reportGrain = _cluster.GrainFactory.GetGrain<ICqmReportGrain>(reportId);
        await reportGrain.EvaluateAsync(measureId, new List<string> { pid1, pid2 },
            DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, "tester");

        CqmReportState report = await reportGrain.GetReportAsync();
        Assert.That(report.InitialPopulationCount, Is.EqualTo(2));
        Assert.That(report.DenominatorCount, Is.EqualTo(2));
        Assert.That(report.NumeratorCount, Is.EqualTo(1));
        Assert.That(report.PerformanceRate, Is.EqualTo(50.0));
    }

    // ─── Report State ────────────────────────────────────────────────────────

    [Test]
    public async Task CqmReport_DefaultState_IsPending()
    {
        string reportId = $"CQM-REPORT:{Guid.NewGuid():N}";
        ICqmReportGrain reportGrain = _cluster.GrainFactory.GetGrain<ICqmReportGrain>(reportId);

        CqmReportState report = await reportGrain.GetReportAsync();
        Assert.That(report.Status, Is.EqualTo("pending"));
    }

    [Test]
    public async Task CqmReport_EvaluatedDate_IsSet()
    {
        string measureId = $"CMS-DATE-{Guid.NewGuid():N}";
        ICqmMeasureGrain measureGrain = _cluster.GrainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");
        await measureGrain.SaveMeasureAsync(new CqmMeasureState
        {
            MeasureId = measureId,
            Title = "Date Test",
            InitialPopulation = new List<CqmCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age", Operator = "greater-than",
                    ComparisonValue = "0" }
            }
        });

        string reportId = $"CQM-REPORT:{Guid.NewGuid():N}";
        ICqmReportGrain reportGrain = _cluster.GrainFactory.GetGrain<ICqmReportGrain>(reportId);
        DateTime before = DateTime.UtcNow;

        string pid = $"PATIENT-CQM-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid);
        await w.UpdateDemographicsAsync("DATE,TEST", "F", DateTime.UtcNow.AddYears(-35), "000-00-0030");

        await reportGrain.EvaluateAsync(measureId, new List<string> { pid },
            DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, "tester");

        CqmReportState report = await reportGrain.GetReportAsync();
        Assert.That(report.EvaluatedDate, Is.Not.Null);
        Assert.That(report.EvaluatedDate, Is.GreaterThanOrEqualTo(before));
        Assert.That(report.EvaluatedBy, Is.EqualTo("tester"));
    }
}
