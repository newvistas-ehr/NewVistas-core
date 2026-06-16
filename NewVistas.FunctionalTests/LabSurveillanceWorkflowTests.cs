// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Lab Surveillance enhancements to the eCR module.
/// Tests end-to-end workflows for lab-specific trigger fields and
/// Lab Surveillance Taxonomy grain operations.
/// </summary>
[TestFixture]
public class LabSurveillanceWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Taxonomy Workflows ──────────────────────────────────────────────────

    [Test]
    public async Task CreateTaxonomy_AppearsInIndex()
    {
        string taxId = $"LAB-SURV-TAX:{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyGrain grain = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(taxId);

        await grain.SaveAsync("Chlamydia Tests", "Chlamydia", "240589008", "communicable",
            new List<string> { "US" }, "24 hours", true);

        string indexKey = $"LAB-SURV-TAX-IDX-{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyIndexGrain index = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyIndexGrain>(indexKey);
        await index.UpsertAsync(new LabSurveillanceTaxonomyIndexEntry
        {
            TaxonomyId = taxId,
            TaxonomyName = "Chlamydia Tests",
            ConditionName = "Chlamydia",
            Category = "communicable",
            CodeCount = 0,
            IsActive = true
        });

        List<LabSurveillanceTaxonomyIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].TaxonomyName, Is.EqualTo("Chlamydia Tests"));
        Assert.That(all[0].IsActive, Is.True);
    }

    [Test]
    public async Task CreateTaxonomy_AddMultipleCodes_AllPersisted()
    {
        string taxId = $"LAB-SURV-TAX:{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyGrain grain = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(taxId);

        await grain.SaveAsync("CT LOINC Panel", "Chlamydia", null, "communicable", null, "24 hours", true);

        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "21613-5", CodeSystem = "LOINC", Description = "CT DNA [Presence] in Specimen by NAA"
        });
        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "6357-8", CodeSystem = "LOINC", Description = "CT Ag [Presence] in Specimen by IF"
        });
        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "14463-4", CodeSystem = "LOINC", Description = "CT DNA [Presence] in Urine by NAA"
        });
        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "43304-5", CodeSystem = "LOINC", Description = "CT rRNA [Presence] in Specimen by Probe"
        });

        LabSurveillanceTaxonomyState result = await grain.GetAsync();
        Assert.That(result.Codes, Has.Count.EqualTo(4));
        Assert.That(result.Codes.All(c => c.CodeSystem == "LOINC"), Is.True);
    }

    [Test]
    public async Task CreateTaxonomy_WithLabSpecificFields()
    {
        string taxId = $"LAB-SURV-TAX:{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyGrain grain = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(taxId);

        await grain.SaveAsync("Lead Screening", "Lead Poisoning", null, "environmental",
            new List<string> { "US" }, "5 days", true);

        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "5671-3",
            CodeSystem = "LOINC",
            Description = "Lead [Mass/volume] in Blood",
            SpecimenType = "Blood",
            ValueOperator = "greater-equal",
            ThresholdValue = "5",
            ResultInterpretation = "ABNORMAL"
        });

        LabSurveillanceTaxonomyState result = await grain.GetAsync();
        LabSurveillanceTaxonomyCode code = result.Codes[0];
        Assert.That(code.SpecimenType, Is.EqualTo("Blood"));
        Assert.That(code.ValueOperator, Is.EqualTo("greater-equal"));
        Assert.That(code.ThresholdValue, Is.EqualTo("5"));
        Assert.That(code.ResultInterpretation, Is.EqualTo("ABNORMAL"));
    }

    [Test]
    public async Task TaxonomyActivation_ToggleOnOff()
    {
        string taxId = $"LAB-SURV-TAX:{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyGrain grain = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(taxId);

        await grain.SaveAsync("Toggle Taxonomy", "Test Condition", null, "communicable", null, "24 hours", true);

        string indexKey = $"LAB-SURV-TAX-IDX-{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyIndexGrain index = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyIndexGrain>(indexKey);
        await index.UpsertAsync(new LabSurveillanceTaxonomyIndexEntry
        {
            TaxonomyId = taxId, TaxonomyName = "Toggle Taxonomy", ConditionName = "Test Condition",
            Category = "communicable", CodeCount = 0, IsActive = true
        });

        // Deactivate
        await grain.SetActiveAsync(false);
        LabSurveillanceTaxonomyState deactivated = await grain.GetAsync();
        Assert.That(deactivated.IsActive, Is.False);

        await index.UpsertAsync(new LabSurveillanceTaxonomyIndexEntry
        {
            TaxonomyId = taxId, TaxonomyName = "Toggle Taxonomy", ConditionName = "Test Condition",
            Category = "communicable", CodeCount = 0, IsActive = false
        });
        List<LabSurveillanceTaxonomyIndexEntry> active = await index.GetActiveAsync();
        Assert.That(active.Any(e => e.TaxonomyId == taxId), Is.False);

        // Reactivate
        await grain.SetActiveAsync(true);
        LabSurveillanceTaxonomyState reactivated = await grain.GetAsync();
        Assert.That(reactivated.IsActive, Is.True);

        await index.UpsertAsync(new LabSurveillanceTaxonomyIndexEntry
        {
            TaxonomyId = taxId, TaxonomyName = "Toggle Taxonomy", ConditionName = "Test Condition",
            Category = "communicable", CodeCount = 0, IsActive = true
        });
        active = await index.GetActiveAsync();
        Assert.That(active.Any(e => e.TaxonomyId == taxId), Is.True);
    }

    [Test]
    public async Task RemoveCode_UpdatesTaxonomy()
    {
        string taxId = $"LAB-SURV-TAX:{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyGrain grain = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(taxId);

        await grain.SaveAsync("Remove Code Test", "Test", null, "communicable", null, "24 hours", true);

        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "21613-5", CodeSystem = "LOINC", Description = "Code A"
        });
        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "6357-8", CodeSystem = "LOINC", Description = "Code B"
        });
        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "14463-4", CodeSystem = "LOINC", Description = "Code C"
        });

        await grain.RemoveCodeAsync("6357-8", "LOINC");

        LabSurveillanceTaxonomyState result = await grain.GetAsync();
        Assert.That(result.Codes, Has.Count.EqualTo(2));
        Assert.That(result.Codes.Any(c => c.Code == "6357-8"), Is.False);
        Assert.That(result.Codes.Any(c => c.Code == "21613-5"), Is.True);
        Assert.That(result.Codes.Any(c => c.Code == "14463-4"), Is.True);
    }

    [Test]
    public async Task MultipleTaxonomies_DifferentConditions()
    {
        string indexKey = $"LAB-SURV-TAX-IDX-{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyIndexGrain index = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyIndexGrain>(indexKey);

        // Create chlamydia taxonomy
        string chlamTaxId = $"LAB-SURV-TAX:{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyGrain chlamGrain = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(chlamTaxId);
        await chlamGrain.SaveAsync("Chlamydia Tests", "Chlamydia", null, "communicable", null, "24 hours", true);
        await chlamGrain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "21613-5", CodeSystem = "LOINC", Description = "CT DNA probe"
        });

        await index.UpsertAsync(new LabSurveillanceTaxonomyIndexEntry
        {
            TaxonomyId = chlamTaxId, TaxonomyName = "Chlamydia Tests", ConditionName = "Chlamydia",
            Category = "communicable", CodeCount = 1, IsActive = true
        });

        // Create TB taxonomy
        string tbTaxId = $"LAB-SURV-TAX:{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyGrain tbGrain = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(tbTaxId);
        await tbGrain.SaveAsync("TB Culture", "Tuberculosis", null, "communicable", null, "24 hours", true);
        await tbGrain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "543-9", CodeSystem = "LOINC", Description = "AFB culture"
        });

        await index.UpsertAsync(new LabSurveillanceTaxonomyIndexEntry
        {
            TaxonomyId = tbTaxId, TaxonomyName = "TB Culture", ConditionName = "Tuberculosis",
            Category = "communicable", CodeCount = 1, IsActive = true
        });

        // Verify independent in index
        List<LabSurveillanceTaxonomyIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(2));
        Assert.That(all.Any(e => e.ConditionName == "Chlamydia"), Is.True);
        Assert.That(all.Any(e => e.ConditionName == "Tuberculosis"), Is.True);

        // Verify independent grain data
        LabSurveillanceTaxonomyState chlamResult = await chlamGrain.GetAsync();
        LabSurveillanceTaxonomyState tbResult = await tbGrain.GetAsync();
        Assert.That(chlamResult.TaxonomyName, Is.EqualTo("Chlamydia Tests"));
        Assert.That(tbResult.TaxonomyName, Is.EqualTo("TB Culture"));
    }

    [Test]
    public async Task ActiveFilter_ReturnsOnlyActive()
    {
        string indexKey = $"LAB-SURV-TAX-IDX-{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyIndexGrain index = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyIndexGrain>(indexKey);

        await index.UpsertAsync(new LabSurveillanceTaxonomyIndexEntry
        {
            TaxonomyId = "ACTIVE-1", TaxonomyName = "Active Taxonomy 1", ConditionName = "Cond A",
            Category = "communicable", CodeCount = 2, IsActive = true
        });
        await index.UpsertAsync(new LabSurveillanceTaxonomyIndexEntry
        {
            TaxonomyId = "ACTIVE-2", TaxonomyName = "Active Taxonomy 2", ConditionName = "Cond B",
            Category = "communicable", CodeCount = 3, IsActive = true
        });
        await index.UpsertAsync(new LabSurveillanceTaxonomyIndexEntry
        {
            TaxonomyId = "INACTIVE-1", TaxonomyName = "Inactive Taxonomy", ConditionName = "Cond C",
            Category = "communicable", CodeCount = 1, IsActive = false
        });

        List<LabSurveillanceTaxonomyIndexEntry> active = await index.GetActiveAsync();
        Assert.That(active, Has.Count.EqualTo(2));
        Assert.That(active.All(e => e.IsActive), Is.True);
    }

    // ─── Trigger Lab Field Workflows ─────────────────────────────────────────

    [Test]
    public async Task TriggerWithLabFields_QuantitativeThreshold()
    {
        string triggerId = $"TRIGGER-{Guid.NewGuid():N}";
        IEcrTriggerGrain grain = _cluster.GrainFactory.GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:{triggerId}");

        await grain.SaveTriggerAsync(new EcrTriggerState
        {
            TriggerId = triggerId,
            ConditionName = "Hyperglycemia",
            IsActive = true,
            Category = "chronic",
            TriggerCodes = new List<EcrTriggerCode>
            {
                new()
                {
                    Code = "2345-7",
                    CodeSystem = "LOINC",
                    Description = "Glucose [Mass/volume] in Serum or Plasma",
                    TriggerType = "lab-result",
                    ValueOperator = "greater-than",
                    ThresholdValue = "400"
                }
            }
        });

        EcrTriggerState result = await grain.GetTriggerAsync();
        Assert.That(result.ConditionName, Is.EqualTo("Hyperglycemia"));
        EcrTriggerCode code = result.TriggerCodes[0];
        Assert.That(code.ValueOperator, Is.EqualTo("greater-than"));
        Assert.That(code.ThresholdValue, Is.EqualTo("400"));
        Assert.That(code.CodeSystem, Is.EqualTo("LOINC"));
    }

    [Test]
    public async Task TriggerWithLabFields_QualitativeResult()
    {
        string triggerId = $"TRIGGER-{Guid.NewGuid():N}";
        IEcrTriggerGrain grain = _cluster.GrainFactory.GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:{triggerId}");

        await grain.SaveTriggerAsync(new EcrTriggerState
        {
            TriggerId = triggerId,
            ConditionName = "HIV Infection",
            IsActive = true,
            Category = "communicable",
            TriggerCodes = new List<EcrTriggerCode>
            {
                new()
                {
                    Code = "75607-0",
                    CodeSystem = "LOINC",
                    Description = "HIV 1+2 Ab [Presence] in Serum",
                    TriggerType = "lab-result",
                    ResultInterpretation = "POSITIVE"
                }
            }
        });

        EcrTriggerState result = await grain.GetTriggerAsync();
        EcrTriggerCode code = result.TriggerCodes[0];
        Assert.That(code.ResultInterpretation, Is.EqualTo("POSITIVE"));
        Assert.That(code.TriggerType, Is.EqualTo("lab-result"));
    }

    [Test]
    public async Task TriggerWithLabFields_SpecimenType()
    {
        string triggerId = $"TRIGGER-{Guid.NewGuid():N}";
        IEcrTriggerGrain grain = _cluster.GrainFactory.GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:{triggerId}");

        await grain.SaveTriggerAsync(new EcrTriggerState
        {
            TriggerId = triggerId,
            ConditionName = "Bloodstream Infection",
            IsActive = true,
            Category = "communicable",
            TriggerCodes = new List<EcrTriggerCode>
            {
                new()
                {
                    Code = "600-7",
                    CodeSystem = "LOINC",
                    Description = "Bacteria identified in Blood by Culture",
                    TriggerType = "lab-result",
                    SpecimenType = "Blood"
                }
            }
        });

        EcrTriggerState result = await grain.GetTriggerAsync();
        Assert.That(result.TriggerCodes[0].SpecimenType, Is.EqualTo("Blood"));
    }

    [Test]
    public async Task TriggerWithLabFields_BackwardCompatible()
    {
        string triggerId = $"TRIGGER-{Guid.NewGuid():N}";
        IEcrTriggerGrain grain = _cluster.GrainFactory.GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:{triggerId}");

        await grain.SaveTriggerAsync(new EcrTriggerState
        {
            TriggerId = triggerId,
            ConditionName = "Measles",
            IsActive = true,
            Category = "communicable",
            TriggerCodes = new List<EcrTriggerCode>
            {
                new()
                {
                    Code = "B05.*",
                    CodeSystem = "ICD-10",
                    Description = "Measles",
                    TriggerType = "diagnosis"
                }
            }
        });

        EcrTriggerState result = await grain.GetTriggerAsync();
        EcrTriggerCode code = result.TriggerCodes[0];
        Assert.That(code.SpecimenType, Is.Null);
        Assert.That(code.ValueOperator, Is.Null);
        Assert.That(code.ThresholdValue, Is.Null);
        Assert.That(code.ResultInterpretation, Is.Null);
    }

    [Test]
    public async Task TaxonomyCodeWithThresholdAndSpecimen_FullLabProfile()
    {
        string taxId = $"LAB-SURV-TAX:{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyGrain grain = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(taxId);

        await grain.SaveAsync("Full Lab Profile", "Lead Poisoning", null, "environmental",
            new List<string> { "US" }, "5 days", true);

        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "5671-3",
            CodeSystem = "LOINC",
            Description = "Lead [Mass/volume] in Blood",
            SpecimenType = "Blood",
            ValueOperator = "greater-equal",
            ThresholdValue = "5",
            ResultInterpretation = "ABNORMAL"
        });

        LabSurveillanceTaxonomyState result = await grain.GetAsync();
        LabSurveillanceTaxonomyCode code = result.Codes[0];
        Assert.That(code.SpecimenType, Is.EqualTo("Blood"));
        Assert.That(code.ValueOperator, Is.EqualTo("greater-equal"));
        Assert.That(code.ThresholdValue, Is.EqualTo("5"));
        Assert.That(code.ResultInterpretation, Is.EqualTo("ABNORMAL"));
    }

    // ─── Full Condition Taxonomy Setups ──────────────────────────────────────

    [Test]
    public async Task ChlamydiaScreening_FullTaxonomySetup()
    {
        string taxId = $"LAB-SURV-TAX:{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyGrain grain = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(taxId);

        await grain.SaveAsync(
            "Chlamydia Screening Panel",
            "Chlamydia trachomatis infection",
            "240589008",
            "communicable",
            new List<string> { "US", "VA" },
            "24 hours",
            true);

        // CT DNA probe (NAA)
        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "21613-5",
            CodeSystem = "LOINC",
            Description = "CT DNA [Presence] in Specimen by NAA",
            SpecimenType = "Urine",
            ValueOperator = "positive",
            ResultInterpretation = "DETECTED"
        });

        // CT culture
        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "6357-8",
            CodeSystem = "LOINC",
            Description = "CT [Presence] in Specimen by Culture",
            SpecimenType = "Cervical swab",
            ValueOperator = "positive",
            ResultInterpretation = "DETECTED"
        });

        // CT antigen
        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "14463-4",
            CodeSystem = "LOINC",
            Description = "CT Ag [Presence] in Specimen by IF",
            SpecimenType = "Urethral swab",
            ValueOperator = "positive",
            ResultInterpretation = "DETECTED"
        });

        LabSurveillanceTaxonomyState result = await grain.GetAsync();
        Assert.That(result.TaxonomyName, Is.EqualTo("Chlamydia Screening Panel"));
        Assert.That(result.ConditionName, Is.EqualTo("Chlamydia trachomatis infection"));
        Assert.That(result.ConditionCode, Is.EqualTo("240589008"));
        Assert.That(result.Category, Is.EqualTo("communicable"));
        Assert.That(result.Jurisdictions, Has.Count.EqualTo(2));
        Assert.That(result.Codes, Has.Count.EqualTo(3));
        Assert.That(result.Codes.All(c => c.ResultInterpretation == "DETECTED"), Is.True);
        Assert.That(result.Codes.All(c => c.ValueOperator == "positive"), Is.True);
        Assert.That(result.IsActive, Is.True);
    }

    [Test]
    public async Task TBSurveillance_FullTaxonomySetup()
    {
        string taxId = $"LAB-SURV-TAX:{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyGrain grain = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(taxId);

        await grain.SaveAsync(
            "TB Surveillance Panel",
            "Tuberculosis",
            "56717001",
            "communicable",
            new List<string> { "US" },
            "24 hours",
            true);

        // AFB culture
        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "543-9",
            CodeSystem = "LOINC",
            Description = "Mycobacterium sp identified in Specimen by Culture",
            SpecimenType = "Sputum",
            ValueOperator = "positive",
            ResultInterpretation = "POSITIVE"
        });

        // TB PCR
        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "38379-4",
            CodeSystem = "LOINC",
            Description = "Mycobacterium tuberculosis complex DNA [Presence] in Specimen by NAA",
            SpecimenType = "Sputum",
            ValueOperator = "positive",
            ResultInterpretation = "POSITIVE"
        });

        LabSurveillanceTaxonomyState result = await grain.GetAsync();
        Assert.That(result.TaxonomyName, Is.EqualTo("TB Surveillance Panel"));
        Assert.That(result.ConditionName, Is.EqualTo("Tuberculosis"));
        Assert.That(result.ConditionCode, Is.EqualTo("56717001"));
        Assert.That(result.Category, Is.EqualTo("communicable"));
        Assert.That(result.Codes, Has.Count.EqualTo(2));
        Assert.That(result.Codes.All(c => c.ResultInterpretation == "POSITIVE"), Is.True);
        Assert.That(result.Codes.All(c => c.SpecimenType == "Sputum"), Is.True);
        Assert.That(result.IsActive, Is.True);
    }
}
