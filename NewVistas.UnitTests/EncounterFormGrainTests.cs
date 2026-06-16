// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

[TestFixture]
public class EncounterFormGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup() { _cluster = SharedCluster.Instance; }

    private IEncounterFormTemplateGrain GetTemplate(string id) => _cluster.GrainFactory.GetGrain<IEncounterFormTemplateGrain>(id);
    private IEncounterFormInstanceGrain GetInstance(string id) => _cluster.GrainFactory.GetGrain<IEncounterFormInstanceGrain>(id);
    private IEncounterFormTemplateIndexGrain GetTemplateIndex() => _cluster.GrainFactory.GetGrain<IEncounterFormTemplateIndexGrain>("EF-TPL-IDX");
    private IEncounterFormInstanceIndexGrain GetInstanceIndex() => _cluster.GrainFactory.GetGrain<IEncounterFormInstanceIndexGrain>("EF-INST-IDX");

    private List<EncounterFormFieldDefinition> SampleFields() => new()
    {
        new() { FieldId = "chief_complaint", Label = "Chief Complaint", FieldType = "TEXT", IsRequired = true, DisplayOrder = 1 },
        new() { FieldId = "bp_systolic", Label = "BP Systolic", FieldType = "NUMBER", IsRequired = true, DisplayOrder = 2 },
        new() { FieldId = "bp_diastolic", Label = "BP Diastolic", FieldType = "NUMBER", IsRequired = true, DisplayOrder = 3 },
        new() { FieldId = "assessment", Label = "Assessment", FieldType = "TEXTAREA", IsRequired = false, DisplayOrder = 4 }
    };

    [Test]
    public async Task TemplateGrain_CreatesTemplate()
    {
        string id = $"EF-TPL:{Guid.NewGuid()}";
        var grain = GetTemplate(id);
        var result = await grain.CreateTemplateAsync("Diabetes Visit", "Standard DM encounter form", "ENCOUNTER", null, SampleFields(), "Admin");

        Assert.That(result.TemplateId, Is.EqualTo(id));
        Assert.That(result.Name, Is.EqualTo("Diabetes Visit"));
        Assert.That(result.FormType, Is.EqualTo("ENCOUNTER"));
        Assert.That(result.Status, Is.EqualTo("DRAFT"));
        Assert.That(result.Fields, Has.Count.EqualTo(4));
        Assert.That(result.Version, Is.EqualTo(1));
    }

    [Test]
    public async Task TemplateGrain_PublishesTemplate()
    {
        string id = $"EF-TPL:{Guid.NewGuid()}";
        var grain = GetTemplate(id);
        await grain.CreateTemplateAsync("HTN Visit", "Hypertension form", "ENCOUNTER", null, SampleFields(), "Admin");

        await grain.PublishAsync("Admin");

        var state = await grain.GetTemplateAsync();
        Assert.That(state.Status, Is.EqualTo("PUBLISHED"));
    }

    [Test]
    public async Task TemplateGrain_RetiresTemplate()
    {
        string id = $"EF-TPL:{Guid.NewGuid()}";
        var grain = GetTemplate(id);
        await grain.CreateTemplateAsync("Old Form", "Deprecated", "SCREENING", null, SampleFields(), "Admin");
        await grain.PublishAsync("Admin");

        await grain.RetireAsync("Admin");

        var state = await grain.GetTemplateAsync();
        Assert.That(state.Status, Is.EqualTo("RETIRED"));
    }

    [Test]
    public async Task TemplateGrain_CannotUpdateRetired()
    {
        string id = $"EF-TPL:{Guid.NewGuid()}";
        var grain = GetTemplate(id);
        await grain.CreateTemplateAsync("Retired Form", "Desc", "ENCOUNTER", null, SampleFields(), "Admin");
        await grain.RetireAsync("Admin");

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await grain.UpdateTemplateAsync("New Name", "New Desc", SampleFields(), "Admin"));
    }

    [Test]
    public async Task TemplateGrain_AddRemoveField()
    {
        string id = $"EF-TPL:{Guid.NewGuid()}";
        var grain = GetTemplate(id);
        await grain.CreateTemplateAsync("Flexible Form", "Desc", "CUSTOM", null, new(), "Admin");

        await grain.AddFieldAsync(new() { FieldId = "f1", Label = "Field 1", FieldType = "TEXT", DisplayOrder = 1 }, "Admin");
        var state = await grain.GetTemplateAsync();
        Assert.That(state.Fields, Has.Count.EqualTo(1));

        await grain.RemoveFieldAsync("f1", "Admin");
        state = await grain.GetTemplateAsync();
        Assert.That(state.Fields, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task InstanceGrain_CreatesInstance()
    {
        string id = $"EF-INST:{Guid.NewGuid()}";
        var grain = GetInstance(id);
        var result = await grain.CreateInstanceAsync("EF-TPL:1", "DM Visit", "PATIENT-1", "DOE,JOHN", null, "PROV-1", "Dr. Jones");

        Assert.That(result.InstanceId, Is.EqualTo(id));
        Assert.That(result.TemplateId, Is.EqualTo("EF-TPL:1"));
        Assert.That(result.Status, Is.EqualTo("DRAFT"));
        Assert.That(result.PatientId, Is.EqualTo("PATIENT-1"));
    }

    [Test]
    public async Task InstanceGrain_SetsFieldValues()
    {
        string id = $"EF-INST:{Guid.NewGuid()}";
        var grain = GetInstance(id);
        await grain.CreateInstanceAsync("EF-TPL:1", "Form", "P-1", "DOE", null, "PROV-1", "Dr.");

        await grain.SetFieldValuesAsync(new Dictionary<string, string?> { ["bp_systolic"] = "120", ["bp_diastolic"] = "80" });

        var state = await grain.GetInstanceAsync();
        Assert.That(state.FieldValues["bp_systolic"], Is.EqualTo("120"));
        Assert.That(state.FieldValues["bp_diastolic"], Is.EqualTo("80"));
    }

    [Test]
    public async Task InstanceGrain_SubmitsForm()
    {
        string id = $"EF-INST:{Guid.NewGuid()}";
        var grain = GetInstance(id);
        await grain.CreateInstanceAsync("EF-TPL:1", "Form", "P-1", "DOE", null, "PROV-1", "Dr.");

        await grain.SubmitAsync("Dr. Jones");

        var state = await grain.GetInstanceAsync();
        Assert.That(state.Status, Is.EqualTo("SUBMITTED"));
        Assert.That(state.SubmittedByName, Is.EqualTo("Dr. Jones"));
        Assert.That(state.SubmittedDate, Is.Not.Null);
    }

    [Test]
    public async Task InstanceGrain_VoidsForm()
    {
        string id = $"EF-INST:{Guid.NewGuid()}";
        var grain = GetInstance(id);
        await grain.CreateInstanceAsync("EF-TPL:1", "Form", "P-1", "DOE", null, "PROV-1", "Dr.");
        await grain.SubmitAsync("Dr.");

        await grain.VoidAsync("Admin", "Entered in error");

        var state = await grain.GetInstanceAsync();
        Assert.That(state.Status, Is.EqualTo("VOIDED"));
        Assert.That(state.AmendReason, Is.EqualTo("Entered in error"));
    }

    [Test]
    public async Task TemplateIndex_UpdatedOnCreate()
    {
        string id = $"EF-TPL:{Guid.NewGuid()}";
        var grain = GetTemplate(id);
        await grain.CreateTemplateAsync("Index Test", "Desc", "ASSESSMENT", "CLINIC-X", SampleFields(), "Admin");

        var index = GetTemplateIndex();
        var all = await index.GetAllAsync();
        Assert.That(all.Any(e => e.TemplateId == id), Is.True);
        Assert.That(all.First(e => e.TemplateId == id).FormType, Is.EqualTo("ASSESSMENT"));
    }
}
