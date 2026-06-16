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
/// Unit tests for Lab Surveillance enhancements to the eCR module.
/// Tests lab-specific trigger code fields and Lab Surveillance Taxonomy grains.
/// </summary>
[TestFixture]
public class LabSurveillanceTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Extended TriggerCode Fields ─────────────────────────────────────────

    [Test]
    public async Task TriggerCode_LabSpecificFields_Persist()
    {
        string triggerId = $"TRIGGER-{Guid.NewGuid():N}";
        IEcrTriggerGrain grain = _cluster.GrainFactory.GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:{triggerId}");

        await grain.SaveTriggerAsync(new EcrTriggerState
        {
            TriggerId = triggerId,
            ConditionName = "HIV Screening",
            ConditionCode = "19030005",
            ConditionCodeSystem = "SNOMED",
            Category = "communicable",
            IsActive = true,
            TriggerCodes = new List<EcrTriggerCode>
            {
                new()
                {
                    Code = "75607-0",
                    CodeSystem = "LOINC",
                    Description = "HIV 1+2 Ab [Presence] in Serum",
                    TriggerType = "lab-result",
                    SpecimenType = "Blood",
                    ValueOperator = "greater-than",
                    ThresholdValue = "1.0",
                    ResultInterpretation = "REACTIVE"
                }
            }
        });

        EcrTriggerState result = await grain.GetTriggerAsync();
        Assert.That(result.ConditionName, Is.EqualTo("HIV Screening"));
        Assert.That(result.TriggerCodes, Has.Count.EqualTo(1));

        EcrTriggerCode code = result.TriggerCodes[0];
        Assert.That(code.SpecimenType, Is.EqualTo("Blood"));
        Assert.That(code.ValueOperator, Is.EqualTo("greater-than"));
        Assert.That(code.ThresholdValue, Is.EqualTo("1.0"));
        Assert.That(code.ResultInterpretation, Is.EqualTo("REACTIVE"));
    }

    [Test]
    public async Task TriggerCode_QualitativeMatch_PositiveResult()
    {
        string triggerId = $"TRIGGER-{Guid.NewGuid():N}";
        IEcrTriggerGrain grain = _cluster.GrainFactory.GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:{triggerId}");

        await grain.SaveTriggerAsync(new EcrTriggerState
        {
            TriggerId = triggerId,
            ConditionName = "Chlamydia",
            IsActive = true,
            TriggerCodes = new List<EcrTriggerCode>
            {
                new()
                {
                    Code = "75607-0",
                    CodeSystem = "LOINC",
                    Description = "Chlamydia trachomatis DNA [Presence]",
                    TriggerType = "lab-result",
                    ResultInterpretation = "POSITIVE"
                }
            }
        });

        EcrTriggerState result = await grain.GetTriggerAsync();
        EcrTriggerCode code = result.TriggerCodes[0];
        Assert.That(code.CodeSystem, Is.EqualTo("LOINC"));
        Assert.That(code.Code, Is.EqualTo("75607-0"));
        Assert.That(code.TriggerType, Is.EqualTo("lab-result"));
        Assert.That(code.ResultInterpretation, Is.EqualTo("POSITIVE"));
    }

    [Test]
    public async Task TriggerCode_QuantitativeMatch_Threshold()
    {
        string triggerId = $"TRIGGER-{Guid.NewGuid():N}";
        IEcrTriggerGrain grain = _cluster.GrainFactory.GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:{triggerId}");

        await grain.SaveTriggerAsync(new EcrTriggerState
        {
            TriggerId = triggerId,
            ConditionName = "Diabetes Screening",
            IsActive = true,
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
        EcrTriggerCode code = result.TriggerCodes[0];
        Assert.That(code.ValueOperator, Is.EqualTo("greater-than"));
        Assert.That(code.ThresholdValue, Is.EqualTo("400"));
    }

    [Test]
    public async Task TriggerCode_NullLabFields_DefaultBehavior()
    {
        string triggerId = $"TRIGGER-{Guid.NewGuid():N}";
        IEcrTriggerGrain grain = _cluster.GrainFactory.GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:{triggerId}");

        await grain.SaveTriggerAsync(new EcrTriggerState
        {
            TriggerId = triggerId,
            ConditionName = "Measles",
            IsActive = true,
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

    // ─── Taxonomy Grain ──────────────────────────────────────────────────────

    [Test]
    public async Task TaxonomyGrain_Save_PersistsAllFields()
    {
        string taxId = $"LAB-SURV-TAX:{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyGrain grain = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(taxId);

        await grain.SaveAsync(
            "Chlamydia Tests",
            "Chlamydia trachomatis infection",
            "240589008",
            "communicable",
            new List<string> { "US", "VA" },
            "24 hours",
            true);

        LabSurveillanceTaxonomyState result = await grain.GetAsync();
        Assert.That(result.TaxonomyName, Is.EqualTo("Chlamydia Tests"));
        Assert.That(result.ConditionName, Is.EqualTo("Chlamydia trachomatis infection"));
        Assert.That(result.ConditionCode, Is.EqualTo("240589008"));
        Assert.That(result.Category, Is.EqualTo("communicable"));
        Assert.That(result.Jurisdictions, Has.Count.EqualTo(2));
        Assert.That(result.Jurisdictions, Contains.Item("US"));
        Assert.That(result.Jurisdictions, Contains.Item("VA"));
        Assert.That(result.ReportingTimeframe, Is.EqualTo("24 hours"));
        Assert.That(result.IsActive, Is.True);
    }

    [Test]
    public async Task TaxonomyGrain_AddCode_AppendsList()
    {
        string taxId = $"LAB-SURV-TAX:{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyGrain grain = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(taxId);

        await grain.SaveAsync("CT Tests", "Chlamydia", null, "communicable", null, "24 hours", true);

        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "21613-5", CodeSystem = "LOINC", Description = "CT DNA probe",
            SpecimenType = "Urine"
        });
        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "6357-8", CodeSystem = "LOINC", Description = "CT culture",
            SpecimenType = "Cervical swab"
        });
        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "14463-4", CodeSystem = "LOINC", Description = "CT antigen",
            SpecimenType = "Urine"
        });

        LabSurveillanceTaxonomyState result = await grain.GetAsync();
        Assert.That(result.Codes, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task TaxonomyGrain_AddCode_NoDuplicates()
    {
        string taxId = $"LAB-SURV-TAX:{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyGrain grain = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(taxId);

        await grain.SaveAsync("Dup Test", "Test", null, "communicable", null, "24 hours", true);

        LabSurveillanceTaxonomyCode code = new()
        {
            Code = "21613-5", CodeSystem = "LOINC", Description = "CT DNA probe"
        };

        await grain.AddCodeAsync(code);
        await grain.AddCodeAsync(code);

        LabSurveillanceTaxonomyState result = await grain.GetAsync();
        Assert.That(result.Codes, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task TaxonomyGrain_RemoveCode_RemovesByCodeAndSystem()
    {
        string taxId = $"LAB-SURV-TAX:{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyGrain grain = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(taxId);

        await grain.SaveAsync("Remove Test", "Test", null, "communicable", null, "24 hours", true);

        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "21613-5", CodeSystem = "LOINC", Description = "Code A"
        });
        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "6357-8", CodeSystem = "LOINC", Description = "Code B"
        });

        await grain.RemoveCodeAsync("21613-5", "LOINC");

        LabSurveillanceTaxonomyState result = await grain.GetAsync();
        Assert.That(result.Codes, Has.Count.EqualTo(1));
        Assert.That(result.Codes[0].Code, Is.EqualTo("6357-8"));
    }

    [Test]
    public async Task TaxonomyGrain_SetActive_Toggles()
    {
        string taxId = $"LAB-SURV-TAX:{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyGrain grain = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(taxId);

        await grain.SaveAsync("Toggle Test", "Test", null, "communicable", null, "24 hours", true);

        await grain.SetActiveAsync(false);
        Assert.That((await grain.GetAsync()).IsActive, Is.False);

        await grain.SetActiveAsync(true);
        Assert.That((await grain.GetAsync()).IsActive, Is.True);
    }

    [Test]
    public async Task TaxonomyGrain_CodeWithThreshold_PersistsValueFields()
    {
        string taxId = $"LAB-SURV-TAX:{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyGrain grain = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(taxId);

        await grain.SaveAsync("Threshold Test", "Test", null, "communicable", null, "24 hours", true);

        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "21613-5",
            CodeSystem = "LOINC",
            Description = "CT DNA probe",
            ValueOperator = "positive",
            ResultInterpretation = "DETECTED"
        });

        LabSurveillanceTaxonomyState result = await grain.GetAsync();
        LabSurveillanceTaxonomyCode code = result.Codes[0];
        Assert.That(code.ValueOperator, Is.EqualTo("positive"));
        Assert.That(code.ResultInterpretation, Is.EqualTo("DETECTED"));
    }

    [Test]
    public async Task TaxonomyGrain_CodeWithSpecimen_PersistsSpecimenType()
    {
        string taxId = $"LAB-SURV-TAX:{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyGrain grain = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(taxId);

        await grain.SaveAsync("Specimen Test", "Test", null, "communicable", null, "24 hours", true);

        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "5671-3",
            CodeSystem = "LOINC",
            Description = "Lead [Mass/volume] in Blood",
            SpecimenType = "Serum"
        });

        LabSurveillanceTaxonomyState result = await grain.GetAsync();
        Assert.That(result.Codes[0].SpecimenType, Is.EqualTo("Serum"));
    }

    // ─── Taxonomy Index Grain ────────────────────────────────────────────────

    [Test]
    public async Task TaxonomyIndexGrain_Upsert_AddsNew()
    {
        string indexKey = $"LAB-SURV-TAX-IDX-{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyIndexGrain index = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyIndexGrain>(indexKey);

        await index.UpsertAsync(new LabSurveillanceTaxonomyIndexEntry
        {
            TaxonomyId = "TAX-001",
            TaxonomyName = "Chlamydia Tests",
            ConditionName = "Chlamydia",
            Category = "communicable",
            CodeCount = 3,
            IsActive = true
        });

        List<LabSurveillanceTaxonomyIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].TaxonomyName, Is.EqualTo("Chlamydia Tests"));
        Assert.That(all[0].CodeCount, Is.EqualTo(3));
    }

    [Test]
    public async Task TaxonomyIndexGrain_Upsert_UpdatesExisting()
    {
        string indexKey = $"LAB-SURV-TAX-IDX-{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyIndexGrain index = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyIndexGrain>(indexKey);

        await index.UpsertAsync(new LabSurveillanceTaxonomyIndexEntry
        {
            TaxonomyId = "TAX-UPD",
            TaxonomyName = "Original Name",
            ConditionName = "Test",
            Category = "communicable",
            CodeCount = 1,
            IsActive = true
        });

        await index.UpsertAsync(new LabSurveillanceTaxonomyIndexEntry
        {
            TaxonomyId = "TAX-UPD",
            TaxonomyName = "Updated Name",
            ConditionName = "Test",
            Category = "communicable",
            CodeCount = 5,
            IsActive = true
        });

        List<LabSurveillanceTaxonomyIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].TaxonomyName, Is.EqualTo("Updated Name"));
        Assert.That(all[0].CodeCount, Is.EqualTo(5));
    }

    [Test]
    public async Task TaxonomyIndexGrain_GetActive_FiltersCorrectly()
    {
        string indexKey = $"LAB-SURV-TAX-IDX-{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyIndexGrain index = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyIndexGrain>(indexKey);

        await index.UpsertAsync(new LabSurveillanceTaxonomyIndexEntry
        {
            TaxonomyId = "TAX-A1", TaxonomyName = "Active 1", ConditionName = "Cond A",
            Category = "communicable", CodeCount = 2, IsActive = true
        });
        await index.UpsertAsync(new LabSurveillanceTaxonomyIndexEntry
        {
            TaxonomyId = "TAX-A2", TaxonomyName = "Active 2", ConditionName = "Cond B",
            Category = "communicable", CodeCount = 3, IsActive = true
        });
        await index.UpsertAsync(new LabSurveillanceTaxonomyIndexEntry
        {
            TaxonomyId = "TAX-I1", TaxonomyName = "Inactive 1", ConditionName = "Cond C",
            Category = "communicable", CodeCount = 1, IsActive = false
        });

        List<LabSurveillanceTaxonomyIndexEntry> active = await index.GetActiveAsync();
        Assert.That(active, Has.Count.EqualTo(2));
        Assert.That(active.All(e => e.IsActive), Is.True);
    }

    // ─── Combined ────────────────────────────────────────────────────────────

    [Test]
    public async Task TriggerAndTaxonomy_IndependentStorage()
    {
        // Create a trigger for chlamydia
        string triggerId = $"TRIGGER-{Guid.NewGuid():N}";
        IEcrTriggerGrain triggerGrain = _cluster.GrainFactory.GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:{triggerId}");
        await triggerGrain.SaveTriggerAsync(new EcrTriggerState
        {
            TriggerId = triggerId,
            ConditionName = "Chlamydia",
            IsActive = true,
            TriggerCodes = new List<EcrTriggerCode>
            {
                new()
                {
                    Code = "21613-5", CodeSystem = "LOINC", Description = "CT DNA probe",
                    TriggerType = "lab-result", ResultInterpretation = "DETECTED"
                }
            }
        });

        // Create a taxonomy for chlamydia
        string taxId = $"LAB-SURV-TAX:{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyGrain taxGrain = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(taxId);
        await taxGrain.SaveAsync("Chlamydia Tests", "Chlamydia", "240589008", "communicable",
            new List<string> { "US" }, "24 hours", true);
        await taxGrain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "21613-5", CodeSystem = "LOINC", Description = "CT DNA probe"
        });

        // Verify independent retrieval
        EcrTriggerState triggerResult = await triggerGrain.GetTriggerAsync();
        LabSurveillanceTaxonomyState taxResult = await taxGrain.GetAsync();

        Assert.That(triggerResult.ConditionName, Is.EqualTo("Chlamydia"));
        Assert.That(triggerResult.TriggerCodes, Has.Count.EqualTo(1));
        Assert.That(taxResult.TaxonomyName, Is.EqualTo("Chlamydia Tests"));
        Assert.That(taxResult.Codes, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task MultiConditionTaxonomies_IndependentGroups()
    {
        string chlamTaxId = $"LAB-SURV-TAX:{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyGrain chlamGrain = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(chlamTaxId);
        await chlamGrain.SaveAsync("Chlamydia Tests", "Chlamydia", null, "communicable", null, "24 hours", true);
        await chlamGrain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "21613-5", CodeSystem = "LOINC", Description = "CT DNA probe"
        });

        string tbTaxId = $"LAB-SURV-TAX:{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyGrain tbGrain = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(tbTaxId);
        await tbGrain.SaveAsync("TB Culture", "Tuberculosis", null, "communicable", null, "24 hours", true);
        await tbGrain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "543-9", CodeSystem = "LOINC", Description = "AFB culture"
        });
        await tbGrain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "38379-4", CodeSystem = "LOINC", Description = "TB PCR"
        });

        LabSurveillanceTaxonomyState chlamResult = await chlamGrain.GetAsync();
        LabSurveillanceTaxonomyState tbResult = await tbGrain.GetAsync();

        Assert.That(chlamResult.TaxonomyName, Is.EqualTo("Chlamydia Tests"));
        Assert.That(chlamResult.Codes, Has.Count.EqualTo(1));
        Assert.That(tbResult.TaxonomyName, Is.EqualTo("TB Culture"));
        Assert.That(tbResult.Codes, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task TaxonomyGrain_MultipleCodeSystems_MixedLoincAndCpt()
    {
        string taxId = $"LAB-SURV-TAX:{Guid.NewGuid():N}";
        ILabSurveillanceTaxonomyGrain grain = _cluster.GrainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(taxId);

        await grain.SaveAsync("Mixed Code Systems", "Test Condition", null, "communicable", null, "24 hours", true);

        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "21613-5", CodeSystem = "LOINC", Description = "CT DNA probe (LOINC)"
        });
        await grain.AddCodeAsync(new LabSurveillanceTaxonomyCode
        {
            Code = "87491", CodeSystem = "CPT", Description = "Chlamydia detection (CPT)"
        });

        LabSurveillanceTaxonomyState result = await grain.GetAsync();
        Assert.That(result.Codes, Has.Count.EqualTo(2));
        Assert.That(result.Codes.Any(c => c.CodeSystem == "LOINC"), Is.True);
        Assert.That(result.Codes.Any(c => c.CodeSystem == "CPT"), Is.True);
    }

    [Test]
    public void EcrTriggerCode_AllNewFieldIds_Sequential()
    {
        // Verify that the new lab-specific fields on EcrTriggerCode have [Id] values 4,5,6,7
        System.Reflection.PropertyInfo[] props = typeof(EcrTriggerCode).GetProperties();

        System.Reflection.PropertyInfo specimenProp = props.First(p => p.Name == "SpecimenType");
        Orleans.IdAttribute specimenId = specimenProp.GetCustomAttributes(typeof(Orleans.IdAttribute), false)
            .Cast<Orleans.IdAttribute>().First();
        Assert.That(specimenId.Id, Is.EqualTo(4));

        System.Reflection.PropertyInfo operatorProp = props.First(p => p.Name == "ValueOperator");
        Orleans.IdAttribute operatorId = operatorProp.GetCustomAttributes(typeof(Orleans.IdAttribute), false)
            .Cast<Orleans.IdAttribute>().First();
        Assert.That(operatorId.Id, Is.EqualTo(5));

        System.Reflection.PropertyInfo thresholdProp = props.First(p => p.Name == "ThresholdValue");
        Orleans.IdAttribute thresholdId = thresholdProp.GetCustomAttributes(typeof(Orleans.IdAttribute), false)
            .Cast<Orleans.IdAttribute>().First();
        Assert.That(thresholdId.Id, Is.EqualTo(6));

        System.Reflection.PropertyInfo interpProp = props.First(p => p.Name == "ResultInterpretation");
        Orleans.IdAttribute interpId = interpProp.GetCustomAttributes(typeof(Orleans.IdAttribute), false)
            .Cast<Orleans.IdAttribute>().First();
        Assert.That(interpId.Id, Is.EqualTo(7));
    }
}
